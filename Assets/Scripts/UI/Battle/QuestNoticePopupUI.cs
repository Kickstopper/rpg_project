using Data;
using TMPro;
using UnityEngine;

namespace UI.Battle
{
    public class QuestNoticePopupUI : MonoBehaviour
    {
        public TextMeshProUGUI questNameText;
        public TextMeshProUGUI riskText;
        public TextMeshProUGUI rewardText;
        public TextMeshProUGUI descriptionText;

        void Awake()
        {
            Close();
        }
        
        public void Open(QuestData data)
        {
            gameObject.SetActive(true);
            
            questNameText.text = data.QuestName;
            riskText.text = $"RANK {data.Risk}";
            rewardText.text = $"Gold: {data.Reward}";
            
            if (descriptionText != null && !string.IsNullOrEmpty(data.Description))
            {
                descriptionText.text = data.Description;
            }
            else if (descriptionText != null)
            {
                descriptionText.text = "의뢰 목표를 달성했습니다!";
            }
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}