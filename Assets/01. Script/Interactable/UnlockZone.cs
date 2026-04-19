using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum UnlockStageTargetAction
{
    Auto,
    EquipWeapon,
    ActivateSceneObject,
    None
}

[System.Serializable]
public struct UnlockStage
{
    public string stageName;
    public GameObject targetObject;
    public UnlockStageTargetAction targetAction;
    public int cost;
    public Sprite iconSprite;
}

public sealed class UnlockZone : MonoBehaviour
{
    private static readonly Dictionary<int, UnlockZone> ZoneOwnerByPlayer = new Dictionary<int, UnlockZone>();

    [Header("Stages")]
    public List<UnlockStage> stages = new List<UnlockStage>();
    public Image iconImage;

    [Header("UI")]
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image fillImage;

    [Header("Animation")]
    [SerializeField] private float consumeInterval = 0.1f;
    [SerializeField] private float jumpPower = 2f;
    [SerializeField] private Transform cashAbsorbPoint;
    public bool hideOnCompleteWithAnimation = false;

    [Header("Upgrade Settings")]
    [SerializeField] private int capacityIncreasePerStage = 10;
    [SerializeField] private Vector3 miningRangeIncreasePerStage = new Vector3(1f, 0f, 1f);

    public event Action<UnlockZone, int, UnlockStage> StageCompleted;
    public event Action<UnlockZone> AllStagesCompleted;

    private int currentStageIndex = 0;
    private int currentAmount = 0;
    private Coroutine unlockCoroutine;
    private Collider zoneCollider;
    private int activePlayerId = -1;
    private WaitForSeconds consumeWait;

    private UnlockStage CurrentStage => stages[currentStageIndex];

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        consumeWait = new WaitForSeconds(consumeInterval);

        InitializeStageObjects();

