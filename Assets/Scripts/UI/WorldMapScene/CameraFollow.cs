using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;       
    public float smoothSpeed = 5f; 
    
    [Header("카메라 고정 각도/거리")]
    public Vector3 offset = new Vector3(0f, 10f, -7f); 

    void Start()
    {
        if (target != null)
        {
            // 전투 또는 메뉴에서 돌아오자마자 Lerp를 무시하고 플레이어의 현재 위치로 즉시 이동
            SnapToTarget();
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }

    public void SnapToTarget()
    {
        if (target != null)
            transform.position = target.position + offset;
    }
}