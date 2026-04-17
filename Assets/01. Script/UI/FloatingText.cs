using UnityEngine;
using TMPro;
using DG.Tweening;

public sealed class FloatingText : MonoBehaviour
{
    [SerializeField] private TMP_Text textLabel;
    private bool isPlaying;

    private void Awake()
    {
        if (textLabel != null) textLabel.gameObject.SetActive(false);
    }

    public void Show(string message)
    {
        if (textLabel == null || isPlaying) return;

        isPlaying = true;
        textLabel.text = message;
        textLabel.gameObject.SetActive(true);
        textLabel.DOKill();

        // 초기 상태
        textLabel.transform.localPosition = Vector3.zero;
        textLabel.alpha = 1f;

        // 연출
        Sequence seq = DOTween.Sequence();
        seq.Append(textLabel.transform.DOLocalMoveY(1.0f, 0.5f).SetRelative(true).SetEase(Ease.OutQuad));
        seq.AppendInterval(0.5f);
        seq.Append(textLabel.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
        seq.OnComplete(() =>
        {
            textLabel.gameObject.SetActive(false);
            isPlaying = false;
        });
    }
}
