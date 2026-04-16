using UnityEngine;

public sealed class DebugResourceGrantHotkeys : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StackManager stackManager;
    [SerializeField] private GameObject orePrefab;
    [SerializeField] private GameObject handcuffPrefab;
    [SerializeField] private GameObject cashPrefab;

    [Header("Hotkeys")]
    [SerializeField] private KeyCode grantAllKey = KeyCode.F8;
    [SerializeField] private KeyCode grantOreKey = KeyCode.F5;
    [SerializeField] private KeyCode grantHandcuffKey = KeyCode.F6;
    [SerializeField] private KeyCode grantCashKey = KeyCode.F7;

    private void Awake()
    {
        if (stackManager == null)
        {
            stackManager = GetComponent<StackManager>();
        }
    }

    private void Update()
    {
        if (stackManager == null) return;

        if (Input.GetKeyDown(grantAllKey))
        {
            GrantAll();
            return;
        }

        if (Input.GetKeyDown(grantOreKey))
        {
            GrantMaxOre();
        }

        if (Input.GetKeyDown(grantHandcuffKey))
        {
            GrantMaxHandcuffs();
        }

        if (Input.GetKeyDown(grantCashKey))
        {
            GrantMaxCash();
        }
    }

    public void GrantMaxOre()
    {
        if (orePrefab == null) return;

        while (stackManager.IsFull == false)
        {
            stackManager.AddOre(orePrefab);
        }
    }

    public void GrantMaxHandcuffs()
    {
        if (handcuffPrefab == null) return;

        int missing = stackManager.MaxCarryCount - stackManager.HandcuffCount;
        for (int i = 0; i < missing; i++)
        {
            GameObject handcuff = Instantiate(handcuffPrefab);
            Vector3 localPos = stackManager.GetNextHandcuffPosition();
            stackManager.PushHandcuff(handcuff);
            handcuff.transform.localPosition = localPos;
            handcuff.transform.localRotation = Quaternion.identity;
        }
    }

    public void GrantMaxCash()
    {
        if (cashPrefab == null) return;

        int missing = stackManager.MaxCarryCount - stackManager.CashCount;
        for (int i = 0; i < missing; i++)
        {
            GameObject cash = Instantiate(cashPrefab);
            stackManager.PushCash(cash);
        }
    }

    public void GrantAll()
    {
        GrantMaxOre();
        GrantMaxHandcuffs();
        GrantMaxCash();
    }
}
