using UnityEngine;
namespace UI.WorldMapScene
{
    public class BouncingAnimation : MonoBehaviour
    {
        [Header("통통 튀는 설정")]
        public float bounceSpeed = 5f;  // 튀는 속도 (클수록 빠름)
        public float bounceHeight = 0.2f; // 튀는 높이 (클수록 높이 뜀)

        private Vector3 initialPosition;

        void Start()
        {
            // 시작할 때의 '자식(Visual)' 기준 위치를 기억.
            initialPosition = transform.localPosition;
        }

        void Update()
        {
            float newY = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;

            // 기억해둔 위치 + 계산된 Y값을 적용.
            transform.localPosition = new Vector3(initialPosition.x, initialPosition.y + newY, initialPosition.z);
        }
    }
}

