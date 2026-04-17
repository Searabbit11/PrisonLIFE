using UnityEngine;

public sealed class CashCollectorZone : MonoBehaviour
{
    [SerializeField] private DeliveryDesk deliveryDesk;
    [SerializeField] private float collectInterval = 0.05f;
    private float nextCollectTime;

    private void OnTriggerStay(Collider other)
    {
        if (deliveryDesk == null) return;

        if (other.CompareTag("Player") && Time.time >= nextCollectTime)
        {
            nextCollectTime = Time.time + collectInterval;
            deliveryDesk.CollectCash(other.gameObject);
        }
    }
}
