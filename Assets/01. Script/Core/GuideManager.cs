using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class GuideManager : MonoBehaviour
{
    public static GuideManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform inputTarget;
    [SerializeField] private Transform outputTarget;

    [SerializeField] private GameObject rock3DArrow;
    [SerializeField] private GameObject input2DGuide;
    [SerializeField] private GameObject input3DArrow;
    [SerializeField] private GameObject output2DGuide;
    [SerializeField] private GameObject output3DArrow;
    [SerializeField] private GameObject desk3DArrow;
    [SerializeField] private GameObject cash3DArrow;

    [Header("2D Guide Follow (World Space)")]
    [SerializeField] private Vector3 guideFollowOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private Vector3 guideRotationOffsetEuler = Vector3.zero;
    
    [Header("2D Guide World Z PingPong")]
    [SerializeField] private bool useWorldZPingPong = true;
    [SerializeField] private float worldZPingPongDistance = 0.25f;
    [SerializeField] private float worldZPingPongDuration = 0.6f;

    private float worldZPingPongOffset;
    private Tween worldZPingPongTween;
    private readonly HashSet<GameObject> everShownGuides = new HashSet<GameObject>();
    private readonly HashSet<GameObject> permanentlyHiddenGuides = new HashSet<GameObject>();

    private void Awake() => Instance = this;

    private void Start()
    {
        CacheInitiallyShownGuides();
        SetGuide(rock3DArrow, true);
        StartWorldZPingPong();
    }

    private void OnDisable()
    {
        StopWorldZPingPong();
    }

    private void OnDestroy()
    {
        StopWorldZPingPong();
    }

    private void LateUpdate()
    {
        Update2DArrowDirection();
    }

    public void OnOreMined() => SetGuide(rock3DArrow, false);

    public void OnOreCountChanged(int count)
    {
        if (count >= 6)
        {
            SetGuide(input2DGuide, true);
            SetGuide(input3DArrow, true);
        }
    }

    public void OnEnterInputZone() => SetGuide(input2DGuide, false);

    public void OnOreInputSuccess()
    {
        SetGuide(input3DArrow, false);
        SetGuide(output2DGuide, true);
        SetGuide(output3DArrow, true);
    }

    public void OnHandcuffAcquired()
    {
        SetGuide(output2DGuide, false);
        SetGuide(output3DArrow, false);
        SetGuide(desk3DArrow, true);
    }

    public void OnEnterDeskZone() => SetGuide(desk3DArrow, false);

    public void OnCashGenerated() => SetGuide(cash3DArrow, true);

    public void OnCashAcquired() => SetGuide(cash3DArrow, false);

    private void SetGuide(GameObject guide, bool active)
    {
        if (guide == null) return;

        if (active)
        {
            if (permanentlyHiddenGuides.Contains(guide)) return;
            guide.SetActive(true);
            everShownGuides.Add(guide);
            return;
        }

        guide.SetActive(false);

        if (everShownGuides.Contains(guide))
        {
            permanentlyHiddenGuides.Add(guide);
        }
    }

    private void CacheInitiallyShownGuides()
    {
        TryCacheShown(rock3DArrow);
        TryCacheShown(input2DGuide);
        TryCacheShown(input3DArrow);
        TryCacheShown(output2DGuide);
        TryCacheShown(output3DArrow);
        TryCacheShown(desk3DArrow);
        TryCacheShown(cash3DArrow);
    }

    private void TryCacheShown(GameObject guide)
    {
        if (guide != null && guide.activeSelf)
        {
            everShownGuides.Add(guide);
        }
    }

    private void Update2DArrowDirection()
    {
        if (playerTransform == null) return;

        GameObject activeGuide = null;
        Transform target = null;

        if (input2DGuide != null && input2DGuide.activeSelf)
        {
            activeGuide = input2DGuide;
            target = inputTarget;
        }
        else if (output2DGuide != null && output2DGuide.activeSelf)
        {
            activeGuide = output2DGuide;
            target = outputTarget;
        }

        if (activeGuide == null || target == null) return;

        // Keep guide at root/world and follow player position with an offset.
        Vector3 guideWorldPos = playerTransform.position + guideFollowOffset;
        guideWorldPos.z += worldZPingPongOffset;
        activeGuide.transform.position = guideWorldPos;

        // Ignore height difference so the guide only rotates on the XZ plane.
        Vector3 targetWorldPos = target.position;
        targetWorldPos.y = guideWorldPos.y;
        Vector3 flatDirection = targetWorldPos - guideWorldPos;
        if (flatDirection.sqrMagnitude < 0.0001f) return;

        Quaternion lookRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        activeGuide.transform.rotation = lookRotation * Quaternion.Euler(guideRotationOffsetEuler);
    }

    private void StartWorldZPingPong()
    {
        StopWorldZPingPong();

        if (!useWorldZPingPong || worldZPingPongDuration <= 0f || worldZPingPongDistance <= 0f)
        {
            worldZPingPongOffset = 0f;
            return;
        }

        worldZPingPongTween = DOVirtual.Float(
                -worldZPingPongDistance,
                worldZPingPongDistance,
                worldZPingPongDuration,
                value => worldZPingPongOffset = value)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void StopWorldZPingPong()
    {
        if (worldZPingPongTween != null && worldZPingPongTween.IsActive())
        {
            worldZPingPongTween.Kill();
        }

        worldZPingPongTween = null;
        worldZPingPongOffset = 0f;
    }
}
