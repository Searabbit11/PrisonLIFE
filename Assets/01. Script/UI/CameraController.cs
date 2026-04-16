using UnityEngine;

public sealed class CameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target; // 플레이어(Player) 할당
    
    [Header("Offset Settings")]
    [SerializeField] private Vector3 offset = new Vector3(-10f, 10f, -10f); 

    public Vector3 GetCameraPositionForTarget(Vector3 targetPosition)
    {
        return targetPosition + offset;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        
        transform.position = GetCameraPositionForTarget(target.position);
    }
}
