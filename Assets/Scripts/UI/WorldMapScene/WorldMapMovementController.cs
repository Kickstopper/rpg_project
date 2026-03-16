using Manager;
using UnityEngine;

namespace UI.WorldMapScene
{
    public class WorldMapMovementController : MonoBehaviour
    {
        public float maxMoveSpeed = 5f;
        
        [Header("조이스틱 연결 (선택)")]
        public VirtualJoystick virtualJoystick;
        
        private Rigidbody rb;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (GameStateManager.Instance && GameStateManager.Instance.CurrentState != GameState.Exploration) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 direction = new Vector3(h, 0, v);

            // 키보드 대각선 이동 시 속도가 빨라지는 것을 막기 위해 길이를 최대 1로 자름
            direction = Vector3.ClampMagnitude(direction, 1f);

            // 조이스틱 입력이 있다면 덮어씌움
            if (virtualJoystick != null && virtualJoystick.InputVector.magnitude > 0)
            {
                direction = new Vector3(virtualJoystick.InputVector.x, 0, virtualJoystick.InputVector.y);
            }

            // 이동 처리
            if (direction.magnitude > 0)
            {
                // direction.magnitude가 0.5라면 속도도 절반
                Vector3 newPosition = rb.position + direction * maxMoveSpeed * Time.fixedDeltaTime;
                rb.MovePosition(newPosition);
            }
            else 
            {
                rb.linearVelocity = Vector3.zero; 
            }
        }
    }
}