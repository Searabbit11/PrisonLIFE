using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;

public sealed class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI cashUIText;

    [Header("Tutorial Settings")]
    public GameObject firstUnlockZone;
    public Transform cameraTargetPoint;
    public MonoBehaviour playerMovement;
    [SerializeField] private CameraController cameraFollowController;

    private int currentCash = 0;
    private bool hasTutorialPlayed = false; // 튜토리얼 중복 실행 방지

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Clear any stale camera tween before the tutorial sequence starts.
        if (Camera.main != null) Camera.main.transform.DOKill();
    }

    private void Start()
    {
        UpdateUI();
        if (firstUnlockZone != null) firstUnlockZone.SetActive(false);
    }

    public void AddCash(int amount)
    {
        currentCash += amount;
        UpdateUI();

        if (hasTutorialPlayed == false && currentCash > 0)
        {
            hasTutorialPlayed = true;
            if (firstUnlockZone != null && cameraTargetPoint != null)
            {
                StartCoroutine(ShowTutorialSequence());
            }
        }
    }

    private IEnumerator ShowTutorialSequence()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[CurrencyManager] Main Camera was not found. Skipping tutorial camera sequence.");
            firstUnlockZone.SetActive(true);
            yield break;
        }

        // The normal follow controller writes the camera position in LateUpdate.
        // Disable it while DOTween owns the tutorial camera move.
        bool wasPlayerMovementEnabled = playerMovement != null && playerMovement.enabled;
        if (playerMovement != null) playerMovement.enabled = false;

        if (cameraFollowController == null)
        {
            cameraFollowController = mainCamera.GetComponent<CameraController>();
        }

        bool wasCameraFollowEnabled = cameraFollowController != null && cameraFollowController.enabled;
        if (cameraFollowController != null) cameraFollowController.enabled = false;

        Transform camTransform = mainCamera.transform;
        Vector3 originalPos = camTransform.position;
        Vector3 targetCameraPosition = cameraFollowController != null
            ? cameraFollowController.GetCameraPositionForTarget(cameraTargetPoint.position)
            : cameraTargetPoint.position;

        camTransform.DOKill();
        Sequence moveToZone = DOTween.Sequence();
        moveToZone.Join(camTransform.DOMove(targetCameraPosition, 1f).SetEase(Ease.InOutSine));
        yield return moveToZone.WaitForCompletion();

        firstUnlockZone.SetActive(true);
        firstUnlockZone.transform.localScale = Vector3.zero;
        firstUnlockZone.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

        yield return new WaitForSeconds(1.5f);

        camTransform.DOKill();
        Sequence moveBack = DOTween.Sequence();
        moveBack.Join(camTransform.DOMove(originalPos, 1f).SetEase(Ease.InOutSine));
        yield return moveBack.WaitForCompletion();

        if (cameraFollowController != null) cameraFollowController.enabled = wasCameraFollowEnabled;
        if (playerMovement != null) playerMovement.enabled = wasPlayerMovementEnabled;
        
        Debug.Log("[CurrencyManager] Tutorial Sequence Finished.");
    }

    public void SpendCash(int amount)
    {
        currentCash -= amount;
        if (currentCash < 0) currentCash = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (cashUIText != null) cashUIText.text = currentCash.ToString();
    }
}
