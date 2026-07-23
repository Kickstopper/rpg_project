using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System;
public class MonsterCSVImporter
{
    [MenuItem("Tools/CSV/Stats & Resistances From CSV")]
    public static void ImportStatsAndResistances()
    {
        string filePath = EditorUtility.OpenFilePanel("Select Monster Stats CSV", "", "csv");
        if (string.IsNullOrEmpty(filePath)) return;

        string[] guids = AssetDatabase.FindAssets("t:MonsterDatabase");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("에러", "MonsterDatabase 에셋을 찾을 수 없습니다.", "확인");
            return;
        }
        string dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        MonsterDatabase db = AssetDatabase.LoadAssetAtPath<MonsterDatabase>(dbPath);

        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length <= 1) return;

        string[] headers = lines[0].Split(',');
        
        // 스탯 인덱스
        int nameIdx = -1, levelIdx = -1, strIdx = -1, magIdx = -1, intelIdx = -1, vitIdx = -1, agiIdx = -1, lucIdx = -1;
        // 내성 인덱스
        int physIdx = -1, fireIdx = -1, iceIdx = -1, elecIdx = -1, forceIdx = -1, psycheIdx = -1;

        for (int i = 0; i < headers.Length; i++)
        {
            string h = headers[i].Trim();
            if (h == "Name") nameIdx = i;
            else if (h == "Level") levelIdx = i;
            else if (h == "Str") strIdx = i;
            else if (h == "Mag") magIdx = i;
            else if (h == "Intel") intelIdx = i;
            else if (h == "Vit") vitIdx = i;
            else if (h == "Agi") agiIdx = i;
            else if (h == "Luc") lucIdx = i;
            
            else if (h == "Phys") physIdx = i;
            else if (h == "Fire") fireIdx = i;
            else if (h == "Ice") iceIdx = i;
            else if (h == "Elec") elecIdx = i;
            else if (h == "Force") forceIdx = i;
            else if (h == "Psyche") psycheIdx = i;
        }

        if (nameIdx == -1 || levelIdx == -1)
        {
            EditorUtility.DisplayDialog("에러", "CSV 파일의 필수 컬럼(Name, Level 등)이 누락되었습니다.", "확인");
            return;
        }

        Dictionary<string, MonsterDatabase.MonsterEntry> entryMap = new Dictionary<string, MonsterDatabase.MonsterEntry>();
        foreach (var entry in db.entries)
        {
            string targetName = entry.name; 
            if (!string.IsNullOrEmpty(targetName) && !entryMap.ContainsKey(targetName))
            {
                entryMap.Add(targetName, entry);
            }
        }

        int updateCount = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] columns = lines[i].Split(',');
            if (columns.Length <= nameIdx) continue;

            string mName = columns[nameIdx].Trim();

            if (entryMap.TryGetValue(mName, out var entry))
            {
                // 1. 스탯(Struct) 업데이트
                var newStats = entry.stats; 
                
                if (levelIdx != -1 && columns.Length > levelIdx) int.TryParse(columns[levelIdx].Trim(), out newStats.level);
                if (strIdx != -1 && columns.Length > strIdx) int.TryParse(columns[strIdx].Trim(), out newStats.str);
                if (magIdx != -1 && columns.Length > magIdx) int.TryParse(columns[magIdx].Trim(), out newStats.mag);
                if (intelIdx != -1 && columns.Length > intelIdx) int.TryParse(columns[intelIdx].Trim(), out newStats.intel);
                if (vitIdx != -1 && columns.Length > vitIdx) int.TryParse(columns[vitIdx].Trim(), out newStats.vit);
                if (agiIdx != -1 && columns.Length > agiIdx) int.TryParse(columns[agiIdx].Trim(), out newStats.agi);
                if (lucIdx != -1 && columns.Length > lucIdx) int.TryParse(columns[lucIdx].Trim(), out newStats.luc);

                entry.stats = newStats;

                // 2. 내성(Struct & Enum) 업데이트
                var newResist = entry.resistances;

                // 문자열을 안전하게 Enum으로 변환하는 로컬 헬퍼 함수
                void TryParseResist(int idx, ref ResistTier targetTier)
                {
                    if (idx != -1 && columns.Length > idx)
                    {
                        // true 플래그는 대소문자 구분을 무시하여 파싱을 안전하게 만듭니다.
                        if (Enum.TryParse<ResistTier>(columns[idx].Trim(), true, out ResistTier parsed))
                        {
                            targetTier = parsed;
                        }
                    }
                }

                TryParseResist(physIdx, ref newResist.phys);
                TryParseResist(fireIdx, ref newResist.fire);
                TryParseResist(iceIdx, ref newResist.ice);
                TryParseResist(elecIdx, ref newResist.elec);
                TryParseResist(forceIdx, ref newResist.force);
                TryParseResist(psycheIdx, ref newResist.psyche);

                entry.resistances = newResist;
                
                updateCount++;
            }
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("데이터 임포트 완료", $"총 {updateCount}마리의 몬스터 스탯과 내성이 성공적으로 업데이트되었습니다!", "확인");
    }
}