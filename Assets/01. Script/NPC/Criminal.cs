using System.Collections;
using UnityEngine;
using DG.Tweening;
using TMPro;

public sealed class Criminal : MonoBehaviour
{
    private static readonly int IsWalking = Animator.StringToHash("isWalking");

    [Header("Requirements")]
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject bubbleUI;    // 말풍선 UI 전체
    [SerializeField] private Transform deliverPivot; // 수갑이 날아올 위치 (가슴 쪽)

    [Header("Visuals")]
    [SerializeField] private GameObject[] clothesMeshes;   // 평상복
    [SerializeField] private GameObject[] prisonerMeshes;  // 죄수복
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

        // 2~4 사이의 랜덤 요구량 설정
        requiredHandcuffs = Random.Range(2, 5);
        initialRequiredHandcuffs = requiredHandcuffs;
        UpdateUI();
        
        // 처음에는 말풍선 비활성화
        SetBubbleActive(false);

        // 초기 외형 설정 (평상복 ON, 죄수복 OFF)
        ToggleMeshes(false);
    }

    public void SetBubbleActive(bool active)
    {
        if (bubbleUI != null) bubbleUI.SetActive(active);
    }

    private void LateUpdate()
    {
        // 말풍선이 활성화되어 있을 때만 카메라를 향하도록 회전 (빌보드)
        if (bubbleUI != null && bubbleUI.activeSelf == true)
        {
            bubbleUI.transform.forward = mainCamera.transform.forward;
        }
    }

    private void UpdateUI()
    {
        if (countText != null)
            countText.text = requiredHandcuffs.ToString();
    }

    public Transform GetDeliverPivot() => deliverPivot != null ? deliverPivot : transform;

    /// <summary>
    /// 수갑을 전달받았을 때 호출됩니다.
    /// </summary>
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

    /// <summary>
    /// 대기열 내에서 지정된 위치로 이동합니다.
    /// </summary>
    public void MoveToQueuePoint(Transform targetPoint, System.Action onComplete = null)
    {
        if (isArrested) return;

        float distance = Vector3.Distance(transform.position, targetPoint.position);
        float duration = distance / moveSpeed;

        animator.SetBool(IsWalking, true);
        
        transform.DOKill();
        // LookAt 대신 타겟 포인트의 회전값에 맞춤
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
        
        // 말풍선 및 텍스트 비활성화
        if (bubbleUI != null) bubbleUI.SetActive(false);
        countText.gameObject.SetActive(false);

        // 1. 보상 생성 요청 (범죄자 자신의 위치에서 스폰되도록 transform 전달)
        DeliveryDesk desk = FindFirstObjectByType<DeliveryDesk>();
        if (desk != null) desk.SpawnCash(initialRequiredHandcuffs, transform);

        // 2. 외형 교체
        ToggleMeshes(true);

        // 3. 매니저에게 알려 큐에서 이탈 및 경로 이동 시작
        CriminalManager.Instance.RemoveFromQueue(this);
        StartCoroutine(MoveAlongPathRoutine());
    }

    private IEnumerator MoveAlongPathRoutine()
    {
        Transform[] path = CriminalManager.Instance.prisonPathPoints;
        animator.SetBool(IsWalking, true);

        foreach (var waypoint in path)
        {
            float distance = Vector3.Distance(transform.position, waypoint.position);
            float duration = distance / moveSpeed;

            // 회전 및 이동
            transform.DOLookAt(waypoint.position, 0.2f);
            yield return transform.DOMove(waypoint.position, duration)
                .SetEase(Ease.Linear)
                .WaitForCompletion();
        }

        // 마지막 지점 도착 시 Idle 상태로 무한 대기 (Destroy 제거)
        animator.SetBool(IsWalking, false);
    }

    private void ToggleMeshes(bool isPrisoner)
    {
        foreach (var m in clothesMeshes) m.SetActive(!isPrisoner);
        foreach (var m in prisonerMeshes) m.SetActive(isPrisoner);
    }
}
