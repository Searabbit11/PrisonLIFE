using UnityEngine;

public class EndingUIController : MonoBehaviour
{
    [SerializeField] private GameObject endingRoot;
    private bool hasShown;

    private void Start()
    {
        if (endingRoot != null)
        {
            endingRoot.SetActive(false);
        }
    }

    public void ShowEnding()
    {
        if (hasShown) return;
        hasShown = true;

        if (endingRoot != null)
        {
            endingRoot.SetActive(true);
        }
    }
}