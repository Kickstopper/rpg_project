using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data;
using UnityEngine.EventSystems;

namespace UI
{
    public class FieldMapSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI infoText; // 거리 및 소요 시간 표시용
        public Image backgroundImage;
        public Color normalColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        public Color highlightColor = new Color(0.2f, 0.6f, 1f, 0.8f);

        public FieldMapDestData Data { get; private set; }
        private int _index;
        private FieldMapUIManager _manager;

        public void Initialize(int index, FieldMapDestData data, FieldMapUIManager manager)
        {
            _index = index;
            Data = data;
            _manager = manager;

            if (data != null)
            {
                nameText.text = data.displayName;
                infoText.text = $"{data.distance} km / {data.timeHours} hour";
            }
        }

        public void SetFocus(bool isFocused)
        {
            backgroundImage.color = isFocused ? highlightColor : normalColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _manager.OnSlotHovered(_index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _manager.OnSlotClicked(_index);
        }
    }
}