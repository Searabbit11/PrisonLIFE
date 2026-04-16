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

    private List<Criminal> criminalQueue = new List<Criminal>();
    private float nextSpawnCheckTime;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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
