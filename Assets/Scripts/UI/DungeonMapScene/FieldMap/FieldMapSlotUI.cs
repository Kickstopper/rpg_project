using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data;

namespace UI
{
    public class FieldMapSlotUI : MonoBehaviour
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
                infoText.text = $"거리: {data.distance}km / 소요: {data.timeHours}시간";
            }
        }

        public void SetFocus(bool isFocused)
        {
            backgroundImage.color = isFocused ? highlightColor : normalColor;
            transform.localScale = isFocused ? Vector3.one * 1.05f : Vector3.one;
        }

        // 마우스 오버 및 클릭 이벤트 연동 (인스펙터의 EventTrigger 활용 권장)
        public void OnPointerEnter() => _manager.OnSlotHovered(_index);
        public void OnPointerClick() => _manager.OnSlotClicked(_index);
    }
}