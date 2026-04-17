using System.Collections.Generic;
using UnityEngine;

public sealed class EquipmentManager : MonoBehaviour
{
    private enum EquippedType
    {
        None,
        Pickaxe,
        Drill1,
        Drill2,
    }

    [Header("Sockets")]
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private Transform drill1Socket;
    [SerializeField] private Transform drill2Socket;

    [Header("Offsets")]
    [SerializeField] private Vector3 defaultPickaxePos = Vector3.zero;
    [SerializeField] private Vector3 defaultPickaxeRot = Vector3.zero;
    [SerializeField] private Vector3 defaultDrill1Pos = Vector3.zero;
    [SerializeField] private Vector3 defaultDrill1Rot = Vector3.zero;
    [SerializeField] private Vector3 defaultDrill2Pos = Vector3.zero;
    [SerializeField] private Vector3 defaultDrill2Rot = Vector3.zero;

    [Header("Default Equipment")]
    [SerializeField] private GameObject defaultPickaxePrefab;

    private readonly Dictionary<GameObject, GameObject> equippedCache = new Dictionary<GameObject, GameObject>();

    private GameObject currentEquippedWeapon;
    private PlayerController playerController;
    private EquippedType currentType;

    public bool IsDrillEquipped => currentType == EquippedType.Drill1 || currentType == EquippedType.Drill2;
    public bool IsDrill1Equipped => currentType == EquippedType.Drill1;
    public bool IsDrill2Equipped => currentType == EquippedType.Drill2;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        InitializeEquipment();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("MiningZone"))
        {
            SetWeaponVisibility(true);
            playerController.IsInMiningZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("MiningZone"))
        {
            SetWeaponVisibility(false);
            playerController.IsInMiningZone = false;
        }
    }

    private void InitializeEquipment()
    {
        if (defaultPickaxePrefab == null || weaponSocket == null)
        {
            Debug.LogWarning("Equipment Manager: Prefab or Socket is missing.");
            return;
        }

        EquipWeapon(defaultPickaxePrefab);
    }

    public void EquipWeapon(GameObject weaponPrefab)
    {
        if (weaponPrefab == null)
        {
            Debug.LogWarning("Equipment Manager: Tried to equip a null weapon prefab.");
            return;
        }

        if (currentEquippedWeapon != null)
        {
            currentEquippedWeapon.SetActive(false);
        }

        ResolveSocketAndOffset(
            weaponPrefab,
            out Transform targetSocket,
            out Vector3 targetPos,
            out Vector3 targetRot,
            out EquippedType nextType);

        if (targetSocket == null)
        {
            Debug.LogWarning("Equipment Manager: Target socket is missing.");
            return;
        }

        if (!equippedCache.TryGetValue(weaponPrefab, out GameObject weaponInstance) || weaponInstance == null)
        {
            weaponInstance = Instantiate(weaponPrefab, targetSocket);
            equippedCache[weaponPrefab] = weaponInstance;
        }
        else
        {
            weaponInstance.transform.SetParent(targetSocket, false);
        }

        Transform weaponTransform = weaponInstance.transform;
        weaponTransform.localPosition = targetPos;
        weaponTransform.localRotation = Quaternion.Euler(targetRot);

        currentEquippedWeapon = weaponInstance;
        currentType = nextType;

        SetWeaponVisibility(playerController != null && playerController.IsInMiningZone);
        Debug.Log($"Equipped New Weapon: {weaponPrefab.name} on {targetSocket.name}");
    }

    public void SetWeaponVisibility(bool visible)
    {
        if (currentEquippedWeapon != null && currentEquippedWeapon.activeSelf != visible)
        {
            currentEquippedWeapon.SetActive(visible);
        }
    }

    private void ResolveSocketAndOffset(
        GameObject weaponPrefab,
        out Transform targetSocket,
        out Vector3 targetPos,
        out Vector3 targetRot,
        out EquippedType type)
    {
        string prefabName = weaponPrefab.name;

        if (prefabName.Contains("Drill 1"))
        {
            targetSocket = drill1Socket != null ? drill1Socket : weaponSocket;
            targetPos = defaultDrill1Pos;
            targetRot = defaultDrill1Rot;
            type = EquippedType.Drill1;
        }
        else if (prefabName.Contains("Drill 2"))
        {
            targetSocket = drill2Socket != null ? drill2Socket : weaponSocket;
            targetPos = defaultDrill2Pos;
            targetRot = defaultDrill2Rot;
            type = EquippedType.Drill2;
        }
        else
        {
            targetSocket = weaponSocket;
            targetPos = defaultPickaxePos;
            targetRot = defaultPickaxeRot;
            type = EquippedType.Pickaxe;
        }
    }
}