        if (fillImage != null) fillImage.fillAmount = 0f;
        UpdateUI();
    }

    private void OnValidate()
    {
        if (consumeInterval < 0f) consumeInterval = 0f;
        if (Application.isPlaying) consumeWait = new WaitForSeconds(consumeInterval);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (HasCurrentStage() == false) return;
        if (!other.CompareTag("Player") || !other.TryGetComponent<StackManager>(out var playerStack)) return;
        if (TryClaimZone(playerStack) == false) return;

        if (unlockCoroutine != null) StopCoroutine(unlockCoroutine);
        unlockCoroutine = StartCoroutine(UnlockRoutine(playerStack));
    }

    private void OnTriggerStay(Collider other)
    {
        if (unlockCoroutine != null || HasCurrentStage() == false) return;
        if (other.CompareTag("Player") == false) return;
        if (other.TryGetComponent<StackManager>(out var playerStack) == false) return;

        if (TryClaimZone(playerStack) == false) return;
        unlockCoroutine = StartCoroutine(UnlockRoutine(playerStack));
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") == false) return;

        if (unlockCoroutine != null)
        {
            StopCoroutine(unlockCoroutine);
            unlockCoroutine = null;
        }

        if (other.TryGetComponent<StackManager>(out var playerStack))
        {
            ReleaseZoneClaim(playerStack);
        }

        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.ResetUnlockFillLoopBlock();
        }
    }

    private void OnDisable()
    {
        if (unlockCoroutine != null)
        {
            StopCoroutine(unlockCoroutine);
            unlockCoroutine = null;
        }

        if (activePlayerId != -1)
        {
            if (ZoneOwnerByPlayer.TryGetValue(activePlayerId, out var owner) && owner == this)
            {
                ZoneOwnerByPlayer.Remove(activePlayerId);
            }

            activePlayerId = -1;
        }

        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.ResetUnlockFillLoopBlock();
        }
    }

    private IEnumerator UnlockRoutine(StackManager playerStack)
    {
        EquipmentManager equipmentManager = playerStack.GetComponent<EquipmentManager>();

        while (HasCurrentStage() == true && CurrentStage.cost <= 0)
        {
            if (HasZoneClaim(playerStack) == false) yield break;
            CompleteCurrentStage(equipmentManager);
            yield return null;
        }

        while (HasCurrentStage() == true && currentAmount < CurrentStage.cost)
        {
            if (HasZoneClaim(playerStack) == false) yield break;

            if (playerStack.HasCash == true)
            {
                GameObject cash = playerStack.PopCash();
                if (cash != null)
                {
                    cash.transform.DOKill();
                    cash.transform.SetParent(null);

                    Vector3 absorbTargetPosition = GetCashAbsorbPosition(playerStack);
                    cash.transform.DOJump(absorbTargetPosition, jumpPower, 1, 0.4f)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() =>
                        {
                            if (PoolManager.Instance != null)
                            {
                                PoolManager.Instance.ReturnToPool("Cash", cash);
                            }
                            else
                            {
                                Destroy(cash);
                            }
                        });

                    if (SFXManager.Instance != null)
                    {
                        SFXManager.Instance.PlayUnlockFill(absorbTargetPosition);
                    }

                    currentAmount++;
                    UpdateProgress();
                    CurrencyManager.Instance.SpendCash(1);

                    if (currentAmount >= CurrentStage.cost)
                    {
                        CompleteCurrentStage(equipmentManager);
                        yield break;
                    }
                }
            }

            yield return consumeWait;
        }
    }

    private void CompleteCurrentStage(EquipmentManager equipmentManager)
    {
        int completedStageIndex = currentStageIndex;
        UnlockStage completedStage = CurrentStage;

        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayUnlockComplete(GetCashAbsorbPosition(null));
        }

        ApplyStageTarget(completedStage, equipmentManager);

        currentStageIndex++;
        unlockCoroutine = null;

        if (HasCurrentStage() == true)
        {
            currentAmount = 0;
            if (fillImage != null) fillImage.fillAmount = 0f;
            UpdateUI();

            transform.DOKill();
            transform.DOPunchScale(Vector3.one * 0.2f, 0.5f);

            StageCompleted?.Invoke(this, completedStageIndex, completedStage);
            Debug.Log($"Unlock Stage Completed: {completedStage.stageName}");
            return;
        }

        if (hideOnCompleteWithAnimation == true)
        {
            if (zoneCollider != null) zoneCollider.enabled = false;
            
            // 전체 오브젝트가 축소될 때까지 시각적 요소를 유지하되, 텍스트가 MAX로 변하지 않도록 빈칸 처리
            if (costText != null) costText.text = "";

            // Canvas가 UnlockZone 외부에 분리되어 있을 가능성을 대비해 직접 찾아서 쪼그라들게 합니다.
            Canvas uiCanvas = costText != null ? costText.GetComponentInParent<Canvas>() : GetComponentInChildren<Canvas>();
            if (uiCanvas != null)
            {
                uiCanvas.transform.DOKill();
                uiCanvas.transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack).OnComplete(() => uiCanvas.gameObject.SetActive(false));
            }

            transform.DOKill();
            transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack).OnComplete(() => gameObject.SetActive(false));
        }
        else
        {
            ShowMaxState();
        }

        if (activePlayerId != -1)
        {
            ReleaseZoneClaimById(activePlayerId);
        }

        StageCompleted?.Invoke(this, completedStageIndex, completedStage);
        AllStagesCompleted?.Invoke(this);
        Debug.Log("All Unlock Stages Completed!");
    }

    private void ApplyStageTarget(UnlockStage stage, EquipmentManager equipmentManager)
    {
        if (stage.targetObject == null || stage.targetAction == UnlockStageTargetAction.None) return;

        UnlockStageTargetAction resolvedAction = ResolveTargetAction(stage);
        if (resolvedAction == UnlockStageTargetAction.EquipWeapon)
        {
            if (equipmentManager != null)
            {
                equipmentManager.EquipWeapon(stage.targetObject);

                if (equipmentManager.TryGetComponent<StackManager>(out var playerStack))
                {
                    playerStack.IncreaseMaxCapacity(capacityIncreasePerStage);
                }

                if (equipmentManager.TryGetComponent<PlayerController>(out var playerController))
                {
                    playerController.IncreaseMiningRange(miningRangeIncreasePerStage);
                }
            }

            return;
        }

        if (resolvedAction == UnlockStageTargetAction.ActivateSceneObject && IsSceneObject(stage.targetObject) == true && stage.targetObject != gameObject)
        {
            stage.targetObject.SetActive(true);
            stage.targetObject.transform.DOKill();
            stage.targetObject.transform.DOPunchScale(Vector3.one * 0.2f, 0.5f);
        }
    }

    private UnlockStageTargetAction ResolveTargetAction(UnlockStage stage)
    {
        if (stage.targetAction != UnlockStageTargetAction.Auto) return stage.targetAction;

        return IsSceneObject(stage.targetObject) == true
            ? UnlockStageTargetAction.ActivateSceneObject
            : UnlockStageTargetAction.EquipWeapon;
    }

    private void UpdateProgress()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = CurrentStage.cost > 0 ? (float)currentAmount / CurrentStage.cost : 1f;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (HasCurrentStage() == false)
        {
            if (costText != null) costText.text = "MAX";
            if (fillImage != null) fillImage.fillAmount = 1f;
            if (iconImage != null) iconImage.enabled = false;
            return;
        }

        if (costText != null)
        {
            int remaining = Mathf.Max(CurrentStage.cost - currentAmount, 0);
            costText.text = remaining.ToString();
        }

        if (iconImage != null)
        {
            iconImage.sprite = CurrentStage.iconSprite;
            iconImage.enabled = CurrentStage.iconSprite != null;
        }
    }

    private void ShowMaxState()
    {
        if (zoneCollider != null) zoneCollider.enabled = false;
        if (fillImage != null) fillImage.fillAmount = 1f;
        if (costText != null) costText.text = "MAX";
        if (iconImage != null) iconImage.enabled = false;

        transform.DOKill();
        transform.DOPunchScale(Vector3.one * 0.2f, 0.5f);
    }

    private void InitializeStageObjects()
    {
        if (stages == null || stages.Count == 0)
        {
            Debug.LogWarning("[UnlockZone] Stages list is empty. This zone will not unlock anything.", this);
            return;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            GameObject stageObject = stages[i].targetObject;
            if (IsSceneObject(stageObject) == false || stageObject == gameObject) continue;
            stageObject.SetActive(false);
        }
    }

    private Vector3 GetCashAbsorbPosition(StackManager playerStack)
    {
        if (cashAbsorbPoint != null)
        {
            return cashAbsorbPoint.position;
        }

        if (zoneCollider != null)
        {
            // Collider center follows the actual trigger location even when root transform is offset.
            return zoneCollider.bounds.center;
        }

        return transform.position;
    }

    private bool HasCurrentStage()
    {
        return stages != null && currentStageIndex >= 0 && currentStageIndex < stages.Count;
    }

    private static bool IsSceneObject(GameObject target)
    {
        return target != null && target.scene.IsValid();
    }

    private bool TryClaimZone(StackManager playerStack)
    {
        int playerId = playerStack.GetInstanceID();
        if (ZoneOwnerByPlayer.TryGetValue(playerId, out var owner) && owner != this)
        {
            return false;
        }

        ZoneOwnerByPlayer[playerId] = this;
        activePlayerId = playerId;
        return true;
    }

    private bool HasZoneClaim(StackManager playerStack)
    {
        int playerId = playerStack.GetInstanceID();
        return ZoneOwnerByPlayer.TryGetValue(playerId, out var owner) && owner == this;
    }

    private void ReleaseZoneClaim(StackManager playerStack)
    {
        ReleaseZoneClaimById(playerStack.GetInstanceID());
    }

    private void ReleaseZoneClaimById(int playerId)
    {
        if (ZoneOwnerByPlayer.TryGetValue(playerId, out var owner) && owner == this)
        {
            ZoneOwnerByPlayer.Remove(playerId);
        }

        if (activePlayerId == playerId)
        {
            activePlayerId = -1;
        }
    }
}
