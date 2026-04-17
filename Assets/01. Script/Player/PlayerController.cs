using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerController : MonoBehaviour
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsMining = Animator.StringToHash("IsMining");
    private static readonly int IsDrill = Animator.StringToHash("IsDrill");
    private const float RotationSpeed = 15f;
    private const float SnappingAngle = 45f;
    private const int MaxMiningHits = 32;

    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private float movementSpeed = 5f;

    [Header("Mining Settings")]
    [SerializeField] private Vector3 baseMiningDetectionBoxSize = new Vector3(2f, 3f, 2f);
    [SerializeField] private Vector3 miningDetectionOffset = new Vector3(0f, 0f, 0.5f);
    [SerializeField] private LayerMask resourceLayer;

    [Header("Drill 1 Mining Settings")]
    [SerializeField] private Vector3 drill1DetectionBoxSize = new Vector3(3f, 3f, 3f);
    [SerializeField] private Vector3 drill1DetectionOffset = new Vector3(0f, 0f, 1f);

    [Header("Drill 2 Mining Settings")]
    [SerializeField] private Vector3 drill2DetectionBoxSize = new Vector3(4f, 3f, 4f);
    [SerializeField] private Vector3 drill2DetectionOffset = new Vector3(0f, 0f, 1.5f);

    [Header("Drill General Settings")]
    [SerializeField] private float drillHitDelay = 0.05f;

    [SerializeField] private Animator animator;

    private Vector3 miningDetectionBoxBonus = Vector3.zero;
    private Rigidbody rb;
    private StackManager stackManager;
    private EquipmentManager equipmentManager;
    private Camera mainCam;
    private Vector3 moveDirection = Vector3.zero;
    private bool isMiningState;

    private readonly Collider[] miningHitBuffer = new Collider[MaxMiningHits];
    private readonly ResourceNode[] miningNodeBuffer = new ResourceNode[MaxMiningHits];
    private WaitForSeconds drillHitWait;

    public bool IsInMiningZone { get; set; }

    private Vector3 CurrentMiningDetectionBoxSize
    {
        get
        {
            Vector3 baseSize = baseMiningDetectionBoxSize;
            if (equipmentManager != null)
            {
                if (equipmentManager.IsDrill2Equipped) baseSize = drill2DetectionBoxSize;
                else if (equipmentManager.IsDrill1Equipped) baseSize = drill1DetectionBoxSize;
            }
            return baseSize + miningDetectionBoxBonus;
        }
    }

    private Vector3 CurrentMiningOffset
    {
        get
        {
            if (equipmentManager != null)
            {
                if (equipmentManager.IsDrill2Equipped) return drill2DetectionOffset;
                if (equipmentManager.IsDrill1Equipped) return drill1DetectionOffset;
            }
            return miningDetectionOffset;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        stackManager = GetComponent<StackManager>();
        equipmentManager = GetComponent<EquipmentManager>();
        mainCam = Camera.main;
        drillHitWait = new WaitForSeconds(drillHitDelay);
    }

    private void OnValidate()
    {
        if (drillHitDelay < 0f) drillHitDelay = 0f;
        if (Application.isPlaying) drillHitWait = new WaitForSeconds(drillHitDelay);
    }

    private void Update()
    {
        HandleInput();
        HandleRotation();
        HandleMovement();
        UpdateAnimation();
        HandleMiningAnimation();
    }

    public void IncreaseMiningRange(Vector3 bonusSize)
    {
        miningDetectionBoxBonus += bonusSize;
    }

    public void SetMiningState(bool isMining)
    {
        if (animator == null || isMiningState == isMining) return;

        isMiningState = isMining;
        animator.SetBool(IsMining, isMining);
    }

    private void HandleMiningAnimation()
    {
        if (!IsInMiningZone)
        {
            SetMiningState(false);
            return;
        }

        Vector3 detectPosition = transform.position + (transform.rotation * CurrentMiningOffset);
        bool isResourceNearby = Physics.CheckBox(detectPosition, CurrentMiningDetectionBoxSize * 0.5f, transform.rotation, resourceLayer);
        SetMiningState(isResourceNearby);
    }

    private void HandleInput()
    {
        if (CameraController.Instance != null && CameraController.Instance.IsFocusing)
        {
            moveDirection = Vector3.zero;
            SetMiningState(false);
            return;
        }

        if (joystick == null || joystick.Direction.sqrMagnitude < 0.0025f) // 0.05 * 0.05
        {
            moveDirection = Vector3.zero;
            return;
        }

        Vector2 input = joystick.Direction;
        float cameraYaw = mainCam != null ? mainCam.transform.eulerAngles.y : 0f;
        float inputAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
        float snappedAngle = Mathf.Round(inputAngle / SnappingAngle) * SnappingAngle;

        moveDirection = Quaternion.Euler(0f, snappedAngle + cameraYaw, 0f) * Vector3.forward;
    }

    private void HandleMovement()
    {
        if (moveDirection == Vector3.zero) return;
        transform.position += moveDirection * (movementSpeed * Time.deltaTime);
    }

    private void HandleRotation()
    {
        if (moveDirection == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, RotationSpeed * Time.deltaTime);
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        animator.SetFloat(Speed, moveDirection == Vector3.zero ? 0f : 1f);
        if (equipmentManager != null)
        {
            animator.SetBool(IsDrill, equipmentManager.IsDrillEquipped);
        }
    }

    public void OnMiningHit()
    {
        if (equipmentManager != null && equipmentManager.IsDrillEquipped)
        {
            StartCoroutine(DrillMiningRoutine());
            return;
        }

        int nodeCount = CollectMiningNodesNonAlloc();
        if (nodeCount == 0) return;

        int nearestIndex = 0;
        float nearestDist = float.MaxValue;
        Vector3 origin = transform.position;

        for (int i = 0; i < nodeCount; i++)
        {
            ResourceNode node = miningNodeBuffer[i];
            if (node == null) continue;

            float dist = (node.transform.position - origin).sqrMagnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestIndex = i;
            }
        }

        ResourceNode nearestNode = miningNodeBuffer[nearestIndex];
        if (nearestNode != null)
        {
            nearestNode.TakeDamage(1, stackManager);
        }
    }

    private IEnumerator DrillMiningRoutine()
    {
        int nodeCount = CollectMiningNodesNonAlloc();
        if (nodeCount == 0) yield break;

        Vector3 origin = transform.position;

        // Insertion sort by distance to avoid per-call List allocations.
        for (int i = 1; i < nodeCount; i++)
        {
            ResourceNode key = miningNodeBuffer[i];
            if (key == null) continue;

            float keyDist = (key.transform.position - origin).sqrMagnitude;
            int j = i - 1;
            while (j >= 0)
            {
                ResourceNode prev = miningNodeBuffer[j];
                if (prev == null)
                {
                    j--;
                    continue;
                }

                float prevDist = (prev.transform.position - origin).sqrMagnitude;
                if (prevDist <= keyDist) break;

                miningNodeBuffer[j + 1] = prev;
                j--;
            }

            miningNodeBuffer[j + 1] = key;
        }

        for (int i = 0; i < nodeCount; i++)
        {
            ResourceNode node = miningNodeBuffer[i];
            if (node == null) continue;

            node.TakeDamage(1, stackManager);
            yield return drillHitWait;
        }
    }

    private int CollectMiningNodesNonAlloc()
    {
        Vector3 detectPosition = transform.position + transform.rotation * CurrentMiningOffset;
        int hitCount = Physics.OverlapBoxNonAlloc(
            detectPosition,
            CurrentMiningDetectionBoxSize / 2f,
            miningHitBuffer,
            transform.rotation,
            resourceLayer);

        int nodeCount = 0;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = miningHitBuffer[i];
            if (hit != null && hit.TryGetComponent<ResourceNode>(out var node) && nodeCount < miningNodeBuffer.Length)
            {
                miningNodeBuffer[nodeCount++] = node;
            }

            miningHitBuffer[i] = null;
        }

        for (int i = nodeCount; i < miningNodeBuffer.Length; i++)
        {
            miningNodeBuffer[i] = null;
        }

        return nodeCount;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(miningDetectionOffset, baseMiningDetectionBoxSize + miningDetectionBoxBonus);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(drill1DetectionOffset, drill1DetectionBoxSize + miningDetectionBoxBonus);

        Gizmos.color = new Color(0.6f, 0f, 0.8f, 1f);
        Gizmos.DrawWireCube(drill2DetectionOffset, drill2DetectionBoxSize + miningDetectionBoxBonus);
    }
#endif
}
