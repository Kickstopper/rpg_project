using UnityEngine;
namespace UI.WorldMapScene
{
    using UnityEngine;

public class WorldMapMovement : MonoBehaviour
{
    public float moveSpeed = 10f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // 이동 방향 설정
        Vector3 direction = new Vector3(h, 0, v).normalized;

        // 입력이 있을 때만 이동
        if (direction.magnitude > 0)
        {
            // 현재 위치 + (방향 * 속도 * 시간)
            Vector3 newPosition = rb.position + direction * moveSpeed * Time.fixedDeltaTime;
            
            // 물리 엔진을 통해 해당 위치로 이동 (벽이 있으면 막힘)
            rb.MovePosition(newPosition);
        }
    }
}
}
