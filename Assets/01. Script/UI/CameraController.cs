using DG.Tweening;
using UnityEngine;

public sealed class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Target Settings")]
    [SerializeField] private Transform target; 
    
    [Header("Offset Settings")]
    [SerializeField] private Vector3 offset = new Vector3(-10f, 10f, -10f);
    
    private bool isFocusing = false;

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
    public void FocusTarget(Vector3 targetPosition, float duration, float holdTime)
    {
        if (isFocusing == true || target == null) return;
        isFocusing = true;

        Vector3 cameraTargetPos = targetPosition + offset;

        Sequence seq = DOTween.Sequence();
        // 1. 목표 지점으로 이동
        seq.Append(transform.DOMove(cameraTargetPos, duration).SetEase(Ease.InOutQuad));
        // 2. 대기
        seq.AppendInterval(holdTime);
        // 3. 다시 플레이어 위치로 부드럽게 복귀
        seq.Append(transform.DOMove(target.position + offset, duration).SetEase(Ease.InOutQuad));
        // 4. 완료 후 제어권 복구
        seq.OnComplete(() => isFocusing = false);
    }

    private void LateUpdate()
    {
        if (target == null || isFocusing == true) return;
        
        transform.position = target.position + offset;
    }
}
