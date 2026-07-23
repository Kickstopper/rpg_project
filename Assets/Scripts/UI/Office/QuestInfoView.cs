using UnityEngine;
using TMPro;
using Data;
using Manager;

namespace UI.Office
{
    public class QuestInfoView : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI questNameText;
        public TextMeshProUGUI locationText;
        public TextMeshProUGUI rewardText;
        public TextMeshProUGUI targetInfoText;
        public TextMeshProUGUI descriptionText;
        public TextMeshProUGUI statusText;

        // 선택된 퀘스트 데이터를 받아와 UI를 갱신합니다.
        public void UpdateView(QuestData data, bool isCompleted, bool isActive)
        {
            if (data == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            
            // 기본 정보
            questNameText.text = data.QuestName;
            locationText.text = $"장소 : {data.Location} R{data.Risk}";
            rewardText.text = $"보수 : {data.Reward} G";

            // 타겟 정보 문자열 조합
            string targetStr = "[토벌 목표]\n";
            MonsterDatabase.MonsterEntry entry = null;
            if (data.Targets != null && data.Targets.Count > 0)
            {
                foreach (var target in data.Targets)
                {
                    entry = ManagerRoot.Database.monsterDB.GetEntry(target.monsterID);
                    if (entry != null)
                    {
                        targetStr += $"- {entry.name} {target.requiredCount}마리\n";
                    }
                }
            }
            else
            {
                targetStr += "- 목표 없음\n";
            }
            targetInfoText.text = targetStr;

            if (!string.IsNullOrEmpty(data.Description))
            {
                descriptionText.text = data.Description;
            }
            else
            {
                descriptionText.text = string.Empty;
            }
            
            // 상태 표시
            if (isCompleted)
            {
                statusText.text = "완료";
                statusText.color = Color.gold; // 회색
            }
            else if (isActive)
            {
                statusText.text = "진행 중";
                statusText.color = Color.cyan; // 하늘색
            }
            else
            {
                statusText.text = "수주 가능";
                statusText.color = Color.magenta; // 흰색
            }
        }
    }
}