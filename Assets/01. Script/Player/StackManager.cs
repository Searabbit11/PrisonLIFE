using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class StackManager : MonoBehaviour
{
    private const int MaxStackCount = 10;
    [Header("Stack Settings")]
    [SerializeField] private float yOffset = 0.18f;
    private const float FollowSpeed = 15f;

    [Header("UI Settings")]
    [SerializeField] private TMP_Text maxTextLabel;

    [SerializeField] private Transform stackPivot;
    private readonly List<Transform> stackedItems = new List<Transform>();
    private bool isMaxEffectPlaying;

    public bool IsFull => stackedItems.Count >= MaxStackCount;

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

        // 광석 생성 및 초기 설정
        GameObject ore = Instantiate(orePrefab);
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
}
