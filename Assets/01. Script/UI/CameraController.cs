using DG.Tweening;
using UnityEngine;

public sealed class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Target Settings")]
    [SerializeField] private Transform target; 
    
    [Header("Offset Settings")]
    [SerializeField] private Vector3 offset = new Vector3(-10f, 10f, -10f);
    
    public bool IsFocusing { get; private set; } = false;
    private const string TweenId = "CameraFocusTween";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 특정 타겟 위치에 따른 카메라의 최종 목표 위치를 반환합니다.
    /// </summary>
    public Vector3 GetCameraPositionForTarget(Vector3 targetPosition)
    {
        return targetPosition + offset;
    }

    /// <summary>
    /// 특정 위치를 일정 시간 동안 비췄다가 플레이어에게 부드럽게 돌아옵니다.
    /// </summary>
    public void FocusTarget(Vector3 targetPosition, float duration, float holdTime, float zoomMultiplier = 1f)
    {
        if (target == null) return;
        
        IsFocusing = true;
        DOTween.Kill(TweenId); // 기존에 진행 중인 연출이 있다면 강제 종료 후 덮어쓰기

        // 카메라의 높이(Y축)가 변하면 줌인/줌아웃 되는 것처럼 보일 수 있으므로, 
        // 플레이어의 Y값과 동일하게 맞추어 높이를 고정합니다. (줌아웃을 위한 배율은 오프셋에 적용)
        targetPosition.y = target.position.y;
        Vector3 cameraTargetPos = targetPosition + (offset * zoomMultiplier);

        Sequence seq = DOTween.Sequence().SetId(TweenId);
        // 1. 목표 지점으로 이동 (줌아웃 적용)
        seq.Append(transform.DOMove(cameraTargetPos, duration).SetEase(Ease.InOutQuad));
        // 2. 대기
        seq.AppendInterval(holdTime);
        // 3. 다시 플레이어 위치로 부드럽게 복귀 (복귀 도중 플레이어가 움직여도 덜컹거리지 않도록 동적 추적)
        seq.AppendCallback(() => 
        {
            Vector3 startReturnPos = transform.position;
            DOVirtual.Float(0f, 1f, duration, t =>
            {
                if (target != null)
                {
                    // Ease.InOutQuad의 효과를 직접 계산하여 적용
                    float easedT = DOVirtual.EasedValue(0f, 1f, t, Ease.InOutQuad);
                    transform.position = Vector3.Lerp(startReturnPos, target.position + offset, easedT);
                }
            }).SetId(TweenId).OnComplete(() => 
            {
                IsFocusing = false;
            });
        });
    }

    private void LateUpdate()
    {
        if (target == null || IsFocusing == true) return;
        
        transform.position = target.position + offset;
    }
}
