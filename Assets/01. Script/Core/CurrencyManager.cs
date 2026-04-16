using UnityEngine;
using TMPro;
using DG.Tweening;

public sealed class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI cashUIText;

    [Header("Tutorial Settings")]
    [SerializeField] private GameObject firstUnlockZone; // 처음 등장할 해금 구역

    private int currentCash = 0;
    private bool hasEarnedFirstCash = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Awake에서 SetActive(false)를 강제로 호출하지 않습니다. 
        // (에디터에서 비활성화된 오브젝트는 그대로 두고, 필요할 때만 켭니다.)
    }

    public void AddCash(int amount)
    {
        currentCash += amount;
        UpdateUI();

        if (hasEarnedFirstCash == false && currentCash > 0)
        {
            hasEarnedFirstCash = true;
            Debug.Log($"[CurrencyManager] 첫 재화 획득! 대기 중인 구역 활성화를 예약합니다: {firstUnlockZone.name}", firstUnlockZone);
            
            // 타이밍 문제를 방지하기 위해 코루틴으로 실행
            StartCoroutine(ShowZoneDelayedRoutine());
        }
    }

    private System.Collections.IEnumerator ShowZoneDelayedRoutine()
    {
        if (firstUnlockZone == null) yield break;

        // 물리 연산 프레임이 끝날 때까지 대기하여 확실한 상태 보장
        yield return new WaitForEndOfFrame();

        // 1. 활성화 (여기서 확실하게 켜집니다)
        firstUnlockZone.SetActive(true);
        
        // 2. 초기화 (스케일을 0으로 만들어 연출 준비)
        firstUnlockZone.transform.DOKill();
        firstUnlockZone.transform.localScale = Vector3.zero;
        
        // 3. 연출 (0.1초 정도 살짝 더 기다렸다가 스케일 업)
        yield return new WaitForSeconds(0.1f);
        firstUnlockZone.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        
        Debug.Log($"[CurrencyManager] {firstUnlockZone.name} 활성화 완료 및 연출 시작!");
    }

    /// <summary>
    /// 보유 금액을 감소시키고 UI를 즉시 갱신합니다.
    /// </summary>
    public void SpendCash(int amount)
    {
        currentCash -= amount;
        if (currentCash < 0) currentCash = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (cashUIText != null)
        {
            cashUIText.text = currentCash.ToString();
        }
    }
}
