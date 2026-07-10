using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Manager;

namespace UI
{
    public class TerminalGridButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        public int GridIndex { get; private set; }
        public TerminalData Data { get; private set; }

        [Header("UI References")]
        public TextMeshProUGUI nameText;
        public Image outlineImage; // 선택 시 켜질 테두리 이미지
        public CanvasGroup canvasGroup; // 페이드 효과용

        private TerminalUIManager _manager;

        public void Initialize(int index, TerminalData data, TerminalUIManager manager)
        {
            GridIndex = index;
            Data = data;
            _manager = manager;

            if (Data != null)
            {
                nameText.text = Data.displayName;
                nameText.color = Color.white;
            }
            else
            {
                // 빈 슬롯 처리
                nameText.text = "EMPTY";
                nameText.color = new Color(1f, 1f, 1f, 0.3f);
            }

            SetHighlight(false);
        }

        public void SetHighlight(bool isHighlighted)
        {
            if (outlineImage != null)
                outlineImage.enabled = isHighlighted;

            // 하이라이트 시 텍스트 색상 변경 (예: 노란색)
            if (Data != null)
                nameText.color = isHighlighted ? Color.gold : Color.white;
        }

        // 마우스를 올렸을 때 하이라이트 이동
        public void OnPointerEnter(PointerEventData eventData)
        {
            _manager.OnButtonHovered(GridIndex);
        }

        // 클릭 및 재차 클릭(더블클릭) 시 확정
        public void OnPointerClick(PointerEventData eventData)
        {
            _manager.OnButtonClicked(GridIndex);
        }
    }
}