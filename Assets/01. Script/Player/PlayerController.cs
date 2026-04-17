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
    [SerializeField] private Vector3 miningDetectionBoxSize = new Vector3(2f, 3f, 2f); // 감지 박스 크기
    [SerializeField] private Vector3 miningDetectionOffset = new Vector3(0f, 0f, 0.5f); // 감지 오프셋
    [SerializeField] private LayerMask resourceLayer;  // 바위 레이어 설정
    
    [SerializeField] private Animator animator;

    private Rigidbody rb;
    private StackManager stackManager;
    private EquipmentManager equipmentManager;
    private Vector3 moveDirection = Vector3.zero;
    private bool isMiningState = false; // 중복 호출 방지용
    
    public bool IsInMiningZone { get; set; }

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

        // 전방 바위 감지 (CheckBox 활용)
        Vector3 detectPosition = transform.position + transform.rotation * miningDetectionOffset;
        bool isResourceNearby = Physics.CheckBox(detectPosition, miningDetectionBoxSize / 2f, transform.rotation, resourceLayer);
        
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
        }
    }

    // 애니메이션 이벤트에서 호출될 메서드 
    public void OnMiningHit()
    {
        Vector3 detectPosition = transform.position + transform.rotation * miningDetectionOffset;
        Collider[] hitNodes = Physics.OverlapBox(detectPosition, miningDetectionBoxSize / 2f, transform.rotation, resourceLayer);

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
        
        // 플레이어의 위치와 회전을 기준으로 로컬 좌표계(Matrix)를 설정
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        
        // 설정된 로컬 좌표계 내에서 Offset 위치에 지정된 크기의 큐브를 그림
        Gizmos.DrawWireCube(miningDetectionOffset, miningDetectionBoxSize);
    }
#endif
}
