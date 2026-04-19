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
        TryTriggerTutorial();
    }

    private void TryTriggerTutorial()
    {
        if (hasTutorialPlayed || currentCash <= 0 || firstUnlockZone == null || cameraTargetPoint == null) return;
        
        hasTutorialPlayed = true;
        StartCoroutine(ShowTutorialSequence());
    }

    private IEnumerator ShowTutorialSequence()
    {
        if (mainCam == null)
        {
            if (firstUnlockZone != null) firstUnlockZone.SetActive(true);
            yield break;
        }

        // Disable Player & Camera Follow
        bool wasPlayerEnabled = playerMovement != null && playerMovement.enabled;
        if (playerMovement != null)
        {
            if (playerMovement is PlayerController pc) pc.StopImmediately();
            playerMovement.enabled = false;
        }

        if (cameraFollowController == null) cameraFollowController = mainCam.GetComponent<CameraController>();
        bool wasCamFollowEnabled = cameraFollowController != null && cameraFollowController.enabled;
        if (cameraFollowController != null) cameraFollowController.enabled = false;

        // Sequence
        Transform camTrans = mainCam.transform;
        Vector3 originalPos = camTrans.position;
        Vector3 targetPos = cameraFollowController != null 
            ? cameraFollowController.GetCameraPositionForTarget(cameraTargetPoint.position) 
            : cameraTargetPoint.position;

        camTrans.DOKill();
        yield return camTrans.DOMove(targetPos, 1f).SetEase(Ease.InOutSine).WaitForCompletion();

        firstUnlockZone.SetActive(true);
        firstUnlockZone.transform.localScale = Vector3.zero;
        yield return firstUnlockZone.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).WaitForCompletion();

        yield return new WaitForSeconds(1.5f);

        yield return camTrans.DOMove(originalPos, 1f).SetEase(Ease.InOutSine).WaitForCompletion();

        if (cameraFollowController != null) cameraFollowController.enabled = wasCamFollowEnabled;
        if (playerMovement != null) playerMovement.enabled = wasPlayerEnabled;
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
