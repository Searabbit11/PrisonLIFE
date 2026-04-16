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
    [SerializeField] private float cashWidthOffset = 0.4f;  // X축 (가로 2열)
    [SerializeField] private float cashDepthOffset = 0.6f;  // Z축 (세로 3행)
    [SerializeField] private float cashHeightOffset = 0.1f; // Y축 (층간 높이)

    [Header("Visual Feedback")]
    [SerializeField] private float jumpPower = 1.5f;
    [SerializeField] private float jumpDuration = 0.4f;

    private readonly List<GameObject> deskHandcuffs = new List<GameObject>();
    private Coroutine collectCoroutine;
    private float nextDeliveryTime;
    private int totalSpawnedCashCount = 0; // 누적 생성된 지폐 수 (그리드 위치 계산용)

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

            // 1. 기존 트윈(플레이어로부터 날아오던 연출 등)을 즉시 종료
            handcuff.transform.DOKill();

            // 2. 시작 위치를 현재 데스크 스택의 정확한 위치로 강제 고정
            // (deskHandcuffs.Count는 위에서 Remove했으므로 현재 index와 동일함)
            Vector3 stackLocalPos = new Vector3(0, deskHandcuffs.Count * stackYOffset, 0);
            handcuff.transform.position = deskStackPivot.TransformPoint(stackLocalPos);

            // 3. 부모 해제
            handcuff.transform.SetParent(null);

            // 4. 범죄자의 가슴 또는 피벗 위치로 점프
            handcuff.transform.DOJump(targetCriminal.GetDeliverPivot().position, jumpPower, 1, jumpDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    // 전달 완료 시 수갑 풀로 반환 및 범죄자 요구량 감소
                    PoolManager.Instance.ReturnToPool("Handcuff", handcuff);
                    targetCriminal.ReceiveHandcuff();
                });
        }
    }

    /// <summary>
    /// 범죄자 퇴장 시 호출되어 6개 그리드 기반(2x3)으로 현금을 적재
    /// </summary>
    /// <param name="initialRequiredAmount">범죄자가 처음에 요구했던 수갑 개수</param>
    /// <param name="spawnOrigin">지폐가 생겨날 시작 위치 (범죄자 위치)</param>
    public void SpawnCash(int initialRequiredAmount, Transform spawnOrigin)
    {
        if (cashSpawnPoint == null || spawnOrigin == null) return;

        int amountToSpawn = initialRequiredAmount * 2;

        for (int i = 0; i < amountToSpawn; i++)
        {
            // 지폐를 풀에서 가져옴 (범죄자 위치에서 생성)
            GameObject cash = PoolManager.Instance.SpawnFromPool("Cash", spawnOrigin.position, Quaternion.identity);
            if (cash == null) continue;
            
            // 누적 개수(totalSpawnedCashCount)를 사용하여 레이어와 그리드 인덱스 계산
            int layer = totalSpawnedCashCount / 6;
            int indexInLayer = totalSpawnedCashCount % 6;

            // 2x3 그리드 배치 계산
            int xPos = indexInLayer % 2; // 가로 (0, 1)
            int zPos = indexInLayer / 2; // 세로 (0, 1, 2)

            Vector3 targetOffset = new Vector3(
                xPos * cashWidthOffset, 
                layer * cashHeightOffset, 
                zPos * cashDepthOffset
            );
            
            // 데스크의 스폰 포인트 회전을 고려한 최종 좌표 계산
            Vector3 targetPos = cashSpawnPoint.position + (cashSpawnPoint.rotation * targetOffset);

            // 지정된 위치로 점프하여 정렬 (약간의 랜덤 시간으로 자연스럽게)
            cash.transform.DOJump(targetPos, cashJumpPower, 1, 0.5f + Random.Range(0f, 0.2f))
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    // 안착 시 통통 튀는 스케일 연출
                    cash.transform.DOPunchScale(new Vector3(0.3f, 0.3f, 0.3f), 0.2f);
                });
            
            // 회전은 데스크 스폰 포인트의 회전에 맞춤
            cash.transform.DORotateQuaternion(cashSpawnPoint.rotation, 0.5f);

            // 지폐를 하나 생성할 때마다 누적 카운트 증가
            totalSpawnedCashCount++;
        }
    }
}
