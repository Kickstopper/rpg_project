using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
namespace UI
{
    public class ButtonTextColorChanger : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Target Texts")]
        public TextMeshProUGUI text1; // 첫 번째 텍스트
        public TextMeshProUGUI text2; // 두 번째 텍스트

        [Header("Color Settings")]
        public Color normalColor = Color.white;   // 평상시 색상
        public Color selectedColor = Color.gold; // 포커스 됐을 때 색상

        private Button button;

        void Awake()
        {
            button = GetComponent<Button>();
            // 시작 시 비선택 상태 색상으로 초기화
            UpdateColor(false);
        }

        // 키보드나 패드로 포커스가 갔을 때 (Selected)
        public void OnSelect(BaseEventData eventData)
        {
            if (button.interactable)
                UpdateColor(true);
        }

        // 포커스가 빠져나갔을 때 (Deselected)
        public void OnDeselect(BaseEventData eventData)
        {
            if (button.interactable)
                UpdateColor(false);
        }

        // 마우스가 올라갔을 때 (Hover) - 필요 없다면 이 함수 삭제 가능
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button.interactable)
                UpdateColor(true);
        }

        // 마우스가 나갔을 때
        public void OnPointerExit(PointerEventData eventData)
        {
            // 현재 선택된 오브젝트가 이 버튼이 아닐 때만 색상 복구
            if (EventSystem.current.currentSelectedGameObject != gameObject)
            {
                UpdateColor(false);
            }
        }

        // 색상 변경 로직
        private void UpdateColor(bool isSelected)
        {
            Color targetColor = isSelected ? selectedColor : normalColor;

            if (text1 != null) text1.color = targetColor;
            if (text2 != null) text2.color = targetColor;
        }
        
        // 비활성화 될 때 색상 초기화 (옵션)
        void OnDisable()
        {
            UpdateColor(false);
        }
    }
    
}

