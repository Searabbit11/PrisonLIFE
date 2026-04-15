using UnityEngine;

public class ConveyorAnimation : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 0.8f;
    private Material beltMaterial;
    private Vector2 offset;

    void Start()
    {
        // 렌더러에서 재질을 가져옵니다.
        beltMaterial = GetComponent<Renderer>().material;
    }

    void Update()
    {
        // 텍스처의 Y축 오프셋을 시간에 따라 변경합니다. 
        // 벨트 방향에 따라 (offset.x, 0) 또는 (0, offset.y)로 수정 가능합니다.
        float move = Time.time * scrollSpeed;
        beltMaterial.mainTextureOffset = new Vector2(0, move);
    }
}