using Manager;
using UnityEngine;

namespace Controller
{
    public class WorldMapMovementController : MonoBehaviour
    {
        public float moveSpeed = 10f;
        
        private Rigidbody rb;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (GameStateManager.Instance.CurrentState != GameState.Exploration) return;

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