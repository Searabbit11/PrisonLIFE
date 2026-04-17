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
    private const float FollowSpeed = 15f;
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
    [SerializeField] private float swayMaxOffset = 0.2f;
    [SerializeField] private float swaySpeedForMaxOffset = 4f;
    [SerializeField] private float swayDirectionLerp = 12f;
    [SerializeField] private float swayMoveLerp = 10f;
    [SerializeField] private float swayReturnDuration = 0.2f;

    [Header("Carry Curve Visual")]
    [SerializeField] private float curveBackMaxOffset = 0.18f;
    [SerializeField] private float curveSpeedForMaxOffset = 4f;
    [SerializeField] private float curveVelocityLerp = 10f;

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
    private Vector3 smoothedHorizontalVelocityWorld;

    public void IncreaseMaxCapacity(int amount)
    {
        maxStackCount += amount;
        Debug.Log($"[StackManager] Max Capacity increased to: {maxStackCount}");
    }

    private void Start()
    {
        baseStackPivotLocalPos = stackPivot != null ? stackPivot.localPosition : Vector3.zero;
        baseHandcuffPivotLocalPos = handcuffStackPivot != null ? handcuffStackPivot.localPosition : Vector3.zero;
        baseCashPivotLocalPos = cashStackPivot != null ? cashStackPivot.localPosition : Vector3.zero;
        lastOwnerWorldPos = transform.position;
        ApplyCarrySwayOffset();
    }

    private void OnDisable()
    {
        KillCarrySwayTween();
        currentSwayLocalOffset = Vector3.zero;
        ApplyCarrySwayOffset();
    }

    private void OnDestroy()
    {
        KillCarrySwayTween();
    }

    /// <summary>
    /// 외부에서 호출 시 즉시 등 뒤 스택에 광석을 추가합니다.
    /// </summary>
    public void AddOre(GameObject orePrefab)
    {
        if (IsFull == true)
        {
            ShowMaxEffect();
            return;
        }

        // PoolManager에서 광석을 가져옵니다.
        GameObject ore = PoolManager.Instance.SpawnFromPool("Ore", stackPivot.position, Quaternion.identity);
        if (ore == null) return;

        Transform oreTransform = ore.transform;
        // 생성된 광석의 부모를 플레이어의 스택 피벗으로 설정합니다.
        oreTransform.SetParent(stackPivot);
        
        // 생성 시 위치를 마지막 아이템(또는 피벗) 근처로 설정하여 순간이동 방지
        Vector3 startPos = stackedItems.Count == 0 ? stackPivot.position : stackedItems[^1].position;
        oreTransform.position = startPos + Vector3.up * yOffset;
        
        stackedItems.Add(oreTransform);

        // 뾱(Pop) 연출: 0 -> 1.2 -> 1.0
        oreTransform.localScale = Vector3.zero;
        oreTransform.DOScale(1.2f, 0.15f).SetEase(Ease.OutBack).OnComplete(() =>
        {
            oreTransform.DOScale(1.0f, 0.1f);
        });
    }

    private void Update()
    {
        UpdateCarrySway();
        UpdateStackMovement();
        UpdateHandcuffStackMovement();
        UpdateCashStackMovement();
    }

    public void ShowMaxEffect()
    {
        if (floatingText != null)
        {
            floatingText.Show("MAX");
        }
    }
    
    // 각 광석이 앞선 오브젝트를 따라가며 부드럽게 휘어지도록 계산
    private void UpdateStackMovement()
    {
        int stackCount = stackedItems.Count;
        if (stackCount == 0) return;

        float horizontalSpeed = smoothedHorizontalVelocityWorld.magnitude;
        float speedRatio = Mathf.Clamp01(horizontalSpeed / Mathf.Max(0.01f, curveSpeedForMaxOffset));
        Vector3 backDir = horizontalSpeed > SwayVelocityThreshold ? -smoothedHorizontalVelocityWorld.normalized : Vector3.zero;

        float lerpAmount = Time.deltaTime * FollowSpeed;

        for (int i = 0; i < stackCount; i++)
        {
            Transform current = stackedItems[i];
            Transform target = (i == 0) ? stackPivot : stackedItems[i - 1];

            Vector3 targetPos = target.position + (Vector3.up * yOffset);

            if (stackCount > 2)
            {
                float t = i / (float)(stackCount - 1);
                float centerProfile = 4f * t * (1f - t);
                Vector3 curveOffset = backDir * (curveBackMaxOffset * speedRatio * centerProfile);
                targetPos += curveOffset;
            }

            current.position = Vector3.Lerp(current.position, targetPos, lerpAmount);
            current.rotation = Quaternion.Lerp(current.rotation, target.rotation, lerpAmount);
        }
    }

    private void UpdateHandcuffStackMovement()
    {
        UpdateDynamicStackMovement(handcuffItems, handcuffStackPivot, handcuffYOffset, false);
    }

    private void UpdateCashStackMovement()
    {
        UpdateDynamicStackMovement(cashStack, cashStackPivot, cashYOffset, false);
    }

    private void UpdateDynamicStackMovement(List<GameObject> items, Transform rootPivot, float itemYOffset, bool applyCurve)
    {
        int itemCount = items.Count;
        if (rootPivot == null || itemCount == 0) return;

        float horizontalSpeed = smoothedHorizontalVelocityWorld.magnitude;
        float speedRatio = Mathf.Clamp01(horizontalSpeed / Mathf.Max(0.01f, curveSpeedForMaxOffset));
        Vector3 backDir = horizontalSpeed > SwayVelocityThreshold ? -smoothedHorizontalVelocityWorld.normalized : Vector3.zero;

        float lerpAmount = Time.deltaTime * FollowSpeed;

        for (int i = 0; i < itemCount; i++)
        {
            GameObject currentObject = items[i];
            if (currentObject == null) continue;

            Transform current = currentObject.transform;
            Transform target = (i == 0) ? rootPivot : items[i - 1].transform;

            Vector3 targetPos = target.position + (Vector3.up * itemYOffset);

            if (applyCurve && itemCount > 2)
            {
                float t = i / (float)(itemCount - 1);
                float centerProfile = 4f * t * (1f - t);
                Vector3 curveOffset = backDir * (curveBackMaxOffset * speedRatio * centerProfile);
                targetPos += curveOffset;
            }

            current.position = Vector3.Lerp(current.position, targetPos, lerpAmount);
            current.rotation = Quaternion.Lerp(current.rotation, target.rotation, lerpAmount);
        }
    }

    public GameObject PopItem()
    {
        if (stackedItems.Count == 0) return null;

        Transform lastItem = stackedItems[^1];
        stackedItems.RemoveAt(stackedItems.Count - 1);
        lastItem.DOKill();
        
        // 스택에서 빠져나왔으므로 부모 관계 해제
        lastItem.SetParent(null);
        return lastItem.gameObject;
    }

    #region Handcuff Stack Logic (가슴 앞)
    public void PushHandcuff(GameObject handcuff)
    {
        // 부모 설정을 최우선으로 수행하여 로컬 좌표계 기준점을 잡습니다.
        handcuff.transform.SetParent(handcuffStackPivot);
        
        // 리스트에 추가
        handcuffItems.Add(handcuff);
    }

    public GameObject PopHandcuff()
    {
        if (handcuffItems.Count == 0) return null;

        GameObject last = handcuffItems[^1];
        handcuffItems.RemoveAt(handcuffItems.Count - 1);
        last.transform.DOKill();
        last.transform.SetParent(null);
        return last;
    }

    public Vector3 GetNextHandcuffPosition()
    {
        // X, Z축 오프셋을 제거하고 오직 Y축 스택 위치(로컬 좌표)만 반환
        return new Vector3(0f, (handcuffItems.Count) * handcuffYOffset, 0f);
    }
    #endregion

    #region Cash Stack Logic (등 뒤)
    public void PushCash(GameObject cash)
    {
        // 1. 기존 트윈 종료 및 물리 충돌 방지
        cash.transform.DOKill();
        if (cash.TryGetComponent<Collider>(out var col)) col.enabled = false;

        // 2. 부모 설정 및 리스트 추가
        cash.transform.SetParent(cashStackPivot);
        cashStack.Add(cash);

        // 3. 로컬 목표 위치 계산 (Y축 적재)
        Vector3 targetLocalPos = new Vector3(0, (cashStack.Count - 1) * cashYOffset, 0);

        // 4. 점프 연출로 등 뒤 피벗에 안착
        cash.transform.DOLocalJump(targetLocalPos, 1.5f, 1, 0.4f).SetEase(Ease.OutQuad);
        cash.transform.DOLocalRotate(Vector3.zero, 0.4f);

        // CurrencyManager 연동: 보유 금액 증가
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

        smoothedHorizontalVelocityWorld = Vector3.Lerp(smoothedHorizontalVelocityWorld, velocity, Time.deltaTime * curveVelocityLerp);

        if (isMoving)
        {
            KillCarrySwayTween();

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
            return;
        }

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
}
