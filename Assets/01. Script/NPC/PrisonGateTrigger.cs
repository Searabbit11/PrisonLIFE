using UnityEngine;

public sealed class PrisonGateTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 1. 태그 확인 (충돌한 물체 또는 그 부모의 태그 확인)
        bool isNPC = other.CompareTag("NPC") || (other.transform.parent != null && other.transform.parent.CompareTag("NPC"));
        
        if (isNPC)
        {
            // 2. Criminal 컴포넌트를 자신 또는 부모에게서 찾음
            Criminal criminal = other.GetComponent<Criminal>();
            if (criminal == null) criminal = other.GetComponentInParent<Criminal>();

            if (criminal != null)
            {
                criminal.OnPassedGate();
                Debug.Log($"[PrisonGate] Criminal detected: {other.name}");
            }
            else
            {
                Debug.LogWarning($"[PrisonGate] Object with 'NPC' tag detected but no Criminal component found on {other.name}");
            }
        }
    }
}
