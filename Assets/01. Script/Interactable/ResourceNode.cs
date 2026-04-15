using System.Collections;
using DG.Tweening;
using UnityEngine;

public sealed class ResourceNode : MonoBehaviour
{
    private const float JumpPower = 1.5f;
    private const float JumpDuration = 0.5f;
    private const int JumpCount = 1;

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

    private void Awake()
    {
        currentHp = maxHp;
        if (nodeCollider == null) nodeCollider = GetComponent<Collider>();
    }

    /// <summary>
    /// 플레이어의 채굴 애니메이션 타이밍에 맞춰 호출될 데미지 메서드
    /// </summary>
    public void TakeDamage(int damage, StackManager playerStack)
    {
        if (isDestroyed == true || playerStack == null || playerStack.IsFull == true) return;

        currentHp -= damage;

        // 바위 타격 시 살짝 흔들리는 연출
        visualModel.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f);

        if (currentHp <= 0)
        {
            Die(playerStack);
        }
    }

    private void Die(StackManager playerStack)
    {
        isDestroyed = true;
        nodeCollider.enabled = false;
        visualModel.SetActive(false);

        // 플레이어 스택에 즉시 추가
        playerStack.AddOre(orePrefab);

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);

        ResetNode();
    }

    private void ResetNode()
    {
        currentHp = maxHp;
        isDestroyed = false;
        nodeCollider.enabled = true;
        visualModel.SetActive(true);
        
        // 재생성 시 스케일 연출
        visualModel.transform.localScale = Vector3.zero;
        visualModel.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
    }
}
