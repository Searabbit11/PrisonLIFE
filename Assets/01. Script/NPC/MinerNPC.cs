using UnityEngine;

public sealed class MinerNPC : MonoBehaviour
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsMining = Animator.StringToHash("IsMining");
    private static readonly int UpperIsMining = Animator.StringToHash("ISMining");

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotateSpeed = 10f;
    [SerializeField] private float miningDistance = 1.5f;

    [Header("Mining")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float miningInterval = 1f;
    [SerializeField] private Animator animator;

    private ResourceNode[] laneNodes;
    private Converter targetConverter;
    private int targetIndex;
    private int targetDirection = 1;
    private float nextMiningTime;
    private bool hasLoggedMissingConverter;
    private bool hasIsMiningParam;
    private bool hasUpperIsMiningParam;

    public void Initialize(ResourceNode[] nodes, Converter converter)
    {
        laneNodes = nodes;
        targetIndex = 0;
        targetDirection = 1;
        targetConverter = converter;
        hasLoggedMissingConverter = false;
        CacheAnimatorParams();
    }

    private void Update()
    {
        ResourceNode targetNode = GetTargetNode();
        if (targetNode == null)
        {
            SetAnimation(0f, false);
            return;
        }

        Vector3 targetPosition = targetNode.transform.position;
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > miningDistance * miningDistance)
        {
            MoveToTarget(toTarget);
            SetAnimation(1f, false);
            return;
        }

        SetAnimation(0f, true);
        FaceDirection(toTarget);
    }

    private ResourceNode GetTargetNode()
    {
        if (laneNodes == null || laneNodes.Length == 0) return null;
        targetIndex = Mathf.Clamp(targetIndex, 0, laneNodes.Length - 1);

        for (int i = 0; i < laneNodes.Length; i++)
        {
            ResourceNode node = laneNodes[targetIndex];
            if (node != null && node.IsMineable == true)
            {
                return node;
            }

            MoveIndexPingPong();
        }

        return null;
    }

    private void MoveToTarget(Vector3 toTarget)
    {
        Vector3 direction = toTarget.normalized;
        transform.position += direction * (moveSpeed * Time.deltaTime);
        FaceDirection(direction);
    }

    private void FaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
    }

    public void OnMiningHit()
    {
        ResourceNode targetNode = GetTargetNode();
        if (targetNode == null) return;
        if (Time.time < nextMiningTime) return;

        nextMiningTime = Time.time + miningInterval;

        GameObject droppedOre = targetNode.TakeDamageFromNpc(damage, transform.position);
        if (droppedOre == null) return;

        if (targetConverter == null)
        {
            targetConverter = Converter.Instance; // 싱글톤으로 즉시 할당
            if (targetConverter == null)
            {
                if (hasLoggedMissingConverter == false)
                {
                    Debug.LogWarning("[MinerNPC] Converter reference is missing. NPC ore will stay on the ground.", this);
                    hasLoggedMissingConverter = true;
                }

                return;
            }
        }

        targetConverter.ReceiveNpcOre(droppedOre);
        MoveIndexPingPong();
    }

    private void SetAnimation(float speed, bool isMining)
    {
        if (animator == null) return;

        animator.SetFloat(Speed, speed);
        if (hasIsMiningParam) animator.SetBool(IsMining, isMining);
        if (hasUpperIsMiningParam) animator.SetBool(UpperIsMining, isMining);
    }

    private void CacheAnimatorParams()
    {
        if (animator == null) return;

        hasIsMiningParam = false;
        hasUpperIsMiningParam = false;
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type != AnimatorControllerParameterType.Bool) continue;
            if (parameters[i].nameHash == IsMining) hasIsMiningParam = true;
            if (parameters[i].nameHash == UpperIsMining) hasUpperIsMiningParam = true;
        }
    }

    private void MoveIndexPingPong()
    {
        if (laneNodes == null || laneNodes.Length <= 1) return;

        int next = targetIndex + targetDirection;
        if (next >= laneNodes.Length)
        {
            targetDirection = -1;
            next = laneNodes.Length - 2;
        }
        else if (next < 0)
        {
            targetDirection = 1;
            next = 1;
        }

        targetIndex = Mathf.Clamp(next, 0, laneNodes.Length - 1);
    }
}
