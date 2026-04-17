using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
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
    [SerializeField] private float rotationSpeed = 0.2f;
    [SerializeField] private float exitRotationAngle = 180f;

    [Header("Handcuff Settings")]
    [SerializeField] private int minRequiredHandcuffs = 2;
    [SerializeField] private int maxRequiredHandcuffs = 5;

    private Camera mainCamera;
    private int requiredHandcuffs;
    private int initialRequiredHandcuffs;
    private bool isArrested;
    private bool isCounted;

    public bool NeedsHandcuffs => requiredHandcuffs > 0 && !isArrested;

    public void OnPassedGate()
    {
        if (isCounted == true) return;
        isCounted = true;
        CriminalManager.Instance.AddPrisonerVisualCount();
    }

    private void Start()
    {
        mainCamera = Camera.main;

        requiredHandcuffs = Random.Range(minRequiredHandcuffs, maxRequiredHandcuffs);
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
        // 체포되었거나 말풍선이 꺼져있으면 연산 중단 (성능 최적화)
        if (isArrested == true || bubbleUI == null || bubbleUI.activeSelf == false || mainCamera == null)
        {
            return;
        }

        bubbleUI.transform.forward = mainCamera.transform.forward;
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
        transform.DORotateQuaternion(targetPoint.rotation, rotationSpeed);
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

        // 슬롯이 없으면 첫 번째 포인트까지만 가고 대기
        if (targetSlot == null)
        {
            if (path.Length > 0)
            {
                float distance = Vector3.Distance(transform.position, path[0].position);
                float duration = distance / moveSpeed;
                transform.DOLookAt(path[0].position, rotationSpeed);
                yield return transform.DOMove(path[0].position, duration).SetEase(Ease.Linear).WaitForCompletion();
                
                // [연출 추가] 죄수가 입구에 도착했을 때 카메라가 감옥을 비춤
                CriminalManager.Instance.NotifyQueueFull(path[0].position);
            }
            animator.SetBool(IsWalking, false);
            yield break;
        }

        // 1. 공통 경로를 따라 이동
        foreach (var waypoint in path)
        {
            float distance = Vector3.Distance(transform.position, waypoint.position);
            float duration = distance / moveSpeed;

            transform.DOLookAt(waypoint.position, rotationSpeed);
            yield return transform.DOMove(waypoint.position, duration)
                .SetEase(Ease.Linear)
                .WaitForCompletion();
        }

        // 2. 할당받은 개별 슬롯으로 이동
        float slotDistance = Vector3.Distance(transform.position, targetSlot.position);
        float slotDuration = slotDistance / moveSpeed;

        transform.DOLookAt(targetSlot.position, rotationSpeed);
        yield return transform.DOMove(targetSlot.position, slotDuration)
            .SetEase(Ease.Linear)
            .WaitForCompletion();

        // 3. 도착 후 반대 방향으로 정렬
        Quaternion targetRotation = targetSlot.rotation * Quaternion.Euler(0, exitRotationAngle, 0);
        yield return transform.DORotateQuaternion(targetRotation, 0.5f).WaitForCompletion();

        animator.SetBool(IsWalking, false);
    }

    private void ToggleMeshes(bool isPrisoner)
    {
        foreach (var m in clothesMeshes) m.SetActive(!isPrisoner);
        foreach (var m in prisonerMeshes) m.SetActive(isPrisoner);
    }
}
