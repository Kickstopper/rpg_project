using UnityEngine;

public class WorldMapCameraFollow : MonoBehaviour
{
    public GameObject mainCamera;
    public Transform player;       
    public float smoothSpeed = 5f; 
    
    [Header("카메라 고정 각도/거리")]
    public Vector3 offset = new Vector3(0f, 10f, -7f); 

    void Start()
    {
        if (player != null)
        {
            // 전투 또는 메뉴에서 돌아오자마자 Lerp를 무시하고 플레이어의 현재 위치로 즉시 이동
            SnapToTarget();
        }
    }

    void LateUpdate()
    {
        if (player == null) return;

        Vector3 targetPosition = player.position + offset;
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }

    public void SnapToTarget()
    {
        if (player != null)
            mainCamera.transform.position = player.position + offset;
    }
}