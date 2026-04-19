using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class StackManager : MonoBehaviour
{
    private const float MinDeltaTime = 0.0001f;
    private const float SwayVelocityThreshold = 0.05f;

    [Header("Stack Settings")]
    [SerializeField] private int maxStackCount = 10;
    [SerializeField] private float yOffset = 0.18f;
    [SerializeField] private Transform stackPivot;
    private readonly List<Transform> stackedItems = new List<Transform>();

    [Header("Handcuff Stack Settings")]
    [SerializeField] private Transform handcuffStackPivot;
    [SerializeField] private float handcuffYOffset = 0.1f;
    private readonly List<GameObject> handcuffItems = new List<GameObject>();

    [Header("Cash Stack Settings")]
    [SerializeField] private Transform cashStackPivot;
    [SerializeField] private float cashYOffset = 0.05f;
    private readonly List<GameObject> cashStack = new List<GameObject>();

    [Header("Carry Sway Visual")]
    [SerializeField] private bool useSway = true;
    [SerializeField] private float swayMaxOffset = 0.2f;
    [SerializeField] private float swaySpeedForMaxOffset = 4f;
    [SerializeField] private float swayDirectionLerp = 12f;
    [SerializeField] private float swayMoveLerp = 10f;
    [SerializeField] private float swayReturnDuration = 0.2f;

    [Header("Carry Curve Visual")]
    [SerializeField] private float curveSpeedForMaxOffset = 4f;
    [SerializeField] private float curveVelocityLerp = 10f;
    
    [Header("Handcuff/Cash Rod Bend")]
    [SerializeField] private float rodBendMaxOffset = 0.22f;
    [SerializeField] private float rodBendExponent = 1.8f;
    [SerializeField] private float rodBendVelocityLerp = 14f;
    [SerializeField] private float stackFollowLerp = 15f;

    [Header("Stop Bounce")]
    [SerializeField] private float stopBounceBendAmount = 0.55f;
    [SerializeField] private float stopBounceDuration = 0.35f;
    [SerializeField] private float stopBounceOscillations = 2.2f;
    [SerializeField] private float stopBounceDamping = 3.5f;

    [Header("UI Settings")]
    [SerializeField] private FloatingText floatingText;

    public bool IsFull => stackedItems.Count >= maxStackCount;
    public int MaxCarryCount => maxStackCount;
    public int OreCount => stackedItems.Count;
    public bool HasHandcuffs => handcuffItems.Count > 0;
    public int HandcuffCount => handcuffItems.Count;
    public bool HasCash => cashStack.Count > 0;
    public int CashCount => cashStack.Count;

    private Vector3 baseStackPivotLocalPos;
    private Vector3 baseHandcuffPivotLocalPos;
    private Vector3 baseCashPivotLocalPos;
    private Vector3 currentSwayLocalOffset;
    private Vector3 swayDirectionLocal = Vector3.back;
    private Vector3 lastOwnerWorldPos;
    private Tween swayReturnTween;
    private Tween stopBounceTween;
    private Vector3 smoothedHorizontalVelocityWorld;
    private bool isBasePosInitialized;
    private bool wasMovingLastFrame;
    private Vector3 lastBendDirWorld = Vector3.back;
    private float stopBounceBendOffsetAmount;
    private bool isStopBounceActive;
    private float cachedBaseBendSpeedRatio;

    public void IncreaseMaxCapacity(int amount)
    {
        maxStackCount += amount;
        Debug.Log($"[StackManager] Max Capacity increased to: {maxStackCount}");
    }

    public void SetUseSway(bool use)
    {
        InitializeBasePositions(); 
        useSway = use;
        if (!useSway)
        {
            KillCarrySwayTween();
            currentSwayLocalOffset = Vector3.zero;
            ApplyCarrySwayOffset();
        }
    }

    private void Awake()
    {
        InitializeBasePositions();
    }

    private void InitializeBasePositions()
    {
        if (isBasePosInitialized) return;

        baseStackPivotLocalPos = stackPivot != null ? stackPivot.localPosition : Vector3.zero;
        baseHandcuffPivotLocalPos = handcuffStackPivot != null ? handcuffStackPivot.localPosition : Vector3.zero;
        baseCashPivotLocalPos = cashStackPivot != null ? cashStackPivot.localPosition : Vector3.zero;
        lastOwnerWorldPos = transform.position;
        
        isBasePosInitialized = true;
    }

    private void Start()
    {
        ApplyCarrySwayOffset();
    }

    private void OnDisable()
    {
        KillCarrySwayTween();
        KillStopBounceTween();
        currentSwayLocalOffset = Vector3.zero;
        stopBounceBendOffsetAmount = 0f;
        ApplyCarrySwayOffset();
    }

    private void OnDestroy()
    {
        KillCarrySwayTween();
        KillStopBounceTween();
    }
    
    public void AddOre(GameObject orePrefab)
    {
        if (IsFull == true)
        {
            ShowMaxEffect();
            return;
        }

        
        GameObject ore = PoolManager.Instance.SpawnFromPool("Ore", stackPivot.position, Quaternion.identity);
        if (ore == null) return;

        Transform oreTransform = ore.transform;
        oreTransform.SetParent(stackPivot);
        
        Vector3 startPos = stackedItems.Count == 0 ? stackPivot.position : stackedItems[^1].position;
        oreTransform.position = startPos + Vector3.up * yOffset;
        
        stackedItems.Add(oreTransform);
        GuideManager.Instance.OnOreCountChanged(OreCount);
        
        oreTransform.localScale = Vector3.zero;
        oreTransform.DOScale(1.2f, 0.15f).SetEase(Ease.OutBack).OnComplete(() =>
        {
            oreTransform.DOScale(1.0f, 0.1f);
        });
    }

    private void Update()
    {
        if (useSway) UpdateCarrySway();
        
        float horizontalSpeed = smoothedHorizontalVelocityWorld.magnitude;
        float speedRatio = isStopBounceActive
            ? cachedBaseBendSpeedRatio
            : Mathf.Clamp01(horizontalSpeed / Mathf.Max(0.01f, curveSpeedForMaxOffset));
        Vector3 backDir = horizontalSpeed > SwayVelocityThreshold ? -smoothedHorizontalVelocityWorld.normalized : lastBendDirWorld;
        float lerpAmount = Time.deltaTime * stackFollowLerp;

        UpdateDynamicStackMovement(stackedItems, stackPivot, yOffset, true, backDir, speedRatio, lerpAmount);
        UpdateDynamicStackMovement(handcuffItems, handcuffStackPivot, handcuffYOffset, true, backDir, speedRatio, lerpAmount);
        UpdateDynamicStackMovement(cashStack, cashStackPivot, cashYOffset, true, backDir, speedRatio, lerpAmount);
    }

    private void UpdateDynamicStackMovement(List<Transform> items, Transform rootPivot, float itemYOffset, bool applyCurve, Vector3 backDir, float speedRatio, float lerpAmount)
    {
        int itemCount = items.Count;
        if (rootPivot == null || itemCount == 0) return;

        for (int i = 0; i < itemCount; i++)
        {
            Transform current = items[i];
            if (current == null) continue;

            Transform target = i == 0 ? rootPivot : items[i - 1];
            if (target == null) continue;

            Vector3 targetPos = target.position + (Vector3.up * itemYOffset);

            if (applyCurve && itemCount > 1)
            {
                float t = (i + 1f) / itemCount;
                float bendProfile = Mathf.Pow(t, Mathf.Max(1f, rodBendExponent));
                float baseBend = rodBendMaxOffset * speedRatio * bendProfile;
                float bounceBend = stopBounceBendOffsetAmount * bendProfile;
                targetPos += backDir * (baseBend + bounceBend);
            }

            current.position = Vector3.Lerp(current.position, targetPos, lerpAmount);
            current.rotation = Quaternion.Lerp(current.rotation, target.rotation, lerpAmount);
        }
    }

    private void UpdateDynamicStackMovement(List<GameObject> items, Transform rootPivot, float itemYOffset, bool applyCurve, Vector3 backDir, float speedRatio, float lerpAmount)
    {
        int itemCount = items.Count;
        if (rootPivot == null || itemCount == 0) return;

        for (int i = 0; i < itemCount; i++)
        {
            GameObject currentObj = items[i];
            if (currentObj == null) continue;

            Transform current = currentObj.transform;
            Transform target = i == 0 ? rootPivot : items[i - 1].transform;
            if (target == null) continue;

            Vector3 targetPos = target.position + (Vector3.up * itemYOffset);

            if (applyCurve && itemCount > 1)
            {
                float t = (i + 1f) / itemCount;
                float bendProfile = Mathf.Pow(t, Mathf.Max(1f, rodBendExponent));
                float baseBend = rodBendMaxOffset * speedRatio * bendProfile;
                float bounceBend = stopBounceBendOffsetAmount * bendProfile;
                targetPos += backDir * (baseBend + bounceBend);
            }

            current.position = Vector3.Lerp(current.position, targetPos, lerpAmount);
            current.rotation = Quaternion.Lerp(current.rotation, target.rotation, lerpAmount);
        }
    }

    private void UpdateDynamicStackMovement(List<Component> items, Transform rootPivot, float itemYOffset, bool applyCurve, Vector3 backDir, float speedRatio, float lerpAmount)
    {
        int itemCount = items.Count;
        if (rootPivot == null || itemCount == 0) return;

        for (int i = 0; i < itemCount; i++)
        {
            Component currentComponent = items[i];
            if (currentComponent == null) continue;

            Transform current = currentComponent.transform;
            Component prevComponent = i == 0 ? null : items[i - 1];
            Transform target = i == 0 ? rootPivot : (prevComponent != null ? prevComponent.transform : null);
            if (target == null) continue;

            Vector3 targetPos = target.position + (Vector3.up * itemYOffset);

            if (applyCurve && itemCount > 1)
            {
                float t = (i + 1f) / itemCount;
                float bendProfile = Mathf.Pow(t, Mathf.Max(1f, rodBendExponent));
                float baseBend = rodBendMaxOffset * speedRatio * bendProfile;
                float bounceBend = stopBounceBendOffsetAmount * bendProfile;
                targetPos += backDir * (baseBend + bounceBend);
            }

            current.position = Vector3.Lerp(current.position, targetPos, lerpAmount);
            current.rotation = Quaternion.Lerp(current.rotation, target.rotation, lerpAmount);
        }
    }

    private void UpdateStackMovement() { }
    private void UpdateHandcuffStackMovement() { }
    private void UpdateCashStackMovement() { }
    private void UpdateDynamicStackMovement(List<GameObject> items, Transform rootPivot, float itemYOffset, bool applyCurve) { }

    public void ShowMaxEffect()
    {
        if (floatingText != null)
        {
            floatingText.Show("MAX");
        }
    }

    public GameObject PopItem()
    {
        if (stackedItems.Count == 0) return null;

        Transform lastItem = stackedItems[^1];
        stackedItems.RemoveAt(stackedItems.Count - 1);
        lastItem.DOKill();
        
        lastItem.SetParent(null);
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayItemPlace(transform.position);
        }
        return lastItem.gameObject;
    }

    #region Handcuff Stack Logic (가????
    public void PushHandcuff(GameObject handcuff)
    {
        handcuff.transform.SetParent(handcuffStackPivot);
        
        // 리스?�에 추�?
        handcuffItems.Add(handcuff);
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayItemPick(transform.position);
        }
    }

    public GameObject PopHandcuff()
    {
        if (handcuffItems.Count == 0) return null;

        GameObject last = handcuffItems[^1];
        handcuffItems.RemoveAt(handcuffItems.Count - 1);
        last.transform.DOKill();
        last.transform.SetParent(null);
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayItemPlace(transform.position);
        }
        return last;
    }

    public Vector3 GetNextHandcuffPosition()
    {
        return new Vector3(0f, (handcuffItems.Count) * handcuffYOffset, 0f);
    }
    #endregion

    #region Cash Stack Logic
    public void PushCash(GameObject cash)
    {
        cash.transform.DOKill();
        if (cash.TryGetComponent<Collider>(out var col)) col.enabled = false;
        
        cash.transform.SetParent(cashStackPivot);
        cashStack.Add(cash);
        
        Vector3 targetLocalPos = new Vector3(0, (cashStack.Count - 1) * cashYOffset, 0);
        
        cash.transform.DOLocalJump(targetLocalPos, 1.5f, 1, 0.4f).SetEase(Ease.OutQuad);
        cash.transform.DOLocalRotate(Vector3.zero, 0.4f);
        
        CurrencyManager.Instance.AddCash(1);
    }

    public GameObject PopCash()
    {
        if (cashStack.Count == 0) return null;

        GameObject lastCash = cashStack[^1];
        cashStack.RemoveAt(cashStack.Count - 1);
        lastCash.transform.DOKill();
        lastCash.transform.SetParent(null);
        return lastCash;
    }
    #endregion

    private void UpdateCarrySway()
    {
        Vector3 frameDelta = transform.position - lastOwnerWorldPos;
        lastOwnerWorldPos = transform.position;

        float deltaTime = Mathf.Max(Time.deltaTime, MinDeltaTime);
        Vector3 velocity = frameDelta / deltaTime;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        bool isMoving = speed > SwayVelocityThreshold;

        float velocityLerp = rodBendVelocityLerp > 0f ? rodBendVelocityLerp : curveVelocityLerp;
        smoothedHorizontalVelocityWorld = Vector3.Lerp(smoothedHorizontalVelocityWorld, velocity, Time.deltaTime * velocityLerp);
        if (smoothedHorizontalVelocityWorld.sqrMagnitude > 0.0001f)
        {
            lastBendDirWorld = -smoothedHorizontalVelocityWorld.normalized;
        }

        if (isMoving)
        {
            KillCarrySwayTween();
            KillStopBounceTween();
            stopBounceBendOffsetAmount = 0f;
            isStopBounceActive = false;

            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            localVelocity.y = 0f;
            if (localVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 desiredDirection = (-localVelocity).normalized;
                swayDirectionLocal = Vector3.Slerp(swayDirectionLocal, desiredDirection, Time.deltaTime * swayDirectionLerp);
            }

            float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.01f, swaySpeedForMaxOffset));
            Vector3 targetOffset = swayDirectionLocal * (swayMaxOffset * speedRatio);
            currentSwayLocalOffset = Vector3.Lerp(currentSwayLocalOffset, targetOffset, Time.deltaTime * swayMoveLerp);
            ApplyCarrySwayOffset();
            wasMovingLastFrame = true;
            return;
        }

        if (wasMovingLastFrame && lastBendDirWorld.sqrMagnitude > 0.0001f)
        {
            StartStopBounce();
        }
        wasMovingLastFrame = false;

        if (currentSwayLocalOffset.sqrMagnitude <= 0.000001f)
        {
            currentSwayLocalOffset = Vector3.zero;
            ApplyCarrySwayOffset();
            return;
        }

        if (swayReturnTween != null && swayReturnTween.IsActive())
        {
            return;
        }

        swayReturnTween = DOTween.To(
                () => currentSwayLocalOffset,
                value =>
                {
                    currentSwayLocalOffset = value;
                    ApplyCarrySwayOffset();
                },
                Vector3.zero,
                swayReturnDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnComplete(() => swayReturnTween = null);
    }

    private void StartStopBounce()
    {
        KillStopBounceTween();

        if (stopBounceBendAmount <= 0f || stopBounceDuration <= 0f || stopBounceOscillations <= 0f)
        {
            stopBounceBendOffsetAmount = 0f;
            isStopBounceActive = false;
            return;
        }

        isStopBounceActive = true;
        float horizontalSpeed = smoothedHorizontalVelocityWorld.magnitude;
        cachedBaseBendSpeedRatio = Mathf.Clamp01(horizontalSpeed / Mathf.Max(0.01f, curveSpeedForMaxOffset));
        stopBounceBendOffsetAmount = 0f;

        stopBounceTween = DOVirtual.Float(0f, 1f, stopBounceDuration, t =>
            {
                float oscillation = Mathf.Sin(t * Mathf.PI * 2f * stopBounceOscillations);
                float damping = Mathf.Exp(-Mathf.Max(0f, stopBounceDamping) * t);
                stopBounceBendOffsetAmount = oscillation * damping * stopBounceBendAmount;
            })
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnComplete(() =>
            {
                stopBounceBendOffsetAmount = 0f;
                isStopBounceActive = false;
            });
    }

    private void ApplyCarrySwayOffset()
    {
        Vector3 worldSwayOffset = transform.TransformVector(currentSwayLocalOffset);

        if (stackPivot != null)
        {
            Vector3 localOffset = stackPivot.parent != null
                ? stackPivot.parent.InverseTransformVector(worldSwayOffset)
                : worldSwayOffset;
            stackPivot.localPosition = baseStackPivotLocalPos + localOffset;
        }

        if (handcuffStackPivot != null)
        {
            Vector3 localOffset = handcuffStackPivot.parent != null
                ? handcuffStackPivot.parent.InverseTransformVector(worldSwayOffset)
                : worldSwayOffset;
            handcuffStackPivot.localPosition = baseHandcuffPivotLocalPos + localOffset;
        }

        if (cashStackPivot != null)
        {
            Vector3 localOffset = cashStackPivot.parent != null
                ? cashStackPivot.parent.InverseTransformVector(worldSwayOffset)
                : worldSwayOffset;
            cashStackPivot.localPosition = baseCashPivotLocalPos + localOffset;
        }
    }

    private void KillCarrySwayTween()
    {
        if (swayReturnTween == null) return;
        if (swayReturnTween.IsActive()) swayReturnTween.Kill();
        swayReturnTween = null;
    }

    private void KillStopBounceTween()
    {
        if (stopBounceTween == null) return;
        if (stopBounceTween.IsActive()) stopBounceTween.Kill();
        stopBounceTween = null;
        isStopBounceActive = false;
    }
}


