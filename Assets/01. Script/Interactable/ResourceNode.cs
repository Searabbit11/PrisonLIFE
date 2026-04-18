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
    private WaitForSeconds respawnWait; // 캐싱용 변수 추가

    public bool IsMineable => isActiveAndEnabled == true && isDestroyed == false;

    private void Awake()
    {
        currentHp = maxHp;
        respawnWait = new WaitForSeconds(respawnTime); // 미리 생성하여 재사용
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
        SetDestroyedVisualState();

        if (playerStack.IsFull == false)
        {
            playerStack.AddOre(orePrefab);
        }

        GuideManager.Instance.OnOreMined();

        StartCoroutine(RespawnRoutine());
    }

    private GameObject DieForNpc()
    {
        SetDestroyedVisualState();

        GameObject droppedOre = null;
        if (orePrefab != null)
        {
            Vector3 dropPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            droppedOre = PoolManager.Instance.SpawnFromPool("Ore", dropPosition, Quaternion.identity);
        }

        StartCoroutine(RespawnRoutine());
        return droppedOre;
    }

    private void SetDestroyedVisualState()
    {
        isDestroyed = true;
        if (nodeCollider != null) nodeCollider.enabled = false;
        if (visualModel != null) visualModel.SetActive(false);
    }

    private IEnumerator RespawnRoutine()
    {
        yield return respawnWait; // 캐싱된 변수 사용
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
