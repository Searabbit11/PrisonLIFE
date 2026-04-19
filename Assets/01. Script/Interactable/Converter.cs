using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class Converter : MonoBehaviour
{
    public static Converter Instance { get; private set; }
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Process Points")]
    [SerializeField] private Transform rawOreStackPivot;
    [SerializeField] private Transform meltPoint;
    [SerializeField] private Transform beltStartPoint;
    [SerializeField] private Transform pressPoint;
    [SerializeField] private Transform beltEndPoint;
    [SerializeField] private Transform outputStackPoint;

    [Header("Prefabs")]
    [SerializeField] private GameObject ingotPrefab;
    [SerializeField] private GameObject handcuffPrefab;

    [Header("Spawn Rotations")]
    [SerializeField] private Vector3 ingotSpawnRotation = Vector3.zero;
    [SerializeField] private Vector3 handcuffSpawnRotation = Vector3.zero;

    [Header("Visuals")]
    [SerializeField] private Transform customCubePress;
    [SerializeField] private bool playIngotHeatFlash = true;
    [SerializeField] private Color ingotHeatColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private float ingotHeatFlashToHotDuration = 0.1f;
    [SerializeField] private float ingotHeatFlashReturnDuration = 0.22f;
    [SerializeField] private float ingotHeatFlashHoldDuration = 0.03f;

    [Header("Settings")]
    [SerializeField] private float receiveCooldown = 0.1f;
    [SerializeField] private float processCooldown = 1.0f;
    [SerializeField] private float absorbMoveDuration = 0.4f;
    [SerializeField] private float jumpPower = 1.5f;
    [SerializeField] private float jumpDuration = 0.5f;
    [SerializeField] private float moveDuration = 1.0f;

    [Header("Raw Ore Stacking (Input)")]
    [SerializeField] private float rawOreStackZOffset = 0.4f;
    [SerializeField] private float rawOreStackYOffset = 0.15f;
    private readonly List<GameObject> rawOresOnFloor = new List<GameObject>();
    private readonly HashSet<GameObject> rawOresInTransit = new HashSet<GameObject>();

    [Header("Output Stacking")]
    [SerializeField] private float stackYOffset = 0.15f;
    [SerializeField] private float collectInterval = 0.1f;
    private readonly List<GameObject> producedHandcuffs = new List<GameObject>();

    [Header("Capacity Settings")]
    [SerializeField] private int maxRawOreCapacity = 50;
    [SerializeField] private int maxHandcuffCapacity = 50;
    [SerializeField] private FloatingText inputFloatingText;
    [SerializeField] private FloatingText outputFloatingText;

    private float nextCollectTime;
    private float nextReceiveTime;
    private float nextProcessTime;
    private Camera cachedCamera;
    private MaterialPropertyBlock colorPropertyBlock;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        cachedCamera = Camera.main;
        colorPropertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        ProcessRawOresAutomatically();
    }

    public void ProcessOreInput(GameObject player)
    {
        if (Time.time < nextReceiveTime) return;

        if (rawOresOnFloor.Count >= maxRawOreCapacity)
        {
            if (inputFloatingText != null) inputFloatingText.Show("MAX");
            return;
        }

        if (player.TryGetComponent<StackManager>(out var playerStack) == false) return;

        GameObject ore = playerStack.PopItem();
        if (ore == null) return;

        nextReceiveTime = Time.time + receiveCooldown;
        ReceiveOreToFloor(ore);
        GuideManager.Instance.OnOreInputSuccess();
    }

    public bool HasProducedHandcuffs()
    {
        return producedHandcuffs.Count > 0;
    }

    public void ReceiveNpcOre(GameObject ore)
    {
        if (ore == null) return;

        if (rawOresOnFloor.Count >= maxRawOreCapacity)
        {
            if (inputFloatingText != null) inputFloatingText.Show("MAX");
            DiscardOre(ore);
            return;
        }

        ReceiveOreToFloor(ore);
    }

    public void ProcessHandcuffOutput(GameObject player)
    {
        if (Time.time < nextCollectTime || producedHandcuffs.Count == 0) return;
        if (player.TryGetComponent<StackManager>(out var playerStack) == false) return;

        nextCollectTime = Time.time + collectInterval;

        int lastIndex = producedHandcuffs.Count - 1;
        GameObject handcuff = producedHandcuffs[lastIndex];
        producedHandcuffs.RemoveAt(lastIndex);

        Vector3 localTargetPos = playerStack.GetNextHandcuffPosition();
        playerStack.PushHandcuff(handcuff);
        GuideManager.Instance.OnHandcuffAcquired();

        handcuff.transform.DOLocalJump(localTargetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
        handcuff.transform.DOLocalRotate(Vector3.zero, jumpDuration);
    }

    private void DiscardOre(GameObject ore)
    {
        if (ore == null) return;

        ore.transform.DOKill();
        rawOresInTransit.Remove(ore);

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReturnToPool("Ore", ore);
            return;
        }

        Destroy(ore);
    }

    private void ReceiveOreToFloor(GameObject ore)
    {
        ore.transform.DOKill();
        if (ore.TryGetComponent<Collider>(out var col)) col.enabled = false;
        if (ore.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        int index = rawOresOnFloor.Count;
        rawOresOnFloor.Add(ore);
        rawOresInTransit.Add(ore);

        Vector3 targetPos = GetRawOrePosition(index);
        Sequence receiveSeq = DOTween.Sequence();
        receiveSeq.Append(ore.transform.DOJump(targetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad));
        receiveSeq.Join(ore.transform.DORotateQuaternion(rawOreStackPivot.rotation, jumpDuration));
        receiveSeq.OnComplete(() =>
        {
            if (ore == null) return;
            rawOresInTransit.Remove(ore);
        });
    }

    private Vector3 GetRawOrePosition(int index)
    {
        int row = index % 2;
        int layer = index / 2;

        Vector3 targetOffset = new Vector3(
            0f,
            layer * rawOreStackYOffset,
            row == 0 ? -rawOreStackZOffset * 0.5f : rawOreStackZOffset * 0.5f
        );

        return rawOreStackPivot.position + (rawOreStackPivot.rotation * targetOffset);
    }

    private void ProcessRawOresAutomatically()
    {
        if (producedHandcuffs.Count >= maxHandcuffCapacity)
        {
            if (outputFloatingText != null) outputFloatingText.Show("MAX");
            return;
        }

        if (Time.time < nextProcessTime || rawOresOnFloor.Count == 0) return;

        int lastIndex = rawOresOnFloor.Count - 1;
        GameObject oreToProcess = rawOresOnFloor[lastIndex];
        if (oreToProcess == null)
        {
            rawOresOnFloor.RemoveAt(lastIndex);
            return;
        }

        if (rawOresInTransit.Contains(oreToProcess))
        {
            return;
        }

        rawOresOnFloor.RemoveAt(lastIndex);
        rawOresInTransit.Remove(oreToProcess);

        nextProcessTime = Time.time + processCooldown;

        Sequence seq = DOTween.Sequence();
        seq.Append(oreToProcess.transform.DOJump(meltPoint.position, jumpPower, 1, absorbMoveDuration).SetEase(Ease.OutQuad));
        seq.AppendCallback(() =>
        {
            PoolManager.Instance.ReturnToPool("Ore", oreToProcess);
            SpawnAndProcessIngot();
        });
    }

    private void SpawnAndProcessIngot()
    {
        GameObject ingot = PoolManager.Instance.SpawnFromPool("Ingot", beltStartPoint.position, Quaternion.Euler(ingotSpawnRotation));
        if (ingot == null) return;
        PlayIngotHeatFlash(ingot);

        Sequence seq = DOTween.Sequence();
        seq.Append(ingot.transform.DOMove(pressPoint.position, moveDuration).SetEase(Ease.Linear));
        seq.AppendCallback(() =>
        {
            if (customCubePress != null)
            {
                Vector3 originalPressPos = customCubePress.position;
                Vector3 downPos = originalPressPos + Vector3.down * 0.5f;

                Sequence pressSeq = DOTween.Sequence();
                pressSeq.Append(customCubePress.DOMove(downPos, 0.1f).SetEase(Ease.InExpo));
                pressSeq.AppendCallback(() =>
                {
                    if (SFXManager.Instance != null)
                    {
                        SFXManager.Instance.PlayPress(pressPoint != null ? pressPoint.position : transform.position);
                    }

                    PoolManager.Instance.ReturnToPool("Ingot", ingot);

                    GameObject handcuff = PoolManager.Instance.SpawnFromPool("Handcuff", pressPoint.position, Quaternion.Euler(handcuffSpawnRotation));
                    if (handcuff != null)
                    {
                        MoveToOutput(handcuff);
                    }
                });
                pressSeq.Append(customCubePress.DOMove(originalPressPos, 0.1f).SetEase(Ease.OutExpo));
                return;
            }

            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlayPress(pressPoint != null ? pressPoint.position : transform.position);
            }
            PoolManager.Instance.ReturnToPool("Ingot", ingot);
            GameObject handcuffAlt = PoolManager.Instance.SpawnFromPool("Handcuff", pressPoint.position, Quaternion.Euler(handcuffSpawnRotation));
            if (handcuffAlt != null)
            {
                MoveToOutput(handcuffAlt);
            }
        });
    }

    private void MoveToOutput(GameObject handcuff)
    {
        Sequence seq = DOTween.Sequence();
        seq.Append(handcuff.transform.DOMove(beltEndPoint.position, moveDuration).SetEase(Ease.Linear));
        seq.AppendCallback(() =>
        {
            Vector3 targetOffset = new Vector3(0f, producedHandcuffs.Count * stackYOffset, 0f);
            Vector3 finalTargetPos = outputStackPoint.position + (outputStackPoint.rotation * targetOffset);

            handcuff.transform.DOJump(finalTargetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
            handcuff.transform.DORotateQuaternion(outputStackPoint.rotation, jumpDuration);

            producedHandcuffs.Add(handcuff);
        });
    }

    private void PlayIngotHeatFlash(GameObject ingot)
    {
        if (playIngotHeatFlash == false || ingot == null) return;

        Renderer[] renderers = ingot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (TryGetRendererBaseColor(renderer, out int colorPropertyId, out Color originalColor) == false) continue;

            renderer.GetPropertyBlock(colorPropertyBlock);
            colorPropertyBlock.SetColor(colorPropertyId, originalColor);
            renderer.SetPropertyBlock(colorPropertyBlock);

            Sequence flashSeq = DOTween.Sequence().SetTarget(ingot);
            flashSeq.Append(DOTween.To(
                () => 0f,
                t => SetRendererColor(renderer, colorPropertyId, Color.Lerp(originalColor, ingotHeatColor, t)),
                1f,
                Mathf.Max(0.01f, ingotHeatFlashToHotDuration)));
            if (ingotHeatFlashHoldDuration > 0f)
            {
                flashSeq.AppendInterval(ingotHeatFlashHoldDuration);
            }
            flashSeq.Append(DOTween.To(
                () => 0f,
                t => SetRendererColor(renderer, colorPropertyId, Color.Lerp(ingotHeatColor, originalColor, t)),
                1f,
                Mathf.Max(0.01f, ingotHeatFlashReturnDuration)));
            flashSeq.OnComplete(() => SetRendererColor(renderer, colorPropertyId, originalColor));
        }
    }

    private bool TryGetRendererBaseColor(Renderer renderer, out int colorPropertyId, out Color color)
    {
        colorPropertyId = -1;
        color = Color.white;
        if (renderer == null || renderer.sharedMaterial == null) return false;

        Material shared = renderer.sharedMaterial;
        if (shared.HasProperty(BaseColorId))
        {
            colorPropertyId = BaseColorId;
        }
        else if (shared.HasProperty(ColorId))
        {
            colorPropertyId = ColorId;
        }
        else
        {
            return false;
        }

        color = shared.GetColor(colorPropertyId);
        return true;
    }

    private void SetRendererColor(Renderer renderer, int colorPropertyId, Color color)
    {
        if (renderer == null) return;

        renderer.GetPropertyBlock(colorPropertyBlock);
        colorPropertyBlock.SetColor(colorPropertyId, color);
        renderer.SetPropertyBlock(colorPropertyBlock);
    }
}
