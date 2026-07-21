using Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UI.Office
{
    // 인터페이스를 추가하여 키보드/게임패드 포커스 감지
    public class QuestSlotUI : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public Image background;
        public Color normalColor = Color.white;
        public Color selectColor = Color.gray; // 완료된 퀘스트의 배경색
        public Color activeColor = new Color(0.8f, 0.9f, 1f);

        [Header("Text UI")]
        public TextMeshProUGUI riskText;
        public TextMeshProUGUI questNameText;
        public TextMeshProUGUI rewardText;
        public GameObject activeStamp;
        public GameObject completeStamp;
        private bool isCompleted;
        private bool isActive;
        
        public bool IsCompleted => isCompleted;
        public bool IsActive => isActive;
        
        public void SetColor(Color c)
        {
            riskText.color = c;
            questNameText.color = c;
            rewardText.color = c;
        }

        private void SetUI(string questName, int rank, int fee)
        {
            riskText.text = $"RANK {rank}";
            questNameText.text = questName;
            rewardText.text = fee.ToString();
        }

        public void Setup(QuestData data, bool isCompleted, bool isActive)
        {
            this.isCompleted = isCompleted;
            this.isActive = isActive;

            SetUI(data.QuestName, data.Risk, data.Reward);
            background.color = isCompleted ? selectColor : normalColor;
            
            if (isCompleted) background.color = selectColor;
            else if (isActive) background.color = activeColor;
            else background.color = normalColor;
            
            // 상태에 따른 스탬프 표시
            if (completeStamp != null) completeStamp.SetActive(isCompleted);
            if (activeStamp != null) activeStamp.SetActive(isActive);
        } 

        // 방향키 등으로 이 슬롯이 선택되었을 때
        public void OnSelect(BaseEventData eventData)
        {
            // 선택되었을 때 텍스트 색상을 노란색 등으로 강조
            SetColor(Color.yellow); 
        }

        // 다른 슬롯으로 포커스가 넘어갔을 때
        public void OnDeselect(BaseEventData eventData)
        {
            SetColor(Color.white); // 원상복구
        }
    }
}