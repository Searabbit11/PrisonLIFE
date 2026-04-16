using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class Criminal : MonoBehaviour
{
    private static readonly int IsWalking = Animator.StringToHash("isWalking");

    [Header("Requirements")]
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject bubbleUI;
    [SerializeField] private Transform deliverPivot;

    [Header("Visuals")]
    [SerializeField] private GameObject[] clothesMeshes;
    [SerializeField] private GameObject[] prisonerMeshes;
    [SerializeField] private Animator animator;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;

    private Camera mainCamera;
    private int requiredHandcuffs;
    private int initialRequiredHandcuffs;
    private bool isArrested;

    public bool NeedsHandcuffs => requiredHandcuffs > 0 && !isArrested;

    private void Start()
    {
        mainCamera = Camera.main;

        requiredHandcuffs = Random.Range(2, 5);
        initialRequiredHandcuffs = requiredHandcuffs;
        UpdateUI();

        SetRequirementUIVisible(false);
        ToggleMeshes(false);
    }

    public void SetBubbleActive(bool active)
    {
        SetRequirementUIVisible(active);
    }

    private void LateUpdate()
    {
        if (bubbleUI != null && bubbleUI.activeSelf && mainCamera != null)
        {
            bubbleUI.transform.forward = mainCamera.transform.forward;
        }
    }

    private void UpdateUI()
    {
        if (countText != null)
        {
            countText.text = requiredHandcuffs.ToString();
        }
    }

    private void SetRequirementUIVisible(bool visible)
    {
        bool shouldShow = visible && !isArrested && requiredHandcuffs > 0;

        if (bubbleUI != null)
        {
            bubbleUI.SetActive(shouldShow);
        }

        if (countText != null)
        {
            countText.gameObject.SetActive(shouldShow);
        }
    }

    public Transform GetDeliverPivot() => deliverPivot != null ? deliverPivot : transform;

    public void ReceiveHandcuff()
    {
        if (isArrested) return;

        requiredHandcuffs--;
        UpdateUI();

        if (requiredHandcuffs <= 0)
        {
            OnArrested();
        }
    }

    public void MoveToQueuePoint(Transform targetPoint, System.Action onComplete = null)
    {
        if (isArrested) return;

        float distance = Vector3.Distance(transform.position, targetPoint.position);
        float duration = distance / moveSpeed;

        animator.SetBool(IsWalking, true);

        transform.DOKill();
        transform.DORotateQuaternion(targetPoint.rotation, 0.2f);
        transform.DOMove(targetPoint.position, duration)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                animator.SetBool(IsWalking, false);
                onComplete?.Invoke();
            });
    }

    private void OnArrested()
    {
        isArrested = true;

        // Prevent stale queue-move callbacks from re-enabling bubble UI.
        transform.DOKill();
        SetRequirementUIVisible(false);

        DeliveryDesk desk = FindFirstObjectByType<DeliveryDesk>();
        if (desk != null)
        {
            desk.SpawnCash(initialRequiredHandcuffs, transform);
        }

        ToggleMeshes(true);

        CriminalManager.Instance.RemoveFromQueue(this);
        StartCoroutine(MoveAlongPathRoutine());
    }

    private IEnumerator MoveAlongPathRoutine()
    {
        Transform[] path = CriminalManager.Instance.prisonPathPoints;
        Transform targetSlot = CriminalManager.Instance.GetAvailablePrisonSlot(); // 빈 슬롯 할당받기
        
        animator.SetBool(IsWalking, true);

        // 1. 공통 경로를 따라 이동
        foreach (var waypoint in path)
        {
            float distance = Vector3.Distance(transform.position, waypoint.position);
            float duration = distance / moveSpeed;

            transform.DOLookAt(waypoint.position, 0.2f);
            yield return transform.DOMove(waypoint.position, duration)
                .SetEase(Ease.Linear)
                .WaitForCompletion();
        }

        // 2. 할당받은 개별 슬롯으로 이동
        if (targetSlot != null)
        {
            float distance = Vector3.Distance(transform.position, targetSlot.position);
            float duration = distance / moveSpeed;

            transform.DOLookAt(targetSlot.position, 0.2f);
            yield return transform.DOMove(targetSlot.position, duration)
                .SetEase(Ease.Linear)
                .WaitForCompletion();

            // 3. 도착 후 정렬 (슬롯의 방향을 바라보게 함)
            transform.DORotateQuaternion(targetSlot.rotation, 0.5f);
        }

        animator.SetBool(IsWalking, false);
    }

    private void ToggleMeshes(bool isPrisoner)
    {
        foreach (var m in clothesMeshes) m.SetActive(!isPrisoner);
        foreach (var m in prisonerMeshes) m.SetActive(isPrisoner);
    }
}
