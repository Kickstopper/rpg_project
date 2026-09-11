using System;
using System.IO;
using System.Linq;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Dialogue;
using RPGProject.Feature.Negotiation;
using UnityEditor;
using UnityEngine;

public static class NegotiationCsvValidation
{
    [MenuItem("Tools/Negotiation/Validate CSV and Monster Database")]
    public static void Validate()
    {
        try
        {
            var catalog = NegotiationDialogueCatalog.Create(DialogueCsv.Read(
                File.ReadAllText("Assets/CSV/Dialogues/Negotiation.csv")));
            int count = 0;
            foreach (Personality p in Enum.GetValues(typeof(Personality)))
                foreach (Race race in Enum.GetValues(typeof(Race)))
                    foreach (Gender gender in Enum.GetValues(typeof(Gender)))
                    {
                        var rows = catalog.Resolve(p, race, gender, out var key);
                        var errors = NegotiationScriptValidator.Validate(rows);
                        if (errors.Count > 0) throw new FormatException($"{p}/{race}/{gender}: " + string.Join("\n", errors));
                        if (key == "DEFAULT") Debug.LogWarning($"[교섭 검증] {p}/{race}/{gender}: DEFAULT 대사를 사용합니다.");
                        count++;
                    }
            var db = AssetDatabase.LoadAssetAtPath<MonsterDatabase>("Assets/Database/MonsterDatabase.asset");
            if (db == null) throw new FormatException("MonsterDatabase.asset을 찾을 수 없습니다.");
            foreach (var monster in db.entries)
            {
                if (monster == null || !Enum.IsDefined(typeof(Personality), monster.personality) ||
                    !Enum.IsDefined(typeof(Race), monster.race) || !Enum.IsDefined(typeof(Gender), monster.gender))
                    throw new FormatException("몬스터의 성격·종족·성별 값이 유효하지 않습니다: " + monster?.id);
                catalog.Resolve(monster.personality, monster.race, monster.gender, out var key);
                if (key == "DEFAULT") Debug.LogWarning($"[교섭 검증] {monster.id}: DEFAULT 대사를 사용합니다.");
            }
            string distribution = string.Join(", ", db.entries.GroupBy(m => m.personality).Select(g => $"{g.Key}={g.Count()}"));
            Debug.Log($"[교섭 검증 완료] {count}개 조합, DB {db.entries.Count}종. 성격 분포: {distribution}. " +
                "이 검증은 분기 구조 검사이며 전투 UI·보상 실행 검증은 아닙니다.");
        }
        catch (Exception ex) { Debug.LogError("[교섭 검증 실패] " + ex.Message); }
    }
}
