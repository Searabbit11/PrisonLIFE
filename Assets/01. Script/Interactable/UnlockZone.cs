using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UnlockZone : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int targetAmount = 20;
    [SerializeField] private GameObject targetObject; // 해금될 대상 (예: 드릴)
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image fillImage;        // 바닥 게이지 이미지 (Filled 방식)
    
    [Header("Animation")]
    [SerializeField] private float consumeInterval = 0.1f;
    [SerializeField] private float jumpPower = 2f;

    private int currentAmount = 0;
    private Coroutine unlockCoroutine;
    private Collider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        
        if (fillImage != null) fillImage.fillAmount = 0f;
        
        UpdateUI();
        if (targetObject != null && targetObject != gameObject) targetObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent<StackManager>(out var playerStack))
        {
            if (unlockCoroutine != null) StopCoroutine(unlockCoroutine);
            unlockCoroutine = StartCoroutine(UnlockRoutine(playerStack));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (unlockCoroutine != null)
            {
                StopCoroutine(unlockCoroutine);
                unlockCoroutine = null;
            }
        }
    }

    private IEnumerator UnlockRoutine(StackManager playerStack)
    {
        while (currentAmount < targetAmount)
        {
            if (playerStack.HasCash == true)
            {
                GameObject cash = playerStack.PopCash();
                if (cash != null)
                {
                    // 1. 초기화 및 부모 해제
                    cash.transform.DOKill();
                    cash.transform.SetParent(null);

                    // 2. 해금 구역 중심으로 점프하며 소모 연출
                    cash.transform.DOJump(transform.position, jumpPower, 1, 0.4f)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() => Destroy(cash));

                    currentAmount++;
                    if (fillImage != null) fillImage.fillAmount = (float)currentAmount / targetAmount;
                    UpdateUI();

                    // CurrencyManager 연동: 보유 금액 감소
                    CurrencyManager.Instance.SpendCash(1);

                    if (currentAmount >= targetAmount)
                    {
                        CompleteUnlock();
                        yield break;
                    }
                }
            }
            yield return new WaitForSeconds(consumeInterval);
        }
    }

    private void UpdateUI()
    {
        if (costText != null)
        {
            int remaining = targetAmount - currentAmount;
            costText.text = remaining.ToString();
        }
    }

    private void CompleteUnlock()
    {
        if (zoneCollider != null) zoneCollider.enabled = false;
        if (costText != null) costText.gameObject.SetActive(false);
        
        if (targetObject != null && targetObject != gameObject)
        {
            targetObject.SetActive(true);
            // 해금 시 연출 (스케일 커지며 등장)
            targetObject.transform.localScale = Vector3.zero;
            targetObject.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        }

        Debug.Log("Unlock Completed!");
    }
}
