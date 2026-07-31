using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class Pseudo3DRoad : MonoBehaviour
{
    private RawImage roadImage;
    private Material roadMaterial;
    
    [Header("Speed Settings")]
    public float scrollSpeed = 2.0f; // 전진 속도
    public bool isMoving = false;

    private float currentScrollOffset = 0f;

    private void Awake()
    {
        roadImage = GetComponent<RawImage>();
        
        // 런타임 시 머티리얼 인스턴스를 생성하여 다른 UI에 영향을 주지 않도록 함
        roadMaterial = new Material(roadImage.material);
        roadImage.material = roadMaterial;
    }

    private void Update()
    {
        if (!isMoving) return;

        // 시간에 따라 스크롤 오프셋을 감소시켜 앞으로 전진
        currentScrollOffset -= scrollSpeed * Time.deltaTime;
        
        // 텍스처가 무한히 스크롤되므로 소수점 범위(0~1) 내로 유지
        if (currentScrollOffset <= -1f) currentScrollOffset += 1f; 
        
        // 셰이더의 _ScrollOffset 값 갱신
        roadMaterial.SetFloat("_ScrollOffset", currentScrollOffset);
    }
}