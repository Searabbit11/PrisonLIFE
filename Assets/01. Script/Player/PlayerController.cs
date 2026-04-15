using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerController : MonoBehaviour
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsMining = Animator.StringToHash("IsMining");
    private const float RotationSpeed = 15f;
    private const float SnappingAngle = 45f;
    private const float MinInputMagnitude = 0.1f;

    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private float movementSpeed = 5f;
    
    [Header("Mining Settings")]
    [SerializeField] private float miningDetectionRange = 1.5f; // 감지 범위
    [SerializeField] private Vector3 miningDetectionOffset = new Vector3(0f, 0f, 0.5f); // 감지 오프셋
    [SerializeField] private LayerMask resourceLayer;  // 바위 레이어 설정
    
    [SerializeField] private Animator animator;

    private Rigidbody rb;
    private StackManager stackManager;
    private EquipmentManager equipmentManager;
    private Vector3 moveDirection = Vector3.zero;
    private float currentInputMagnitude = 0f;
    private bool isMiningState = false; // 중복 호출 방지용

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
        HandleMiningDetection(); // 실시간 채굴 대상 감지
    }

    private void HandleInput()
    {
        if (joystick == null || joystick.Direction == Vector2.zero) 
        {
            moveDirection = Vector3.zero;
            currentInputMagnitude = 0f;
            return;
        }

        Vector2 input = joystick.Direction;
        currentInputMagnitude = input.magnitude;

        if (currentInputMagnitude < 0.05f) 
        {
            moveDirection = Vector3.zero;
            return;
        }

        // 8방향 이동을 위한 입력 스냅 (45도 단위)
        float angle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
        float snappedAngle = Mathf.Round(angle / SnappingAngle) * SnappingAngle;
        
        // 이동 방향을 즉시 갱신
        moveDirection = Quaternion.Euler(0, snappedAngle, 0) * Vector3.forward;
    }

    private void HandleMovement()
    {
        if (moveDirection == Vector3.zero) return;

        // 즉각적인 반응을 위해 transform.Translate 또는 position 직접 변경 사용
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
            animator.SetFloat(Speed, currentInputMagnitude);
        }
    }

    private void HandleMiningDetection()
    {
        Vector3 detectPosition = transform.position + transform.rotation * miningDetectionOffset;
        bool isResourceNearby = Physics.CheckSphere(detectPosition, miningDetectionRange, resourceLayer);

        if (isResourceNearby)
        {
            if (equipmentManager != null) equipmentManager.SetWeaponVisibility(true);
            SetMiningState(true);
        }
        else
        {
            if (equipmentManager != null) equipmentManager.SetWeaponVisibility(false);
            SetMiningState(false);
        }
    }
    
    // 애니메이션 이벤트에서 호출될 메서드 
    public void OnMiningHit()
    {
        Vector3 detectPosition = transform.position + transform.rotation * miningDetectionOffset;
        Collider[] hitNodes = Physics.OverlapSphere(detectPosition, miningDetectionRange, resourceLayer);

        foreach (var hit in hitNodes)
        {
            if (hit.TryGetComponent<ResourceNode>(out var node))
            {
                // 바위에게 데미지를 입힘
                node.TakeDamage(1, stackManager); 
                Debug.Log("바위 타격 성공!");
                break; // 한 번에 하나의 바위만 캐도록 설정
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 detectPosition = transform.position + transform.rotation * miningDetectionOffset;
        Gizmos.DrawWireSphere(detectPosition, miningDetectionRange);
    }
#endif
}
