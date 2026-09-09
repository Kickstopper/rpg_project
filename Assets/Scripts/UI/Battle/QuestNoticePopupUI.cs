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

        public void Open(QuestData data)
        {
            gameObject.SetActive(true);
            
            questNameText.text = data.QuestName;
            riskText.text = $"RANK {data.Risk}";
            rewardText.text = $"Gold: {data.Reward}";
            
            if (descriptionText != null) descriptionText.text = "목표 달성 — Office에서 보고하고 보상을 받으세요.\n[확인] 다음";
        }

        public void Close() { gameObject.SetActive(false); }
    }
}
