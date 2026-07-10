using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Manager;
using Data;
namespace UI
{
    public enum ActionType { 
        Attack, 
        Shoot, Reload, 
        Skill, 
        Item, Move, Guard, Next,
        Menu_Gun,     // Gun ▶ (Shoot, Reload)
        Menu_Extra,   // Extra ▶ (Item, Move, Guard, Next)
        Menu_Tactics,  // Tactics ▶ (Union, Last_Stand)
        Union_Attack, Last_Stand, Rolling_Vulcan, 
        Penetration, Power_Charge, Anima, 
        Burner, Freezer, Stunner, Pulser,
        Fight, Talk, Escape, Auto   // 메인 커맨드
    }
    public class CommandButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Target Texts")]
        public TextMeshProUGUI text1; // 첫 번째 텍스트
        public TextMeshProUGUI text2; // 두 번째 텍스트

        [Header("Color Settings")]
        public Color normalColor = Color.white;   // 평상시 색상
        public Color selectedColor = Color.gold; // 포커스 됐을 때 색상
        
        public ActionType type; // 인스펙터에서 설정
        public Button button;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
        }

        // 키보드나 패드로 포커스가 갔을 때
        public void OnSelect(BaseEventData eventData)
        {
            if (!button.interactable) return;
            
            UpdateColor(true);
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
        }

        // 포커스가 빠져나갔을 때
        public void OnDeselect(BaseEventData eventData)
        {
            if (!button.interactable) return;
            UpdateColor(false);
        }

        // 마우스가 올라갔을 때
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!button.interactable) return;
            
            UpdateColor(true);
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
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
        
        // 비활성화 될 때 색상 초기화
        void OnDisable()
        {
            UpdateColor(false);
        }
    }
    
}
