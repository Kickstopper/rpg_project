using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;       
    public float smoothSpeed = 5f; 
    
    private Vector3 offset; // 카메라와 플레이어 사이의 '거리 차이(벡터)'

    void Start()
    {
        if (target != null)
        {
            // 1. 에디터에서 잡아둔 구도(거리 차이)를 계산해둠.
            offset = transform.position - target.position;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 플레이어 위치에 아까 계산해둔 오프셋을 더함.
        Vector3 targetPosition = target.position + offset;
        
        // 부드럽게 이동
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }
}