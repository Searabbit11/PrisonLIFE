using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerController : MonoBehaviour
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    private const float RotationSpeed = 15f;
    private const float SnappingAngle = 45f;
    private const float MinInputMagnitude = 0.1f;

    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private Animator animator;

    private Rigidbody rb;
    private Vector3 moveDirection = Vector3.zero;
    private float currentInputMagnitude = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // 물리 연산 배제, 트랜스폼 및 트리거 전용 상태로 설정
    }

    private void Update()
    {
        HandleInput();
        HandleRotation();
        HandleMovement(); // Update에서 즉각적으로 이동 처리 (반응성 향상)
        UpdateAnimation();
    }

    // FixedUpdate는 물리 충돌 판정에만 집중하고 이동은 Update로 통합하여 반응성 극대화
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
        // 하이퍼캐주얼에서는 이 방식이 조이스틱 조작 시 훨씬 가볍고 빠름
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
}