using UnityEngine;

public class GuideManager : MonoBehaviour
{
    public static GuideManager Instance { get; private set; }

    [SerializeField] private GameObject rock3DArrow;
    [SerializeField] private GameObject input2DGuide;
    [SerializeField] private GameObject input3DArrow;
    [SerializeField] private GameObject output2DGuide;
    [SerializeField] private GameObject output3DArrow;
    [SerializeField] private GameObject desk3DArrow;
    [SerializeField] private GameObject cash3DArrow;

    private void Awake() => Instance = this;

    private void Start() => SetGuide(rock3DArrow, true);

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

    public void OnHandcuffAcquired() => SetGuide(desk3DArrow, true);

    public void OnCashGenerated() => SetGuide(cash3DArrow, true);

    public void OnCashAcquired() => SetGuide(cash3DArrow, false);

    private void SetGuide(GameObject guide, bool active)
    {
        if (guide != null) guide.SetActive(active);
    }
}