using System.Collections;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(StackManager))]
public sealed class DeskNPC : MonoBehaviour
{
    private static readonly int IsWalking = Animator.StringToHash("isWalking");
    private static readonly int Speed = Animator.StringToHash("Speed");

    [Header("References")]
    [SerializeField] private Transform pickupPoint;
    [SerializeField] private Transform dropoffPoint;
    [SerializeField] private DeliveryDesk deliveryDesk;
    [SerializeField] private Animator animator;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float interactDelay = 0.2f;
    [SerializeField] private float waitTimeout = 2.0f; // 가만히 기다리는 구간
    private StackManager stackManager;
    private bool isWorking = true;
    private bool hasIsWalkingBool;
    private bool hasIsWalkingFloat;
    private bool hasSpeedFloat;
    private RuntimeAnimatorController cachedController;
    private WaitForSeconds handcuffCheckWait;
    private WaitForSeconds interactWait;

    private void Awake()
    {
        stackManager = GetComponent<StackManager>();
        CacheAnimatorParams();
        handcuffCheckWait = new WaitForSeconds(0.5f);
        interactWait = new WaitForSeconds(interactDelay);
        // NPC
        stackManager.IncreaseMaxCapacity(10 - stackManager.MaxCarryCount); 
    }

    private void OnValidate()
    {
        if (interactDelay < 0f) interactDelay = 0f;
        if (Application.isPlaying)
        {
            interactWait = new WaitForSeconds(interactDelay);
        }
    }

    private void Start()
    {
        StartCoroutine(WorkRoutine());
    }

    private IEnumerator WorkRoutine()
    {
        while (isWorking)
        {
            yield return WaitUntilHandcuffAvailable();

            yield return MoveToPoint(pickupPoint);
            
            yield return CollectHandcuffs();
            
            if (stackManager.HasHandcuffs)
            {
                yield return MoveToPoint(dropoffPoint);
                
                yield return DropHandcuffs();
            }
        }
    }

    private IEnumerator WaitUntilHandcuffAvailable()
    {
        SetWalkingAnimation(false);
        
        while (Converter.Instance == null || Converter.Instance.HasProducedHandcuffs() == false)
        {
            yield return handcuffCheckWait;
        }
    }

    private IEnumerator MoveToPoint(Transform target)
    {
        if (target == null) yield break;

        float distance = Vector3.Distance(transform.position, target.position);
        float duration = Mathf.Max(distance / moveSpeed, 0.1f);

        SetWalkingAnimation(true);

        transform.DOKill();
        transform.DOLookAt(target.position, 0.2f, AxisConstraint.Y);
        yield return transform.DOMove(target.position, duration).SetEase(Ease.Linear).WaitForCompletion();

        SetWalkingAnimation(false);
    }

    private void CacheAnimatorParams()
    {
        if (animator == null) return;

        hasIsWalkingBool = false;
        hasIsWalkingFloat = false;
        hasSpeedFloat = false;
        cachedController = animator.runtimeAnimatorController;

        foreach (var param in animator.parameters)
        {
            if (param.nameHash == IsWalking && param.type == AnimatorControllerParameterType.Bool)
            {
                hasIsWalkingBool = true;
            }
            else if (param.nameHash == IsWalking && param.type == AnimatorControllerParameterType.Float)
            {
                hasIsWalkingFloat = true;
            }
            else if (param.nameHash == Speed && param.type == AnimatorControllerParameterType.Float)
            {
                hasSpeedFloat = true;
            }
        }
    }

    private void EnsureAnimatorParamCache()
    {
        if (animator == null) return;

        if (cachedController == animator.runtimeAnimatorController &&
            (hasIsWalkingBool || hasIsWalkingFloat || hasSpeedFloat))
        {
            return;
        }

        CacheAnimatorParams();
    }

    private void SetWalkingAnimation(bool walking)
    {
        if (animator == null) return;
        EnsureAnimatorParamCache();

        if (hasIsWalkingBool)
        {
            animator.SetBool(IsWalking, walking);
        }

        if (hasIsWalkingFloat)
        {
            animator.SetFloat(IsWalking, walking ? 1f : 0f);
        }

        if (hasSpeedFloat)
        {
            animator.SetFloat(Speed, walking ? 1f : 0f);
        }
    }

    private IEnumerator CollectHandcuffs()
    {
        float timer = 0f;

        while (stackManager.HandcuffCount < stackManager.MaxCarryCount)
        {
            int previousCount = stackManager.HandcuffCount;

            if (Converter.Instance != null)
            {
                Converter.Instance.ProcessHandcuffOutput(gameObject);
            }

            if (stackManager.HandcuffCount >= stackManager.MaxCarryCount)
            {
                break;
            }
            
            
            if (stackManager.HandcuffCount == previousCount)
            {
                timer += interactDelay;
                
                if (timer >= waitTimeout && stackManager.HasHandcuffs)
                {
                    break;
                }
            }
            else
            {
                timer = 0f; 
            }
            
            yield return interactWait;
        }
    }

    private IEnumerator DropHandcuffs()
    {
        while (stackManager.HasHandcuffs)
        {
            if (deliveryDesk != null)
            {
                deliveryDesk.ProcessNpcHandcuff(stackManager);
            }
            yield return interactWait;
        }
    }
}

