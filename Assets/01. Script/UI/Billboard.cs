using UnityEngine;

public sealed class Billboard : MonoBehaviour
{
    private Transform camTransform;

    private void Start()
    {
        if (Camera.main != null) camTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (camTransform == null) return;
        // 텍스트가 카메라의 정면을 바라보게 고정
        transform.LookAt(transform.position + camTransform.forward);
    }
}