using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class CriminalManager : MonoBehaviour
{
    public static CriminalManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private GameObject criminalPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private int maxQueueSize = 2;
    [SerializeField] private float spawnCheckInterval = 2f;

    [Header("Waypoints")]
    [SerializeField] private Transform[] queuePoints;
    [SerializeField] public Transform[] prisonPathPoints;

    [Header("Prison Settings")]
    [SerializeField] private Transform[] prisonSlots;
    [SerializeField] private Transform prisonFocusPoint;
    [SerializeField] private TMP_Text capacityText;
    [SerializeField] private int maxCapacity = 20;
    [SerializeField] private GameObject upgradeZone;

    private readonly List<Criminal> criminalQueue = new List<Criminal>();
    private readonly Dictionary<Transform, bool> slotOccupancy = new Dictionary<Transform, bool>();

    private int currentPrisonerCount;
    private float nextSpawnCheckTime;
    private bool hasNotifiedFull;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializePrisonSlots();
        UpdateCapacityUI();
    }

    private void Update()
    {
        if (criminalQueue.Count < maxQueueSize && Time.time >= nextSpawnCheckTime)
        {
            SpawnCriminal();
            nextSpawnCheckTime = Time.time + spawnCheckInterval;
        }
    }

    public Transform GetAvailablePrisonSlot()
    {
        foreach (Transform slot in prisonSlots)
        {
            if (slot != null && slotOccupancy.TryGetValue(slot, out bool occupied) && occupied == false)
            {
                slotOccupancy[slot] = true;
                return slot;
            }
        }

        return null;
    }

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

    public void UpgradeCapacity(int newMax, Transform[] newSlots)
    {
        maxCapacity = newMax;
        hasNotifiedFull = false;

        if (newSlots != null)
        {
            foreach (Transform slot in newSlots)
            {
                if (slot != null && slotOccupancy.ContainsKey(slot) == false)
                {
                    slotOccupancy[slot] = false;
                }
            }
        }

        UpdateCapacityUI();
    }

    public void NotifyQueueFull(Vector3 queuePosition)
    {
        if (hasNotifiedFull) return;
        hasNotifiedFull = true;

        if (upgradeZone != null)
        {
            upgradeZone.SetActive(true);
            upgradeZone.transform.localScale = Vector3.zero;
            upgradeZone.transform.DOKill();
            upgradeZone.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        }

        Vector3 focusPos = prisonFocusPoint != null
            ? prisonFocusPoint.position
            : (prisonSlots != null && prisonSlots.Length > 0 ? prisonSlots[prisonSlots.Length / 2].position : queuePosition);

        if (CameraController.Instance != null && CameraController.Instance.IsFocusing == false)
        {
            CameraController.Instance.FocusTarget(focusPos, 1.2f, 1.5f);
        }
    }

    public void UpdateQueuePositions()
    {
        for (int i = 0; i < criminalQueue.Count; i++)
        {
            if (queuePoints == null || i >= queuePoints.Length) break;

            Criminal criminal = criminalQueue[i];
            if (criminal == null) continue;

            Transform targetPoint = queuePoints[i];
            if (targetPoint == null) continue;

            if (i != 0)
            {
                criminal.SetCanReceiveHandcuffs(false);
                criminal.SetBubbleActive(false);
            }

            if (Vector3.Distance(criminal.transform.position, targetPoint.position) < 0.1f)
            {
                if (i == 0)
                {
                    criminal.SetCanReceiveHandcuffs(true);
                    criminal.SetBubbleActive(true);
                }
                continue;
            }

            if (i == 0)
            {
                criminal.SetCanReceiveHandcuffs(false);
                criminal.SetBubbleActive(false);
                criminal.MoveToQueuePoint(targetPoint, () =>
                {
                    criminal.SetCanReceiveHandcuffs(true);
                    criminal.SetBubbleActive(true);
                });
            }
            else
            {
                criminal.MoveToQueuePoint(targetPoint);
            }
        }
    }

    public Criminal GetFirstCriminalInQueue()
    {
        return criminalQueue.Count > 0 ? criminalQueue[0] : null;
    }

    public void RemoveFromQueue(Criminal criminal)
    {
        if (criminalQueue.Remove(criminal))
        {
            UpdateQueuePositions();
        }
    }

    private void InitializePrisonSlots()
    {
        slotOccupancy.Clear();
        if (prisonSlots == null) return;

        foreach (Transform slot in prisonSlots)
        {
            if (slot != null) slotOccupancy[slot] = false;
        }
    }

    private void SpawnCriminal()
    {
        if (criminalPrefab == null || spawnPoint == null) return;

        GameObject go = Instantiate(criminalPrefab, spawnPoint.position, spawnPoint.rotation);
        Criminal criminal = go.GetComponent<Criminal>();
        if (criminal == null) return;

        criminalQueue.Add(criminal);
        UpdateQueuePositions();
    }
}
