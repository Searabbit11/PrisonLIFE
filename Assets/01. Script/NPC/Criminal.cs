using System.Collections;
using System;
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
    private DeliveryDesk deliveryDesk;

    private int requiredHandcuffs;
    private int initialRequiredHandcuffs;
    private bool isArrested;
    private bool isCounted;

    public bool NeedsHandcuffs => requiredHandcuffs > 0 && isArrested == false;

    private void Awake()
    {
        mainCamera = Camera.main;
        deliveryDesk = FindFirstObjectByType<DeliveryDesk>();
    }

    private void Start()
    {
        requiredHandcuffs = UnityEngine.Random.Range(minRequiredHandcuffs, maxRequiredHandcuffs);
        initialRequiredHandcuffs = requiredHandcuffs;

        UpdateUI();
        SetRequirementUIVisible(false);
        ToggleMeshes(false);
    }

    public void OnPassedGate()
    {
        if (isCounted) return;
        isCounted = true;
        CriminalManager.Instance.AddPrisonerVisualCount();
    }

    public void SetBubbleActive(bool active)
    {
        SetRequirementUIVisible(active);
    }

    private void LateUpdate()
    {
        if (isArrested || bubbleUI == null || bubbleUI.activeSelf == false || mainCamera == null)
        {
            return;
        }

        bubbleUI.transform.forward = mainCamera.transform.forward;
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

    public void MoveToQueuePoint(Transform targetPoint, Action onComplete = null)
    {
        if (isArrested || targetPoint == null) return;

        float distance = Vector3.Distance(transform.position, targetPoint.position);
        float duration = distance / Mathf.Max(0.01f, moveSpeed);

        if (animator != null) animator.SetBool(IsWalking, true);

        transform.DOKill();
        transform.DORotateQuaternion(targetPoint.rotation, rotationSpeed);
        transform.DOMove(targetPoint.position, duration)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                if (animator != null) animator.SetBool(IsWalking, false);
                onComplete?.Invoke();
            });
    }

    private void OnArrested()
    {
        isArrested = true;

        transform.DOKill();
        SetRequirementUIVisible(false);

        if (deliveryDesk != null)
        {
            deliveryDesk.SpawnCash(initialRequiredHandcuffs, transform);
        }

        ToggleMeshes(true);

        CriminalManager.Instance.RemoveFromQueue(this);
        StartCoroutine(MoveAlongPathRoutine());
    }

    private IEnumerator MoveAlongPathRoutine()
    {
        Transform[] path = CriminalManager.Instance.prisonPathPoints;
        Transform targetSlot = CriminalManager.Instance.GetAvailablePrisonSlot();

        if (animator != null) animator.SetBool(IsWalking, true);

        if (targetSlot == null)
        {
            if (path != null && path.Length > 0 && path[0] != null)
            {
                float distance = Vector3.Distance(transform.position, path[0].position);
                float duration = distance / Mathf.Max(0.01f, moveSpeed);
                transform.DOLookAt(path[0].position, rotationSpeed);
                yield return transform.DOMove(path[0].position, duration).SetEase(Ease.Linear).WaitForCompletion();
                CriminalManager.Instance.NotifyQueueFull(path[0].position);
            }

            if (animator != null) animator.SetBool(IsWalking, false);
            yield break;
        }

        if (path != null)
        {
            for (int i = 0; i < path.Length; i++)
            {
                Transform waypoint = path[i];
                if (waypoint == null) continue;

                float distance = Vector3.Distance(transform.position, waypoint.position);
                float duration = distance / Mathf.Max(0.01f, moveSpeed);

                transform.DOLookAt(waypoint.position, rotationSpeed);
                yield return transform.DOMove(waypoint.position, duration)
                    .SetEase(Ease.Linear)
                    .WaitForCompletion();
            }
        }

        float slotDistance = Vector3.Distance(transform.position, targetSlot.position);
        float slotDuration = slotDistance / Mathf.Max(0.01f, moveSpeed);

        transform.DOLookAt(targetSlot.position, rotationSpeed);
        yield return transform.DOMove(targetSlot.position, slotDuration)
            .SetEase(Ease.Linear)
            .WaitForCompletion();

        Quaternion targetRotation = targetSlot.rotation * Quaternion.Euler(0f, exitRotationAngle, 0f);
        yield return transform.DORotateQuaternion(targetRotation, 0.5f).WaitForCompletion();

        if (animator != null) animator.SetBool(IsWalking, false);
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
        bool shouldShow = visible && isArrested == false && requiredHandcuffs > 0;

        if (bubbleUI != null)
        {
            bubbleUI.SetActive(shouldShow);
        }

        if (countText != null)
        {
            countText.gameObject.SetActive(shouldShow);
        }
    }

    private void ToggleMeshes(bool isPrisoner)
    {
        for (int i = 0; i < clothesMeshes.Length; i++)
        {
            if (clothesMeshes[i] != null) clothesMeshes[i].SetActive(isPrisoner == false);
        }

        for (int i = 0; i < prisonerMeshes.Length; i++)
        {
            if (prisonerMeshes[i] != null) prisonerMeshes[i].SetActive(isPrisoner);
        }
    }
}
