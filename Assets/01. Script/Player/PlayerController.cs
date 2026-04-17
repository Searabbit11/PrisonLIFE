using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerController : MonoBehaviour
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsMining = Animator.StringToHash("IsMining");
    private static readonly int IsDrill = Animator.StringToHash("IsDrill"); // [추가] 드릴 애니메이션 파라미터 (애니메이터 컨트롤러 세팅 필요)
    private const float RotationSpeed = 15f;
    private const float SnappingAngle = 45f;
    private const float MinInputMagnitude = 0.1f;

    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private float movementSpeed = 5f;
    
    [Header("Mining Settings")]
    [SerializeField] private Vector3 baseMiningDetectionBoxSize = new Vector3(2f, 3f, 2f); // 기본 감지 박스 크기
    [SerializeField] private Vector3 miningDetectionOffset = new Vector3(0f, 0f, 0.5f); // 감지 오프셋
    [SerializeField] private LayerMask resourceLayer;  // 바위 레이어 설정
    
    [Header("Drill 1 Mining Settings")]
    [SerializeField] private Vector3 drill1DetectionBoxSize = new Vector3(3f, 3f, 3f);
    [SerializeField] private Vector3 drill1DetectionOffset = new Vector3(0f, 0f, 1f);

    [Header("Drill 2 Mining Settings")]
    [SerializeField] private Vector3 drill2DetectionBoxSize = new Vector3(4f, 3f, 4f);
    [SerializeField] private Vector3 drill2DetectionOffset = new Vector3(0f, 0f, 1.5f);

    [Header("Drill General Settings")]
    [SerializeField] private float drillHitDelay = 0.05f;  // 드릴 순차 타격 간격

    private Vector3 miningDetectionBoxBonus = Vector3.zero; // 업그레이드 보너스
    
    private Vector3 CurrentMiningDetectionBoxSize
    {
        get
        {
            if (equipmentManager != null)
            {
                if (equipmentManager.IsDrill2Equipped) return drill2DetectionBoxSize + miningDetectionBoxBonus;
                if (equipmentManager.IsDrill1Equipped) return drill1DetectionBoxSize + miningDetectionBoxBonus;
            }
            return baseMiningDetectionBoxSize + miningDetectionBoxBonus;
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

    [SerializeField] private Animator animator;

    private Rigidbody rb;
    private StackManager stackManager;
    private EquipmentManager equipmentManager;
    private Vector3 moveDirection = Vector3.zero;
    private bool isMiningState = false; // 중복 호출 방지용
    
    public bool IsInMiningZone { get; set; }

    public void IncreaseMiningRange(Vector3 bonusSize)
    {
        miningDetectionBoxBonus += bonusSize;
        Debug.Log($"[PlayerController] Mining range increased by {bonusSize}. Current: {CurrentMiningDetectionBoxSize}");
    }

    public void SetMiningState(bool isMining)
    {
        if (animator == null || isMiningState == isMining) return;

        isMiningState = isMining;
        animator.SetBool(IsMining, isMining);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // 물리 연산 배제, 트랜스폼 및 트리거 전용 상태로 설정
        stackManager = GetComponent<StackManager>();
        equipmentManager = GetComponent<EquipmentManager>();
    }

    private void Update()
    {
        HandleInput();
        HandleRotation();
        HandleMovement(); // Update에서 즉각적으로 이동 처리 (반응성 향상)
        UpdateAnimation();
        HandleMiningAnimation();
    }

    /// <summary>
    /// 구역 내에서 바위 감지 및 애니메이션 제어
    /// </summary>
    private void HandleMiningAnimation()
    {
        if (IsInMiningZone == false)
        {
            SetMiningState(false);
            return;
        }

        Vector3 detectPosition = transform.position + transform.rotation * CurrentMiningOffset;
        bool isResourceNearby = Physics.CheckBox(detectPosition, CurrentMiningDetectionBoxSize / 2f, transform.rotation, resourceLayer);
        
        SetMiningState(isResourceNearby);
    }

    private void HandleInput()
    {
        if (CameraController.Instance != null && CameraController.Instance.IsFocusing == true)
        {
            moveDirection = Vector3.zero;
            SetMiningState(false);
            return;
        }

        if (joystick == null || joystick.Direction == Vector2.zero) 
        {
            moveDirection = Vector3.zero;
            return;
        }

        Vector2 input = joystick.Direction;

        if (input.sqrMagnitude < 0.05f * 0.05f) 
        {
            moveDirection = Vector3.zero;
            return;
        }

        // 카메라의 Yaw(Y축 회전) 값 가져오기
        float cameraYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;

        // 8방향 이동을 위한 입력 스냅 (45도 단위)
        float inputAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
        float snappedAngle = Mathf.Round(inputAngle / SnappingAngle) * SnappingAngle;
        
        // 카메라 회전값을 더해 최종 이동 방향 결정
        moveDirection = Quaternion.Euler(0, snappedAngle + cameraYaw, 0) * Vector3.forward;
    }

    private void HandleMovement()
    {
        if (moveDirection == Vector3.zero) return;

        // 조이스틱 강도 반영 제거, 고정 속도 이동
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
        if (animator != null)
        {
            // 이동 여부에 따라 애니메이션 속도 파라미터를 0 또는 1로 고정
            float targetSpeed = moveDirection == Vector3.zero ? 0f : 1f;
            animator.SetFloat(Speed, targetSpeed);

            // 매 프레임 장비 상태를 애니메이터에 동기화하여 지연 발생 방지
            if (equipmentManager != null)
            {
                animator.SetBool(IsDrill, equipmentManager.IsDrillEquipped);
            }
        }
    }

    // 애니메이션 이벤트에서 호출될 메서드 
    public void OnMiningHit()
    {
        if (equipmentManager != null && equipmentManager.IsDrillEquipped)
        {
            StartCoroutine(DrillMiningRoutine());
        }
        else
        {
            Vector3 detectPosition = transform.position + transform.rotation * CurrentMiningOffset;
            Collider[] hitNodes = Physics.OverlapBox(detectPosition, CurrentMiningDetectionBoxSize / 2f, transform.rotation, resourceLayer);

            foreach (var hit in hitNodes)
            {
                if (hit.TryGetComponent<ResourceNode>(out var node))
                {
                    node.TakeDamage(1, stackManager);
                    break; // 곡괭이는 단일 타격 유지
                }
            }
        }
    }

    private System.Collections.IEnumerator DrillMiningRoutine()
    {
        Vector3 detectPosition = transform.position + transform.rotation * CurrentMiningOffset;
        Collider[] hitColliders = Physics.OverlapBox(detectPosition, CurrentMiningDetectionBoxSize / 2f, transform.rotation, resourceLayer);

        var nodes = new System.Collections.Generic.List<ResourceNode>();
        foreach (var hit in hitColliders)
        {
            if (hit.TryGetComponent<ResourceNode>(out var node))
            {
                nodes.Add(node);
            }
        }

        // 거리순 정렬 (가까운 것부터 타격)
        nodes.Sort((a, b) => 
            (a.transform.position - transform.position).sqrMagnitude.CompareTo(
            (b.transform.position - transform.position).sqrMagnitude));

        // 지정된 간격으로 순차 타격
        foreach (var node in nodes)
        {
            if (node != null) // 중간에 파괴되었으면 무시, 스택이 꽉 차도 채굴은 진행
            {
                node.TakeDamage(1, stackManager);
                yield return new WaitForSeconds(drillHitDelay);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        // Base mining range
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(miningDetectionOffset, baseMiningDetectionBoxSize + miningDetectionBoxBonus);

        // Drill 1 mining range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(drill1DetectionOffset, drill1DetectionBoxSize + miningDetectionBoxBonus);

        // Drill 2 mining range
        Gizmos.color = new Color(0.6f, 0f, 0.8f, 1f); // Purple
        Gizmos.DrawWireCube(drill2DetectionOffset, drill2DetectionBoxSize + miningDetectionBoxBonus);
    }
#endif
}
