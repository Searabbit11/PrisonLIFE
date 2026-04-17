using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public sealed class CriminalManager : MonoBehaviour
{
    public static CriminalManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private GameObject criminalPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private int maxQueueSize = 2;
    [SerializeField] private float spawnCheckInterval = 2f;

    [Header("Waypoints")]
    [SerializeField] private Transform[] queuePoints;    // 대기열 위치 (2개)
    [SerializeField] public Transform[] prisonPathPoints; // 감옥으로 가는 경로

    [Header("Prison Settings")]
    [SerializeField] private Transform[] prisonSlots;     // 감옥 내 개별 자리 (정렬용)
    [SerializeField] private Transform prisonFocusPoint;  // 카메라가 주시할 특정 지점
    [SerializeField] private TMPro.TMP_Text capacityText; // "0/20" 표시용 UI
    [SerializeField] private int maxCapacity = 20;        // 최대 수용 인원
    [SerializeField] private GameObject upgradeZone;      // 정원 초과 시 활성화될 업그레이드 존

private List<Criminal> criminalQueue = new List<Criminal>();
private Dictionary<Transform, bool> slotOccupancy = new Dictionary<Transform, bool>();
private int currentPrisonerCount = 0;
private float nextSpawnCheckTime;
private bool hasNotifiedFull = false;

private void Awake()
{
    if (Instance == null) Instance = this;
    else Destroy(gameObject);

    InitializePrisonSlots();
    UpdateCapacityUI();
}

private void InitializePrisonSlots()
{
    if (prisonSlots == null) return;
    foreach (var slot in prisonSlots)
    {
        if (slot != null) slotOccupancy[slot] = false;
    }
}

/// <summary>
/// 감옥 내 빈 자리를 찾아 예약합니다. (시각적 카운트는 올리지 않음)
/// </summary>
public Transform GetAvailablePrisonSlot()
{
    foreach (var slot in prisonSlots)
    {
        if (slot != null && slotOccupancy[slot] == false)
        {
            slotOccupancy[slot] = true; // 자리 예약만 수행
            return slot;
        }
    }
    return null;
}

/// <summary>
/// 죄수가 실제로 감옥 자리에 도착했을 때 호출하여 UI 카운트를 올립니다.
/// </summary>
public void AddPrisonerVisualCount()
{
    currentPrisonerCount++;
    UpdateCapacityUI();
}

public void UpdateCapacityUI()
{
    if (capacityText != null)
    {
        capacityText.text = $"{currentPrisonerCount}/{maxCapacity}";
    }
}

/// <summary>
/// 업그레이드 시 최대 수용량을 늘리는 함수
/// </summary>
public void UpgradeCapacity(int newMax, Transform[] newSlots)
{
    maxCapacity = newMax;
    hasNotifiedFull = false;

    // 새로운 슬롯 추가
    if (newSlots != null)
    {
        foreach (var slot in newSlots)
        {
            if (slot != null && slotOccupancy.ContainsKey(slot) == false)
            {
                slotOccupancy[slot] = false;
            }
        }
    }

    UpdateCapacityUI();
}

/// <summary>
/// 정원 초과 시 카메라 연출을 실행합니다.
/// </summary>
public void NotifyQueueFull(Vector3 queuePosition)
{
    if (hasNotifiedFull == true) return;
    hasNotifiedFull = true;

    if (upgradeZone != null)
    {
        upgradeZone.SetActive(true);
        upgradeZone.transform.localScale = Vector3.zero;
        upgradeZone.transform.DOKill();
        upgradeZone.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
    }

    // 우선적으로 직접 지정한 지점을 사용하고, 없으면 슬롯들의 중간 지점 사용
    Vector3 focusPos = prisonFocusPoint != null 
        ? prisonFocusPoint.position 
        : (prisonSlots != null && prisonSlots.Length > 0 ? prisonSlots[prisonSlots.Length / 2].position : queuePosition);

    CameraController.Instance.FocusTarget(focusPos, 1.2f, 1.5f);
}

private void Update()
    {
        // 큐에 빈자리가 있고 스폰 쿨타임이 지났으면 생성
        if (criminalQueue.Count < maxQueueSize && Time.time >= nextSpawnCheckTime)
        {
            SpawnCriminal();
            nextSpawnCheckTime = Time.time + spawnCheckInterval;
        }
    }

    private void SpawnCriminal()
    {
        if (criminalPrefab == null || spawnPoint == null) return;

        GameObject go = Instantiate(criminalPrefab, spawnPoint.position, spawnPoint.rotation);
        Criminal criminal = go.GetComponent<Criminal>();
        
        if (criminal != null)
        {
            criminalQueue.Add(criminal);
            UpdateQueuePositions();
        }
    }

    /// <summary>
    /// 대기열 내의 모든 범죄자들을 자신의 순서에 맞는 위치로 이동시킵니다.
    /// </summary>
    public void UpdateQueuePositions()
    {
        for (int i = 0; i < criminalQueue.Count; i++)
        {
            if (i < queuePoints.Length)
            {
                Criminal criminal = criminalQueue[i];
                Transform targetPoint = queuePoints[i];

                // 이미 해당 위치에 있는지 체크 (중복 호출 방지)
                if (Vector3.Distance(criminal.transform.position, targetPoint.position) < 0.1f)
                {
                    // 0번 자리에 이미 있다면 말풍선 활성화 확인
                    if (i == 0) criminal.SetBubbleActive(true);
                    continue;
                }

                // 이동 시작
                if (i == 0)
                {
                    // 0번 자리는 도착 시 말풍선을 켬
                    criminal.MoveToQueuePoint(targetPoint, () => criminal.SetBubbleActive(true));
                }
                else
                {
                    // 그 외 자리는 이동만 수행
                    criminal.MoveToQueuePoint(targetPoint);
                }
            }
        }
    }

    /// <summary>
    /// 대기열의 가장 첫 번째 범죄자를 반환합니다. (데스크 납품용)
    /// </summary>
    public Criminal GetFirstCriminalInQueue()
    {
        if (criminalQueue.Count > 0) return criminalQueue[0];
        return null;
    }

    /// <summary>
    /// 수갑 지급이 완료된 범죄자를 큐에서 제거하고 뒤의 사람들을 전진시킵니다.
    /// </summary>
    public void RemoveFromQueue(Criminal criminal)
    {
        if (criminalQueue.Contains(criminal))
        {
            criminalQueue.Remove(criminal);
            UpdateQueuePositions();
        }
    }
}