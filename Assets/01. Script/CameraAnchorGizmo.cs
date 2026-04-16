using UnityEngine;

public class CameraAnchorGizmo : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        // 카메라의 위치를 표시하는 작은 구체
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        
        // 카메라가 바라보는 방향을 선으로 표시
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
        
        // 가상의 카메라 시야(Frustum) 표시
        Matrix4x4 temp = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawFrustum(Vector3.zero, 60f, 5f, 0.3f, 1.7f);
        Gizmos.matrix = temp;
    }
}