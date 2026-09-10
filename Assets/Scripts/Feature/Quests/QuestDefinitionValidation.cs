using System.Collections.Generic;

namespace RPGProject.Feature.Quests
{
    public static class QuestDefinitionValidation
    {
        public static string Error(QuestData q)
        {
            if (q == null) return "누락된 퀘스트 에셋입니다.";
            if (string.IsNullOrWhiteSpace(q.QuestID)) return "QuestID가 없습니다.";
            if (string.IsNullOrWhiteSpace(q.locationID) || q.Reward < 0 || q.Risk < 0) return q.QuestID + ": 장소/보상/위험도 오류";
            if (q.Targets == null || (q.Targets.Count == 0 && string.IsNullOrWhiteSpace(q.completionFlag))) return q.QuestID + ": 토벌 목표 또는 완료 플래그가 필요합니다.";
            var seen = new HashSet<string>();
            foreach (var t in q.Targets)
                if (t == null || string.IsNullOrWhiteSpace(t.monsterID) || t.requiredCount <= 0 || !seen.Add(t.monsterID))
                    return q.QuestID + ": 목표 ID 중복/누락 또는 수량 오류";
            if (q.prerequisiteQuestIDs != null && q.prerequisiteQuestIDs.Contains(q.QuestID)) return q.QuestID + ": 자신을 선행 의뢰로 지정할 수 없습니다.";
            return null;
        }
    }
}
