using UnityEngine;

public sealed class CashItem : MonoBehaviour
{
    private bool isCollected = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected == true) return;

        if (other.CompareTag("Player") && other.TryGetComponent<StackManager>(out var playerStack))
        {
            isCollected = true;
            playerStack.PushCash(gameObject);
        }
    }
}
