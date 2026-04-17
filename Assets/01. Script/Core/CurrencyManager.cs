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
    private Camera mainCam;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        mainCam = Camera.main;
        if (mainCam != null) mainCam.transform.DOKill();
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
        if (mainCam == null)
        {
            Debug.LogWarning("[CurrencyManager] Main Camera was not found. Skipping tutorial camera sequence.");
            if (firstUnlockZone != null) firstUnlockZone.SetActive(true);
            yield break;
        }

        bool wasPlayerMovementEnabled = playerMovement != null && playerMovement.enabled;
        if (playerMovement != null) playerMovement.enabled = false;

        if (cameraFollowController == null)
        {
            cameraFollowController = mainCam.GetComponent<CameraController>();
        }

        bool wasCameraFollowEnabled = cameraFollowController != null && cameraFollowController.enabled;
        if (cameraFollowController != null) cameraFollowController.enabled = false;

        Transform camTransform = mainCam.transform;
        Vector3 originalPos = camTransform.position;
        Vector3 targetCameraPosition = cameraFollowController != null
            ? cameraFollowController.GetCameraPositionForTarget(cameraTargetPoint.position)
            : cameraTargetPoint.position;

        camTransform.DOKill();
        yield return camTransform.DOMove(targetCameraPosition, 1f).SetEase(Ease.InOutSine).WaitForCompletion();

        if (firstUnlockZone != null)
        {
            firstUnlockZone.SetActive(true);
            firstUnlockZone.transform.localScale = Vector3.zero;
            firstUnlockZone.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        }

        yield return new WaitForSeconds(1.5f);

        camTransform.DOKill();
        yield return camTransform.DOMove(originalPos, 1f).SetEase(Ease.InOutSine).WaitForCompletion();

        if (cameraFollowController != null) cameraFollowController.enabled = wasCameraFollowEnabled;
        if (playerMovement != null) playerMovement.enabled = wasPlayerMovementEnabled;
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
