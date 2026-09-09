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
                        targetStr += $"- {entry.name} {(isCompleted ? target.requiredCount : ManagerRoot.Quest.GetKillCount(data.QuestID, target.monsterID))}/{target.requiredCount}마리\n";
                    }
                }
            }
            else
            {
                targetStr += "- 목표 없음\n";
            }
            if (!string.IsNullOrEmpty(data.completionFlag)) targetStr += "- 관련 대화·조사 이벤트를 해결해도 달성 가능\n";
            targetInfoText.text = targetStr;

            if (!string.IsNullOrEmpty(data.Description))
            {
                descriptionText.text = data.Description;
            }
            else
            {
                descriptionText.text = string.Empty;
            }
            
            var state = ManagerRoot.Quest.GetState(data.QuestID);
            switch (state)
            {
                case QuestState.ReadyToReport: statusText.text = "보고 가능 — Office에서 보상 수령"; statusText.color = Color.yellow; break;
                case QuestState.Claimed: statusText.text = "보상 수령 완료"; statusText.color = Color.gray; break;
                case QuestState.Active: statusText.text = "진행 중"; statusText.color = Color.cyan; break;
                case QuestState.Locked: statusText.text = "선행 의뢰 미완료"; statusText.color = Color.gray; break;
                default: statusText.text = data.IsRepeatable ? "접수 가능 (반복 의뢰)" : "접수 가능"; statusText.color = Color.white; break;
            }
        }
    }
}
