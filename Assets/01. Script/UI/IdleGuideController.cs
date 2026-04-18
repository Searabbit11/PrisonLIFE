using UnityEngine;
using UnityEngine.UI;

public sealed class IdleGuideController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject guideRoot;
    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private bool ignoreUIRaycast = true;

    [Header("Idle Settings")]
    [SerializeField] private float idleDelay = 3f;
    [SerializeField] private float joystickDeadZoneSqr = 0.0025f; // 0.05 * 0.05

    [Header("Block Conditions")]
    [SerializeField] private bool hideWhileCameraFocusing = true;
    [SerializeField] private GameObject[] tutorialBlockObjects;

    private float lastInputTime;
    private bool isGuideVisible;

    private void Awake()
    {
        if (joystick == null)
        {
            joystick = FindFirstObjectByType<FloatingJoystick>();
        }
    }

    private void Start()
    {
        ConfigureGuideRaycastBlocking();
        lastInputTime = Time.time;
        SetGuideVisible(false);
    }

    private void Update()
    {
        if (HasAnyInput())
        {
            lastInputTime = Time.time;
            SetGuideVisible(false);
            return;
        }

        if (IsBlockedByCinematicOrTutorial())
        {
            lastInputTime = Time.time;
            SetGuideVisible(false);
            return;
        }

        bool shouldShow = Time.time - lastInputTime >= idleDelay;
        SetGuideVisible(shouldShow);
    }

    private void OnDisable()
    {
        SetGuideVisible(false);
    }

    private bool HasAnyInput()
    {
        if (joystick == null)
        {
            joystick = FindFirstObjectByType<FloatingJoystick>();
        }

        if (joystick != null && joystick.Direction.sqrMagnitude > joystickDeadZoneSqr)
        {
            return true;
        }

        if (Input.touchCount > 0)
        {
            return true;
        }

        return Input.GetMouseButton(0);
    }

    private bool IsBlockedByCinematicOrTutorial()
    {
        if (hideWhileCameraFocusing && CameraController.Instance != null && CameraController.Instance.IsFocusing)
        {
            return true;
        }

        if (tutorialBlockObjects == null) return false;

        for (int i = 0; i < tutorialBlockObjects.Length; i++)
        {
            GameObject blockObj = tutorialBlockObjects[i];
            if (blockObj != null && blockObj.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private void SetGuideVisible(bool visible)
    {
        if (guideRoot == null)
        {
            isGuideVisible = false;
            return;
        }

        if (isGuideVisible == visible) return;

        isGuideVisible = visible;
        guideRoot.SetActive(visible);
    }

    private void ConfigureGuideRaycastBlocking()
    {
        if (!ignoreUIRaycast || guideRoot == null) return;

        CanvasGroup canvasGroup = guideRoot.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        Graphic[] graphics = guideRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }
    }
}
