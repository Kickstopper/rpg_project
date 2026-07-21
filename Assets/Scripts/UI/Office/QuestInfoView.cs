using UnityEngine;
using TMPro;
using Data;

namespace UI.Office
{
    public class QuestInfoView : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI questNameText;
        public TextMeshProUGUI locationText;
        public TextMeshProUGUI rewardText;
        public TextMeshProUGUI targetInfoText;
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
            locationText.text = $"장소 : {data.Location} (LV.{data.Risk})";
            rewardText.text = $"보수 : {data.Reward} G";

            // 타겟 정보 문자열 조합
            string targetStr = "<b>[토벌 목표]</b>\n";
            if (data.Targets != null && data.Targets.Count > 0)
            {
                foreach (var target in data.Targets)
                {
                    targetStr += $"- {target.monsterID} {target.requiredCount}마리\n";
                }
            }
            else
            {
                targetStr += "- 목표 없음\n";
            }
            targetInfoText.text = targetStr;

            // 상태 표시
            if (isCompleted)
            {
                statusText.text = "<color=#808080>완료된 의뢰</color>"; // 회색
            }
            else if (isActive)
            {
                statusText.text = "<color=#00BFFF>진행 중인 의뢰</color>"; // 하늘색
            }
            else
            {
                statusText.text = "<color=#FFFFFF>수주 가능</color>"; // 흰색
            }
        }
    }
}