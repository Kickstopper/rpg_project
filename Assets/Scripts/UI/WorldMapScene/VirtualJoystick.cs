using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.WorldMapScene
{
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("조이스틱 UI")]
        public RectTransform joystickBase;
        public RectTransform joystickHandle;

        private Canvas canvas;
        private Camera uiCamera;

        public Vector2 InputVector { get; private set; }

        void Start()
        {
            canvas = GetComponentInParent<Canvas>();
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            
            if (joystickBase != null) joystickBase.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (joystickBase == null) return;

            joystickBase.gameObject.SetActive(true);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, 
                eventData.position, 
                uiCamera, 
                out Vector2 localPoint);

            joystickBase.anchoredPosition = localPoint;
            
            joystickHandle.anchoredPosition = Vector2.zero;
            InputVector = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (joystickBase == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBase, 
                eventData.position, 
                uiCamera, 
                out Vector2 localPoint);

            float radius = joystickBase.sizeDelta.x / 2f;

            // 순수 이동 비율 계산 (원점을 벗어나지 않도록 최대 1로 자름)
            Vector2 rawInput = localPoint / radius;
            if (rawInput.magnitude > 1f)
            {
                rawInput = rawInput.normalized;
            }

            // 조작감 보정 (입력값 제곱)
            float smoothedMagnitude = rawInput.magnitude * rawInput.magnitude;
            
            InputVector = rawInput.normalized * smoothedMagnitude;

            // 눈에 보이는 UI 손잡이는 손가락 위치를 정직하게 따라가도록 rawInput 사용
            joystickHandle.anchoredPosition = rawInput * radius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            InputVector = Vector2.zero;
            if (joystickBase != null) joystickBase.gameObject.SetActive(false);
        }
    }
}