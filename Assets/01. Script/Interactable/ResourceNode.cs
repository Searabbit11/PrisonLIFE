using System.Collections;
using DG.Tweening;
using UnityEngine;

public sealed class ResourceNode : MonoBehaviour
{
    private static readonly Vector3 PunchAmount = Vector3.one * 0.1f;

    [Header("Health Settings")]
    [SerializeField] private int maxHp = 3;
    [SerializeField] private float respawnTime = 5f;

    [Header("Drops")]
    [SerializeField] private GameObject orePrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Visuals")]
    [SerializeField] private GameObject visualModel;
    [SerializeField] private Collider nodeCollider;

    private int currentHp;
    private bool isDestroyed;
    private WaitForSeconds respawnWait; // 罹먯떛??蹂??異붽?

    public bool IsMineable => isActiveAndEnabled == true && isDestroyed == false;

    private void Awake()
    {
        currentHp = maxHp;
        respawnWait = new WaitForSeconds(respawnTime);
        if (nodeCollider == null) nodeCollider = GetComponent<Collider>();
    }

    public void TakeDamage(int damage, StackManager playerStack)
    {
        if (isDestroyed == true || playerStack == null) return;

        if (playerStack.IsFull == true)
        {
            playerStack.ShowMaxEffect();
        }

        ApplyDamage(damage);

        if (currentHp <= 0)
        {
            DieForPlayer(playerStack);
        }
    }

    public GameObject TakeDamageFromNpc(int damage)
    {
        if (isDestroyed == true) return null;

        ApplyDamage(damage);

        if (currentHp <= 0)
        {
            return DieForNpc();
        }

        return null;
    }

    private void ApplyDamage(int damage)
    {
        currentHp -= damage;

        if (visualModel != null)
        {
            visualModel.transform.DOPunchScale(PunchAmount, 0.2f);
        }
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

    private void SetDestroyedVisualState() { }

    private IEnumerator RespawnRoutine()
    {
        yield return respawnWait; // 罹먯떛??蹂???ъ슜
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
}

