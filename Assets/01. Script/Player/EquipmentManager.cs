using UnityEngine;

public sealed class EquipmentManager : MonoBehaviour
{
    [Header("Sockets")]
    [SerializeField] private Transform weaponSocket; // 곡괭이 소켓
    [SerializeField] private Transform drill1Socket; // 드릴 1 전용 소켓
    [SerializeField] private Transform drill2Socket; // 드릴 2 전용 소켓

    [Header("Offsets")]
    [SerializeField] private Vector3 defaultPickaxePos = Vector3.zero;
    [SerializeField] private Vector3 defaultPickaxeRot = Vector3.zero;
    [SerializeField] private Vector3 defaultDrill1Pos = Vector3.zero;
    [SerializeField] private Vector3 defaultDrill1Rot = Vector3.zero;
    [SerializeField] private Vector3 defaultDrill2Pos = Vector3.zero;
    [SerializeField] private Vector3 defaultDrill2Rot = Vector3.zero;

    [Header("Default Equipment")]
    [SerializeField] private GameObject defaultPickaxePrefab;

    private GameObject currentEquippedWeapon;
    private PlayerController playerController;

    public bool IsDrillEquipped => currentEquippedWeapon != null && currentEquippedWeapon.name.Contains("Drill");
    public bool IsDrill1Equipped => currentEquippedWeapon != null && currentEquippedWeapon.name.Contains("Drill 1");
    public bool IsDrill2Equipped => currentEquippedWeapon != null && currentEquippedWeapon.name.Contains("Drill 2");

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

    /// <summary>
    /// 게임 시작 시 기본 장비를 장착합니다.
    /// </summary>
    private void InitializeEquipment()
    {
        if (defaultPickaxePrefab == null || weaponSocket == null)
        {
            Debug.LogWarning("Equipment Manager: Prefab or Socket is missing.");
            return;
        }

        EquipWeapon(defaultPickaxePrefab);
    }

    /// <summary>
    /// 새로운 장비를 소켓에 생성하고 정렬합니다. (업그레이드 시 호출 가능)
    /// </summary>
    public void EquipWeapon(GameObject weaponPrefab)
    {
        if (currentEquippedWeapon != null)
        {
            Destroy(currentEquippedWeapon);
        }

        Transform targetSocket = weaponSocket;
        Vector3 targetPos = defaultPickaxePos;
        Vector3 targetRot = defaultPickaxeRot;

        // 프리팹 이름에 따라 대상 소켓과 오프셋을 분기
        if (weaponPrefab.name.Contains("Drill 1"))
        {
            targetSocket = drill1Socket != null ? drill1Socket : weaponSocket;
            targetPos = defaultDrill1Pos;
            targetRot = defaultDrill1Rot;
        }
        else if (weaponPrefab.name.Contains("Drill 2"))
        {
            targetSocket = drill2Socket != null ? drill2Socket : weaponSocket;
            targetPos = defaultDrill2Pos;
            targetRot = defaultDrill2Rot;
        }

        // 프리팹 생성 및 지정된 소켓의 자식으로 설정
        currentEquippedWeapon = Instantiate(weaponPrefab, targetSocket);
        
        // 로컬 좌표 및 회전값 적용
        Transform weaponTransform = currentEquippedWeapon.transform;
        weaponTransform.localPosition = targetPos;
        weaponTransform.localRotation = Quaternion.Euler(targetRot);
        
        SetWeaponVisibility(false);

        Debug.Log($"Equipped New Weapon: {weaponPrefab.name} on {targetSocket.name}");
    }

    /// <summary>
    /// 장비의 활성화/비활성화 상태를 제어합니다.
    /// </summary>
    public void SetWeaponVisibility(bool visible)
    {
        if (currentEquippedWeapon != null && currentEquippedWeapon.activeSelf != visible)
        {
            currentEquippedWeapon.SetActive(visible);
        }
    }
}
