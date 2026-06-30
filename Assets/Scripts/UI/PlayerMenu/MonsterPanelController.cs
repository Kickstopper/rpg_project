using Data;
using Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UI.PlayerMenu
{
    public class MonsterPanelController : MonoBehaviour
    {
        [Header("UI Reference")]
        public Button selectButton;
        public Image backgroundImage; // 패널의 배경 이미지 컴포넌트 할당 필요
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI levelText;
        public Slider hpSlider;
        public Slider mpSlider;

        [Header("Highlight Colors")]
        public Color normalColor = new Color(0.1f, 0.1f, 0.1f, 1f);  // 기본 색상
        public Color focusedColor = new Color(0.8f, 0.8f, 0, 1f);    // 방향키/마우스가 올려졌을 때 (노란색 톤)
        public Color selectedColor = new Color(0, 0.8f, 0, 1f);      // 합체 재료로 확정 선택되었을 때 (녹색 톤)

        public string currentMonsterID { get; private set; } // 외부에서 ID를 참조하기 위해 추가

        public void Initialize(string monsterID)
        {
            currentMonsterID = monsterID;
            RuntimeCharacterData monster = PartyManager.Instance.GetCharacterByID(monsterID);
            
            if (monster == null)
            {
                nameText.text = "";
                levelText.text = "";
                hpSlider.value = 0;
                mpSlider.value = 0;
                return;
            }

            nameText.text = monster.name;
            levelText.text = $"LV.{monster.stats.level}";
            hpSlider.value = (float)monster.currentHp / monster.maxHp;
            mpSlider.value = (float)monster.currentMp / monster.maxMp;

            SetVisualState(false, false);
        }

        // 상태에 따라 배경색을 변경하는 함수
        public void SetVisualState(bool isFocused, bool isSelected)
        {
            if (backgroundImage != null)
            {
                if (isSelected) backgroundImage.color = selectedColor;
                else if (isFocused) backgroundImage.color = focusedColor;
                else backgroundImage.color = normalColor;
            }
        }
    }
}