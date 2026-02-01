using UnityEngine;

namespace Controller
{
    public class WorldMapMovementController : MonoBehaviour
    {
        public float moveSpeed = 10f;
        
        // 외부에서 이동 가능 여부를 제어할 변수
        public bool canMove = true; 

        private Rigidbody rb;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            // 이동 불가능 상태라면 코드를 실행하지 않고 리턴
            if (!canMove) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 direction = new Vector3(h, 0, v).normalized;

            if (direction.magnitude > 0)
            {
                Vector3 newPosition = rb.position + direction * moveSpeed * Time.fixedDeltaTime;
                rb.MovePosition(newPosition);
            }
            // 키를 떼면 미끄러지지 않고 즉시 멈추도록 속도 초기화
            else 
            {
                rb.linearVelocity = Vector3.zero; 
            }
        }
    }
}