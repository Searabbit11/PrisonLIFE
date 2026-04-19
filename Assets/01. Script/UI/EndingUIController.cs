using UnityEngine;
using DG.Tweening;

public class EndingUIController : MonoBehaviour
{
    [SerializeField] private GameObject endingRoot;
    [Header("Bounce Motion")]
    [SerializeField] private RectTransform[] bounceTargets;
    [SerializeField] private float bounceHeight = 16f;
    [SerializeField] private float bounceDuration = 0.9f;
    [SerializeField] private float bounceStagger = 0.08f;
    private bool hasShown;
    private Vector2[] baseAnchoredPositions;
    public bool IsEndingVisible => hasShown && endingRoot != null && endingRoot.activeInHierarchy;

    private void Start()
    {
        if (endingRoot != null)
        {
            endingRoot.SetActive(false);
        }

        CacheBasePositions();
    }

    public void ShowEnding()
    {
        if (hasShown) return;
        hasShown = true;

        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayEndingShow();
        }

        if (endingRoot != null)
        {
            endingRoot.SetActive(true);
        }

        StartBounceMotion();
    }

    private void OnDisable()
    {
        StopBounceMotion(resetToBasePosition: true);
    }

    private void OnDestroy()
    {
        StopBounceMotion(resetToBasePosition: false);
    }

    private void CacheBasePositions()
    {
        if (bounceTargets == null || bounceTargets.Length == 0)
        {
            baseAnchoredPositions = null;
            return;
        }

        baseAnchoredPositions = new Vector2[bounceTargets.Length];
        for (int i = 0; i < bounceTargets.Length; i++)
        {
            RectTransform target = bounceTargets[i];
            baseAnchoredPositions[i] = target != null ? target.anchoredPosition : Vector2.zero;
        }
    }

    private void StartBounceMotion()
    {
        if (bounceTargets == null || bounceTargets.Length == 0) return;
        if (baseAnchoredPositions == null || baseAnchoredPositions.Length != bounceTargets.Length)
        {
            CacheBasePositions();
        }

        float duration = Mathf.Max(0.05f, bounceDuration);
        float height = Mathf.Max(0f, bounceHeight);
        for (int i = 0; i < bounceTargets.Length; i++)
        {
            RectTransform target = bounceTargets[i];
            if (target == null) continue;

            target.DOKill();
            if (baseAnchoredPositions != null && i < baseAnchoredPositions.Length)
            {
                target.anchoredPosition = baseAnchoredPositions[i];
            }

            target.DOAnchorPosY(target.anchoredPosition.y + height, duration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(i * bounceStagger)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
        }
    }

    private void StopBounceMotion(bool resetToBasePosition)
    {
        if (bounceTargets == null) return;

        for (int i = 0; i < bounceTargets.Length; i++)
        {
            RectTransform target = bounceTargets[i];
            if (target == null) continue;

            target.DOKill();
            if (resetToBasePosition && baseAnchoredPositions != null && i < baseAnchoredPositions.Length)
            {
                target.anchoredPosition = baseAnchoredPositions[i];
            }
        }
    }
}
