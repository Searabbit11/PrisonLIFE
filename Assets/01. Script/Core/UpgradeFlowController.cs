using DG.Tweening;
using UnityEngine;

public sealed class UpgradeFlowController : MonoBehaviour
{
    [Header("Upgrade Zones")]
    [SerializeField] private UnlockZone drillUpgradeZone;
    [SerializeField] private UnlockZone minerNpcUpgradeZone;
    [SerializeField] private GameObject deskNpcUnlockZone;

    [Header("NPC Spawning")]
    [SerializeField] private MinerNPCSpawner minerNpcSpawner;

    [Header("Flow Timing")]
    [SerializeField] private float deskNpcUnlockDelay = 2f;

    [Header("Startup State")]
    [SerializeField] private bool hideMinerNpcZoneOnStart = true;
    [SerializeField] private bool hideDeskNpcZoneOnStart = true;

    [Header("Completion State")]
    [SerializeField] private bool hideDrillZoneWhenComplete = true;
    [SerializeField] private bool hideMinerNpcZoneWhenComplete = true;

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
    }

    private void HandleDrillStageCompleted(UnlockZone zone, int stageIndex, UnlockStage stage)
    {
        if (stageIndex == 0 && minerNpcUpgradeZone != null)
        {
            ActivateZone(minerNpcUpgradeZone.gameObject);
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
}
