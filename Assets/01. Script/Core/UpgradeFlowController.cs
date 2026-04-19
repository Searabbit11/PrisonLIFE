using DG.Tweening;
using UnityEngine;

public sealed class UpgradeFlowController : MonoBehaviour
{
    [Header("Upgrade Zones")]
    [SerializeField] private UnlockZone drillUpgradeZone;
    [SerializeField] private UnlockZone minerNpcUpgradeZone;
    [SerializeField] private GameObject deskNpcUnlockZone;

    [Header("Prison Upgrade")]
    [SerializeField] private UnlockZone prisonUpgradeZone;
    [SerializeField] private GameObject prisonZone1;
    [SerializeField] private GameObject prisonZone2;
    [SerializeField] private Transform[] prisonZone2Slots;
    [SerializeField] private int prisonZone2Capacity = 40;
    [SerializeField] private Transform prisonZone2FocusPoint;
    [SerializeField] private float prisonZone2CameraZoom = 1.5f;

    [Header("NPC Spawning")]
    [SerializeField] private MinerNPCSpawner minerNpcSpawner;

    [Header("Flow Timing")]
    [SerializeField] private float drillNextStageConsumeDelay = 1f;
    [SerializeField] private float minerNpcUnlockDelay = 1.5f;
    [SerializeField] private float deskNpcUnlockDelay = 2f;

    [Header("Startup State")]
    [SerializeField] private bool hideMinerNpcZoneOnStart = true;
    [SerializeField] private bool hideDeskNpcZoneOnStart = true;

    [Header("Completion State")]
    [SerializeField] private bool hideDrillZoneWhenComplete = true;
    [SerializeField] private bool hideMinerNpcZoneWhenComplete = true;

    [Header("Ending")]
    [SerializeField] private EndingUIController endingUIController;
    private bool hasStartedEndingSequence;

    private void OnEnable()
    {
        if (drillUpgradeZone != null)
        {
            drillUpgradeZone.StageCompleted += HandleDrillStageCompleted;
            drillUpgradeZone.AllStagesCompleted += HandleDrillAllStagesCompleted;
        }

        if (minerNpcUpgradeZone != null)
        {
            minerNpcUpgradeZone.AllStagesCompleted += HandleMinerNpcAllStagesCompleted;
        }

        if (prisonUpgradeZone != null)
        {
            prisonUpgradeZone.AllStagesCompleted += HandlePrisonAllStagesCompleted;
        }
    }

    private void Start()
    {
        if (hideMinerNpcZoneOnStart == true && minerNpcUpgradeZone != null)
        {
            minerNpcUpgradeZone.gameObject.SetActive(false);
        }

        if (hideDeskNpcZoneOnStart == true && deskNpcUnlockZone != null)
        {
            deskNpcUnlockZone.SetActive(false);
        }

        if (prisonUpgradeZone != null)
        {
            prisonUpgradeZone.hideOnCompleteWithAnimation = true;
        }
    }

    private void OnDisable()
    {
        if (drillUpgradeZone != null)
        {
            drillUpgradeZone.StageCompleted -= HandleDrillStageCompleted;
            drillUpgradeZone.AllStagesCompleted -= HandleDrillAllStagesCompleted;
        }

        if (minerNpcUpgradeZone != null)
        {
            minerNpcUpgradeZone.AllStagesCompleted -= HandleMinerNpcAllStagesCompleted;
        }

        if (prisonUpgradeZone != null)
        {
            prisonUpgradeZone.AllStagesCompleted -= HandlePrisonAllStagesCompleted;
        }
    }

    private void HandleDrillStageCompleted(UnlockZone zone, int stageIndex, UnlockStage stage)
    {
        if (stageIndex == 0 && minerNpcUpgradeZone != null)
        {
            zone.BlockCashConsume(drillNextStageConsumeDelay);
            DOVirtual.DelayedCall(minerNpcUnlockDelay, () => ActivateZone(minerNpcUpgradeZone.gameObject));
        }
    }

    private void HandleDrillAllStagesCompleted(UnlockZone zone)
    {
        if (hideDrillZoneWhenComplete == true && drillUpgradeZone != null)
        {
            drillUpgradeZone.gameObject.SetActive(false);
        }
    }

    private void HandleMinerNpcAllStagesCompleted(UnlockZone zone)
    {
        if (minerNpcSpawner != null)
        {
            minerNpcSpawner.SpawnMiners();
        }

        if (hideMinerNpcZoneWhenComplete == true && minerNpcUpgradeZone != null)
        {
            minerNpcUpgradeZone.gameObject.SetActive(false);
        }

        if (deskNpcUnlockZone != null)
        {
            DOVirtual.DelayedCall(deskNpcUnlockDelay, () => ActivateZone(deskNpcUnlockZone));
        }
    }

    private static void ActivateZone(GameObject zone)
    {
        if (zone == null) return;

        zone.SetActive(true);
        zone.transform.DOKill();
        zone.transform.DOPunchScale(Vector3.one * 0.2f, 0.5f);
    }

    private void HandlePrisonAllStagesCompleted(UnlockZone zone)
    {
        // UnlockZone 자체에서 0.4초 동안 DOScale로 사라지는 연출을 진행하므로,
        // 그 타이밍에 맞춰서 감옥 업그레이드 연출을 실행합니다.
        DOVirtual.DelayedCall(0.4f, () => 
        {
            ApplyPrisonUpgrade();
        });
    }

    private void ApplyPrisonUpgrade()
    {
        if (prisonZone1 != null) prisonZone1.SetActive(false);
        if (prisonZone2 != null) prisonZone2.SetActive(true);

        if (CriminalManager.Instance != null)
        {
            CriminalManager.Instance.UpgradeCapacity(prisonZone2Capacity, prisonZone2Slots);
        }

        bool startedCameraFocus = false;
        if (prisonZone2FocusPoint != null && CameraController.Instance != null)
        {
            CameraController.Instance.FocusTarget(prisonZone2FocusPoint.position, 1.5f, 2.0f, prisonZone2CameraZoom);
            startedCameraFocus = true;
        }

        if (endingUIController != null && hasStartedEndingSequence == false)
        {
            hasStartedEndingSequence = true;
            StartCoroutine(WaitCameraAndShowEnding(startedCameraFocus));
        }
    }

    private System.Collections.IEnumerator WaitCameraAndShowEnding(bool waitForCameraFocus)
    {
        if (waitForCameraFocus && CameraController.Instance != null)
        {
            yield return new WaitWhile(() => CameraController.Instance != null && CameraController.Instance.IsFocusing);
        }

        endingUIController.ShowEnding();
    }
}
