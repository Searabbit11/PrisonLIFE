using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class DeliveryDesk : MonoBehaviour
{
    [Header("Stack Settings")]
    [SerializeField] private Transform deskStackPivot;
    [SerializeField] private float stackYOffset = 0.15f;
    [SerializeField] private float collectInterval = 0.1f;
    [SerializeField] private float deliveryInterval = 0.2f;

    [Header("Reward Settings")]
    [SerializeField] private Transform cashSpawnPoint;
    [SerializeField] private GameObject cashPrefab;
    [SerializeField] private float cashJumpPower = 2f;
    [SerializeField] private float cashWidthOffset = 0.4f;  // X축 (가로 2열)
    [SerializeField] private float cashDepthOffset = 0.6f;  // Z축 (세로 3행)
    [SerializeField] private float cashHeightOffset = 0.1f; // Y축 (층간 높이)

    [Header("Visual Feedback")]
    [SerializeField] private float jumpPower = 1.5f;
    [SerializeField] private float jumpDuration = 0.4f;

    [Header("Capacity Settings")]
    [SerializeField] private int maxHandcuffCapacity = 50;
    [SerializeField] private int maxCashCapacity = 100;
    [SerializeField] private FloatingText floatingText;
    
    [Header("Zone Visual")]
    [SerializeField] private Renderer zoneRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor = Color.green;

    private readonly List<GameObject> deskHandcuffs = new List<GameObject>();
    private readonly List<GameObject> spawnedCashList = new List<GameObject>(); 
    private readonly HashSet<int> playerColliderIds = new HashSet<int>();
    private MaterialPropertyBlock zonePropertyBlock;
    private int colorPropertyId = -1;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private Coroutine collectCoroutine;
    private float nextDeliveryTime;
    private int totalSpawnedCashCount = 0; 
    private WaitForSeconds collectWait;

    private void Awake()
    {
        collectWait = new WaitForSeconds(collectInterval);

        if (zoneRenderer == null)
        {
            zoneRenderer = GetComponent<Renderer>();
        }

        if (zoneRenderer == null) return;

        zonePropertyBlock = new MaterialPropertyBlock();
        CacheColorProperty();
        SetZoneActiveVisual(false);
    }

    private void OnValidate()
    {
        if (collectInterval < 0f) collectInterval = 0f;
        if (Application.isPlaying) collectWait = new WaitForSeconds(collectInterval);
    }

    public void CollectCash(GameObject player)
    {
        if (spawnedCashList.Count == 0) return;
        if (player.TryGetComponent<StackManager>(out var stackManager))
        {
            int lastIndex = spawnedCashList.Count - 1;
            GameObject cash = spawnedCashList[lastIndex];
            spawnedCashList.RemoveAt(lastIndex);

            if (cash != null)
            {
                stackManager.PushCash(cash);
                totalSpawnedCashCount = Mathf.Max(0, totalSpawnedCashCount - 1);
                GuideManager.Instance.OnCashAcquired();
            }
        }
    }

    private void Update()
    {
        HandleHandcuffDelivery();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        bool isFirstPlayerCollider = playerColliderIds.Add(other.GetInstanceID()) && playerColliderIds.Count == 1;
        if (isFirstPlayerCollider)
        {
            SetZoneActiveVisual(true);
            GuideManager.Instance.OnEnterDeskZone();
        }

        if (collectCoroutine == null && other.TryGetComponent<StackManager>(out var stackManager))
        {
            collectCoroutine = StartCoroutine(CollectHandcuffsRoutine(stackManager));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (playerColliderIds.Remove(other.GetInstanceID()) && playerColliderIds.Count == 0)
        {
            if (collectCoroutine != null)
            {
                StopCoroutine(collectCoroutine);
                collectCoroutine = null;
            }

            SetZoneActiveVisual(false);
        }
    }

    private void SetZoneActiveVisual(bool isActive)
    {
        if (zoneRenderer == null || zonePropertyBlock == null || colorPropertyId == -1) return;

        zoneRenderer.GetPropertyBlock(zonePropertyBlock);
        zonePropertyBlock.SetColor(colorPropertyId, isActive ? activeColor : normalColor);
        zoneRenderer.SetPropertyBlock(zonePropertyBlock);
    }

    private void CacheColorProperty()
    {
        var sharedMat = zoneRenderer.sharedMaterial;
        if (sharedMat == null) return;

        if (sharedMat.HasProperty(BaseColorId))
        {
            colorPropertyId = BaseColorId;
        }
        else if (sharedMat.HasProperty(ColorId))
        {
            colorPropertyId = ColorId;
        }

        if (colorPropertyId != -1)
        {
            normalColor = sharedMat.GetColor(colorPropertyId);
        }
    }
    
    private IEnumerator CollectHandcuffsRoutine(StackManager playerStack)
    {
        while (true)
        {
            if (playerStack.HasHandcuffs == true)
            {
                // 데스크 수갑 적재 한도 체크
                if (deskHandcuffs.Count >= maxHandcuffCapacity)
                {
                    if (floatingText != null) floatingText.Show("MAX");
                    yield return collectWait;
                    continue;
                }

                GameObject handcuff = playerStack.PopHandcuff();
                if (handcuff != null)
                {
                    int currentIndex = deskHandcuffs.Count;
                    deskHandcuffs.Add(handcuff);

                    Vector3 targetLocalPos = new Vector3(0, currentIndex * stackYOffset, 0);
                    
                    handcuff.transform.SetParent(deskStackPivot);
                    handcuff.transform.DOLocalJump(targetLocalPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
                    handcuff.transform.DOLocalRotate(Vector3.zero, jumpDuration);
                }
            }
            yield return collectWait;
        }
    }

    public void ProcessNpcHandcuff(StackManager npcStack)
    {
        if (npcStack.HasHandcuffs == true && deskHandcuffs.Count < maxHandcuffCapacity)
        {
            GameObject handcuff = npcStack.PopHandcuff();
            if (handcuff != null)
            {
                int currentIndex = deskHandcuffs.Count;
                deskHandcuffs.Add(handcuff);

                Vector3 targetLocalPos = new Vector3(0, currentIndex * stackYOffset, 0);
                
                handcuff.transform.SetParent(deskStackPivot);
                handcuff.transform.DOLocalJump(targetLocalPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
                handcuff.transform.DOLocalRotate(Vector3.zero, jumpDuration);
            }
        }
    }

    private void HandleHandcuffDelivery()
    {
        if (Time.time < nextDeliveryTime || deskHandcuffs.Count == 0) return;

        var targetCriminal = CriminalManager.Instance.GetFirstCriminalInQueue();
        
        if (targetCriminal != null && targetCriminal.CanReceiveHandcuffs == true)
        {
            nextDeliveryTime = Time.time + deliveryInterval;

            int lastIndex = deskHandcuffs.Count - 1;
            GameObject handcuff = deskHandcuffs[lastIndex];
            deskHandcuffs.RemoveAt(lastIndex);

            handcuff.transform.DOKill();

            Vector3 stackLocalPos = new Vector3(0, deskHandcuffs.Count * stackYOffset, 0);
            handcuff.transform.position = deskStackPivot.TransformPoint(stackLocalPos);

            handcuff.transform.SetParent(null);

            handcuff.transform.DOJump(targetCriminal.GetDeliverPivot().position, jumpPower, 1, jumpDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    PoolManager.Instance.ReturnToPool("Handcuff", handcuff);
                    targetCriminal.ReceiveHandcuff();
                });
        }
    }

    public void SpawnCash(int initialRequiredAmount, Transform spawnOrigin)
    {
        if (cashSpawnPoint == null || spawnOrigin == null) return;

        int amountToSpawn = initialRequiredAmount * 2;

        for (int i = 0; i < amountToSpawn; i++)
        {
            // 현금 적재 한도 체크
            if (spawnedCashList.Count >= maxCashCapacity)
            {
                if (floatingText != null) floatingText.Show("MAX");
                break;
            }

            GameObject cash = PoolManager.Instance.SpawnFromPool("Cash", spawnOrigin.position, Quaternion.identity);
            if (cash == null) continue;

            spawnedCashList.Add(cash); 
            GuideManager.Instance.OnCashGenerated();
            
            int layer = totalSpawnedCashCount / 6;
            int indexInLayer = totalSpawnedCashCount % 6;

            int xPos = indexInLayer % 2; 
            int zPos = indexInLayer / 2; 

            Vector3 targetOffset = new Vector3(
                xPos * cashWidthOffset, 
                layer * cashHeightOffset, 
                zPos * cashDepthOffset
            );
            
            Vector3 targetPos = cashSpawnPoint.position + (cashSpawnPoint.rotation * targetOffset);

            cash.transform.DOJump(targetPos, cashJumpPower, 1, 0.5f + Random.Range(0f, 0.2f))
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    cash.transform.DOPunchScale(new Vector3(0.3f, 0.3f, 0.3f), 0.2f);
                });
            
            cash.transform.DORotateQuaternion(cashSpawnPoint.rotation, 0.5f);
            totalSpawnedCashCount++;
        }
    }
}
