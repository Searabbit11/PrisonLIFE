using UnityEngine;

public sealed class EquipmentManager : MonoBehaviour
{
    // TODO: 에디터에서 맞춘 정확한 로컬 좌표값을 아래 상수에 기입하세요.
    private static readonly Vector3 DefaultPickaxePos = new Vector3(0f, 0f, 0f);
    private static readonly Vector3 DefaultPickaxeRot = new Vector3(0f, 0f, 0f);

    [Header("Sockets")]
    [SerializeField] private Transform weaponSocket;

    [Header("Default Equipment")]
    [SerializeField] private GameObject defaultPickaxePrefab;

    private GameObject currentEquippedWeapon;
    private PlayerController playerController;

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
        // 기존 장비가 있다면 제거
        if (currentEquippedWeapon != null)
        {
            Destroy(currentEquippedWeapon);
        }

        // 프리팹 생성 및 소켓 자식으로 설정
        currentEquippedWeapon = Instantiate(weaponPrefab, weaponSocket);
        
        // 로컬 좌표 및 회전값 초기화 (에디터 설정값 강제 적용)
        Transform weaponTransform = currentEquippedWeapon.transform;
        weaponTransform.localPosition = DefaultPickaxePos;
        weaponTransform.localRotation = Quaternion.Euler(DefaultPickaxeRot);
        
        SetWeaponVisibility(false);

        Debug.Log($"Equipped New Weapon: {weaponPrefab.name}");
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
