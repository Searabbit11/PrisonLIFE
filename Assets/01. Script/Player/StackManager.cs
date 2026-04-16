using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class StackManager : MonoBehaviour
{
    private const int MaxStackCount = 10;
    [Header("Ore Stack Settings")]
    [SerializeField] private float yOffset = 0.18f;
    [SerializeField] private Transform stackPivot;
    private const float FollowSpeed = 15f;
    private readonly List<Transform> stackedItems = new List<Transform>();

    [Header("Handcuff Stack Settings")]
    [SerializeField] private Transform handcuffStackPivot;
    [SerializeField] private float handcuffYOffset = 0.1f;
    private readonly List<GameObject> handcuffItems = new List<GameObject>();

    [Header("Cash Stack Settings")]
    [SerializeField] private Transform cashStackPivot;
    [SerializeField] private float cashYOffset = 0.05f;
    private readonly List<GameObject> cashStack = new List<GameObject>();

    [Header("UI Settings")]
    [SerializeField] private TMP_Text maxTextLabel;

    private bool isMaxEffectPlaying;

    public bool IsFull => stackedItems.Count >= MaxStackCount;
    public int MaxCarryCount => MaxStackCount;
    public int OreCount => stackedItems.Count;
    public bool HasHandcuffs => handcuffItems.Count > 0;
    public int HandcuffCount => handcuffItems.Count;
    public bool HasCash => cashStack.Count > 0;
    public int CashCount => cashStack.Count;

    private void Start()
    {
        if (maxTextLabel != null)
        {
            maxTextLabel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 외부에서 호출 시 즉시 등 뒤 스택에 광석을 추가합니다.
    /// </summary>
    public void AddOre(GameObject orePrefab)
    {
        if (IsFull == true)
        {
            Debug.Log("Stack is Full!");
            return;
        }

        // PoolManager에서 광석을 가져옵니다.
        GameObject ore = PoolManager.Instance.SpawnFromPool("Ore", stackPivot.position, Quaternion.identity);
        if (ore == null) return;

        Transform oreTransform = ore.transform;
        // 생성된 광석의 부모를 플레이어의 스택 피벗으로 설정합니다.
        oreTransform.SetParent(stackPivot);
        
        // 생성 시 위치를 마지막 아이템(또는 피벗) 근처로 설정하여 순간이동 방지
        Vector3 startPos = stackedItems.Count == 0 ? stackPivot.position : stackedItems[^1].position;
        oreTransform.position = startPos + Vector3.up * yOffset;
        
        stackedItems.Add(oreTransform);

        // 뾱(Pop) 연출: 0 -> 1.2 -> 1.0
        oreTransform.localScale = Vector3.zero;
        oreTransform.DOScale(1.2f, 0.15f).SetEase(Ease.OutBack).OnComplete(() =>
        {
            oreTransform.DOScale(1.0f, 0.1f);
        });
    }

    private void Update()
    {
        UpdateStackMovement();
    }

    public void ShowMaxEffect()
    {
        if (maxTextLabel == null || isMaxEffectPlaying == true) return;

        isMaxEffectPlaying = true;

        // 기존 실행 중인 트윈이 있다면 중단
        maxTextLabel.DOKill();
        maxTextLabel.gameObject.SetActive(true);

        // 초기화
        maxTextLabel.transform.localPosition = Vector3.zero;
        maxTextLabel.alpha = 1f;

        // 위로 떠오르며 잠시 멈췄다가 페이드 아웃되는 연출
        Sequence seq = DOTween.Sequence();
        seq.Append(maxTextLabel.transform.DOLocalMoveY(0.5f, 0.2f).SetRelative(true).SetEase(Ease.OutQuad));
        seq.AppendInterval(1.0f);
        seq.Append(maxTextLabel.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
        seq.OnComplete(() => 
        {
            maxTextLabel.gameObject.SetActive(false);
            isMaxEffectPlaying = false;
        });
    }
    
    // 각 광석이 앞선 오브젝트를 따라가며 부드럽게 휘어지도록 계산
    private void UpdateStackMovement()
    {
        for (int i = 0; i < stackedItems.Count; i++)
        {
            Transform current = stackedItems[i];
            Transform target = (i == 0) ? stackPivot : stackedItems[i - 1];

            // 타겟 위치: 앞선 오브젝트의 위치 + 수직 오프셋
            Vector3 targetPos = target.position + Vector3.up * yOffset;

            // 부드러운 위치 추적 (지연 현상이 자연스러운 Swaying을 유도함)
            current.position = Vector3.Lerp(current.position, targetPos, Time.deltaTime * FollowSpeed);
            
            // 회전도 앞선 오브젝트를 따라가게 하여 역동성 추가
            current.rotation = Quaternion.Lerp(current.rotation, target.rotation, Time.deltaTime * FollowSpeed);
        }
    }
    
    // 나중에 가공기에 투입할 때 사용할 메서드
    public GameObject PopItem()
    {
        if (stackedItems.Count == 0) return null;

        Transform lastItem = stackedItems[^1];
        stackedItems.RemoveAt(stackedItems.Count - 1);
        
        // 스택에서 빠져나왔으므로 부모 관계 해제
        lastItem.SetParent(null);
        return lastItem.gameObject;
    }

    #region Handcuff Stack Logic (가슴 앞)
    public void PushHandcuff(GameObject handcuff)
    {
        // 부모 설정을 최우선으로 수행하여 로컬 좌표계 기준점을 잡습니다.
        handcuff.transform.SetParent(handcuffStackPivot);
        
        // 리스트에 추가
        handcuffItems.Add(handcuff);
    }

    public GameObject PopHandcuff()
    {
        if (handcuffItems.Count == 0) return null;

        GameObject last = handcuffItems[^1];
        handcuffItems.RemoveAt(handcuffItems.Count - 1);
        last.transform.SetParent(null);
        return last;
    }

    public Vector3 GetNextHandcuffPosition()
    {
        // X, Z축 오프셋을 제거하고 오직 Y축 스택 위치(로컬 좌표)만 반환
        return new Vector3(0f, (handcuffItems.Count) * handcuffYOffset, 0f);
    }
    #endregion

    #region Cash Stack Logic (등 뒤)
    public void PushCash(GameObject cash)
    {
        // 1. 기존 트윈 종료 및 물리 충돌 방지
        cash.transform.DOKill();
        if (cash.TryGetComponent<Collider>(out var col)) col.enabled = false;

        // 2. 부모 설정 및 리스트 추가
        cash.transform.SetParent(cashStackPivot);
        cashStack.Add(cash);

        // 3. 로컬 목표 위치 계산 (Y축 적재)
        Vector3 targetLocalPos = new Vector3(0, (cashStack.Count - 1) * cashYOffset, 0);

        // 4. 점프 연출로 등 뒤 피벗에 안착
        cash.transform.DOLocalJump(targetLocalPos, 1.5f, 1, 0.4f).SetEase(Ease.OutQuad);
        cash.transform.DOLocalRotate(Vector3.zero, 0.4f);

        // CurrencyManager 연동: 보유 금액 증가
        CurrencyManager.Instance.AddCash(1);
    }

    public GameObject PopCash()
    {
        if (cashStack.Count == 0) return null;

        GameObject lastCash = cashStack[^1];
        cashStack.RemoveAt(cashStack.Count - 1);
        lastCash.transform.SetParent(null);
        return lastCash;
    }
    #endregion
}
