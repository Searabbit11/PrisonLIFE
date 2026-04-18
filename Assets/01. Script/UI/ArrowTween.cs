using UnityEngine;
using DG.Tweening;

public class ArrowTween : MonoBehaviour
{
    [SerializeField] private float bobHeight = 0.2f;
    [SerializeField] private float bobDuration = 0.8f;
    [SerializeField] private float rotateDuration = 2f;

    private Vector3 startLocalPos;

    private void OnEnable()
    {
        startLocalPos = transform.localPosition;
        
        // 왕복
        transform.DOLocalMoveY(startLocalPos.y + bobHeight, bobDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
        
        // 회전
        transform.DOLocalRotate(new Vector3(0f, 360f, 0f), rotateDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
    }

    private void OnDisable()
    {
        transform.DOKill();
        transform.localPosition = startLocalPos;
    }
}