using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class DeliveryDesk : MonoBehaviour
{
    [Header("Stack Settings")]
    [SerializeField] private Transform deskStackPivot;
    [SerializeField] private float stackYOffset = 0.15f;
    [SerializeField] private float collectInterval = 0.1f;
    [SerializeField] private float deliveryInterval = 0.2f;

    [Header("Reward Settings")]
    [SerializeField] private Transform cashSpawnPoint;
    [SerializeField] private GameObject cashPrefab;
    [SerializeField] private float cashJumpPower = 2f;
    [SerializeField] private float cashSpreadRange = 1.5f;

    [Header("Visual Feedback")]
    [SerializeField] private float jumpPower = 1.5f;
    [SerializeField] private float jumpDuration = 0.4f;

    private readonly List<GameObject> deskHandcuffs = new List<GameObject>();
    private Coroutine collectCoroutine;
    private float nextDeliveryTime;

    private void Update()
    {
        HandleHandcuffDelivery();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent<StackManager>(out var stackManager))
        {
            if (collectCoroutine != null) StopCoroutine(collectCoroutine);
            collectCoroutine = StartCoroutine(CollectHandcuffsRoutine(stackManager));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (collectCoroutine != null)
            {
                StopCoroutine(collectCoroutine);
                collectCoroutine = null;
            }
        }
    }
    
    // 플레이어의 앞면 스택에서 데스크로 수갑을 수거하는 루틴
    private IEnumerator CollectHandcuffsRoutine(StackManager playerStack)
    {
        while (true)
        {
            if (playerStack.HasHandcuffs == true)
            {
                GameObject handcuff = playerStack.PopHandcuff();
                if (handcuff != null)
                {
                    // 데스크 리스트에 추가 및 적재 위치 계산
                    int currentIndex = deskHandcuffs.Count;
                    deskHandcuffs.Add(handcuff);

                    Vector3 targetLocalPos = new Vector3(0, currentIndex * stackYOffset, 0);
                    
                    // 부모를 데스크 피벗으로 변경 후 로컬 점프
                    handcuff.transform.SetParent(deskStackPivot);
                    handcuff.transform.DOLocalJump(targetLocalPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
                    handcuff.transform.DOLocalRotate(Vector3.zero, jumpDuration);
                }
            }
            yield return new WaitForSeconds(collectInterval);
        }
    }

    /// <summary>
    /// 데스크에 쌓인 수갑을 대기열 1번 범죄자에게 전달
    /// </summary>
    private void HandleHandcuffDelivery()
    {
        if (Time.time < nextDeliveryTime || deskHandcuffs.Count == 0) return;

        // CriminalManager에서 대기열 첫 번째 범죄자 가져오기 (싱글톤 전제)
        var targetCriminal = CriminalManager.Instance.GetFirstCriminalInQueue();
        
        // 범죄자가 존재하고 아직 수갑이 더 필요한 상태인지 확인
        if (targetCriminal != null && targetCriminal.NeedsHandcuffs == true)
        {
            nextDeliveryTime = Time.time + deliveryInterval;

            // 데스크 스택의 맨 위(마지막 인덱스) 수갑 추출
            int lastIndex = deskHandcuffs.Count - 1;
            GameObject handcuff = deskHandcuffs[lastIndex];
            deskHandcuffs.RemoveAt(lastIndex);

            // 범죄자에게 날려보내기
            handcuff.transform.SetParent(null);
            // 범죄자의 가슴 또는 피벗 위치로 점프 (Criminal 스크립트의 포지션 활용)
            handcuff.transform.DOJump(targetCriminal.GetDeliverPivot().position, jumpPower, 1, jumpDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    // 전달 완료 시 수갑 파괴 및 범죄자 요구량 감소
                    Destroy(handcuff);
                    targetCriminal.ReceiveHandcuff();
                });
        }
    }

    /// <summary>
    /// 범죄자 퇴장 시 호출되어 사방으로 현금을 살포
    /// </summary>
    /// <param name="initialRequiredAmount">범죄자가 처음에 요구했던 수갑 개수</param>
    public void SpawnCash(int initialRequiredAmount)
    {
        if (cashPrefab == null || cashSpawnPoint == null) return;

        int spawnCount = initialRequiredAmount * 2;

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject cash = Instantiate(cashPrefab, cashSpawnPoint.position, Quaternion.identity);
            
            // 무작위 흩어짐 위치 계산
            Vector3 randomOffset = new Vector3(
                Random.Range(-cashSpreadRange, cashSpreadRange),
                0,
                Random.Range(-cashSpreadRange, cashSpreadRange)
            );
            Vector3 targetPos = cashSpawnPoint.position + randomOffset;

            // 튀어오르는 연출 (DOJump)
            cash.transform.DOJump(targetPos, cashJumpPower, 1, 0.5f + Random.Range(0f, 0.2f))
                .SetEase(Ease.OutBack);
            
            // 랜덤한 회전 추가로 자연스러움 부여
            cash.transform.DORotate(new Vector3(0, Random.Range(0, 360f), 0), 0.5f);
        }
    }
}
