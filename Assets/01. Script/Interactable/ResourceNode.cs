using System.Collections;
using DG.Tweening;
using UnityEngine;

public sealed class ResourceNode : MonoBehaviour
{
    private static readonly Vector3 PunchAmount = Vector3.one * 0.1f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Health Settings")]
    [SerializeField] private int maxHp = 3;
    [SerializeField] private float respawnTime = 5f;

    [Header("Drops")]
    [SerializeField] private GameObject orePrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Visuals")]
    [SerializeField] private GameObject visualModel;
    [SerializeField] private Collider nodeCollider;

    [Header("Hit VFX")]
    [SerializeField] private bool playHitVfx = true;
    [SerializeField] private bool usePoolForHitVfx = true;
    [SerializeField] private string hitVfxPoolTag = "OreHitVFX";
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private float hitVfxPoolReturnDelay = 1f;
    [SerializeField] private float hitVfxPositionOffset = 0.03f;
    [SerializeField] private int fallbackBurstCount = 12;
    [SerializeField] private float fallbackLifetime = 0.6f;

    private int currentHp;
    private bool isDestroyed;
    private WaitForSeconds respawnWait;
    private Color cachedOreColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    public bool IsMineable => isActiveAndEnabled == true && isDestroyed == false;

    private void Awake()
    {
        currentHp = maxHp;
        respawnWait = new WaitForSeconds(respawnTime);
        if (nodeCollider == null) nodeCollider = GetComponent<Collider>();
        CacheOreColor();
    }

    public void TakeDamage(int damage, StackManager playerStack)
    {
        if (isDestroyed == true || playerStack == null) return;

        if (playerStack.IsFull == true)
        {
            playerStack.ShowMaxEffect();
        }

        ApplyDamage(damage, playerStack.transform.position);

        if (currentHp <= 0)
        {
            DieForPlayer(playerStack);
        }
    }

    public GameObject TakeDamageFromNpc(int damage)
    {
        return TakeDamageFromNpc(damage, transform.position);
    }

    public GameObject TakeDamageFromNpc(int damage, Vector3 hitOrigin)
    {
        if (isDestroyed == true) return null;

        ApplyDamage(damage, hitOrigin);

        if (currentHp <= 0)
        {
            return DieForNpc();
        }

        return null;
    }

    private void ApplyDamage(int damage, Vector3 hitOrigin)
    {
        currentHp -= damage;

        if (visualModel != null)
        {
            visualModel.transform.DOPunchScale(PunchAmount, 0.2f);
        }

        PlayHitVfx(hitOrigin);
    }

    private void DieForPlayer(StackManager playerStack)
    {
        HandleDeath();
        if (playerStack.IsFull == false) playerStack.AddOre(orePrefab);
        GuideManager.Instance.OnOreMined();
    }

    private GameObject DieForNpc()
    {
        HandleDeath();
        if (orePrefab == null) return null;
        Vector3 dropPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        return PoolManager.Instance.SpawnFromPool("Ore", dropPosition, Quaternion.identity);
    }

    private void HandleDeath()
    {
        isDestroyed = true;
        if (nodeCollider != null) nodeCollider.enabled = false;
        if (visualModel != null) visualModel.SetActive(false);
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return respawnWait;
        ResetNode();
    }

    private void ResetNode()
    {
        currentHp = maxHp;
        isDestroyed = false;

        if (nodeCollider != null) nodeCollider.enabled = true;
        if (visualModel == null) return;

        visualModel.SetActive(true);
        visualModel.transform.localScale = Vector3.zero;
        visualModel.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
    }

    private void CacheOreColor()
    {
        Renderer targetRenderer = null;
        if (visualModel != null)
        {
            targetRenderer = visualModel.GetComponentInChildren<Renderer>();
        }

        if (targetRenderer == null || targetRenderer.sharedMaterial == null) return;

        Material mat = targetRenderer.sharedMaterial;
        if (mat.HasProperty(BaseColorId)) cachedOreColor = mat.GetColor(BaseColorId);
        else if (mat.HasProperty(ColorId)) cachedOreColor = mat.GetColor(ColorId);
    }

    private void PlayHitVfx(Vector3 hitOrigin)
    {
        if (playHitVfx == false) return;

        Vector3 impactPos = transform.position;
        if (nodeCollider != null)
        {
            impactPos = nodeCollider.ClosestPoint(hitOrigin);
        }

        Vector3 hitDir = impactPos - hitOrigin;
        if (hitDir.sqrMagnitude < 0.0001f) hitDir = transform.position - hitOrigin;
        if (hitDir.sqrMagnitude < 0.0001f) hitDir = transform.up;

        Vector3 normal = hitDir.normalized;
        Vector3 spawnPos = impactPos + (normal * hitVfxPositionOffset);
        Quaternion spawnRot = Quaternion.LookRotation(normal);

        if (TrySpawnFromPool(spawnPos, spawnRot)) return;
        if (TrySpawnFromPrefab(spawnPos, spawnRot)) return;
        SpawnFallbackParticle(spawnPos, spawnRot);
    }

    private bool TrySpawnFromPool(Vector3 position, Quaternion rotation)
    {
        if (usePoolForHitVfx == false || string.IsNullOrEmpty(hitVfxPoolTag)) return false;
        if (PoolManager.Instance == null) return false;

        GameObject pooled = PoolManager.Instance.SpawnFromPool(hitVfxPoolTag, position, rotation);
        if (pooled == null) return false;

        PlayParticleRecursively(pooled);
        StartCoroutine(ReturnHitVfxToPool(hitVfxPoolTag, pooled, Mathf.Max(0.05f, hitVfxPoolReturnDelay)));
        return true;
    }

    private bool TrySpawnFromPrefab(Vector3 position, Quaternion rotation)
    {
        if (hitVfxPrefab == null) return false;

        GameObject spawned = Instantiate(hitVfxPrefab, position, rotation);
        PlayParticleRecursively(spawned);
        Destroy(spawned, Mathf.Max(0.1f, fallbackLifetime));
        return true;
    }

    private static void PlayParticleRecursively(GameObject target)
    {
        if (target == null) return;
        ParticleSystem[] particles = target.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }
    }

    private IEnumerator ReturnHitVfxToPool(string poolTag, GameObject target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target == null || PoolManager.Instance == null) yield break;
        PoolManager.Instance.ReturnToPool(poolTag, target);
    }

    private void SpawnFallbackParticle(Vector3 position, Quaternion rotation)
    {
        GameObject go = new GameObject("OreHitVFX_Fallback");
        go.transform.SetPositionAndRotation(position, rotation);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.25f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.maxParticles = 64;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1f;
        main.startColor = cachedOreColor;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(fallbackBurstCount, 1, 64)) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 32f;
        shape.radius = 0.02f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(cachedOreColor, 0f), new GradientColorKey(cachedOreColor * 0.85f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play(true);
        Destroy(go, Mathf.Max(0.2f, fallbackLifetime));
    }
}
