#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using Data; // 프로젝트의 Data 네임스페이스

public class SkillDataGenerator : EditorWindow
{
    // 유니티 상단 메뉴에 [Tools -> Generate Skill Data] 메뉴를 생성합니다.
    [MenuItem("Tools/Generate Skill Data from CSV")]
    public static void GenerateSkills()
    {
        // CSV 파일 선택 창 띄우기
        string path = EditorUtility.OpenFilePanel("Select Monster Skill CSV", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(path)) return;

        string[] lines = File.ReadAllLines(path);
        if (lines.Length <= 1) 
        {
            Debug.LogWarning("CSV 파일이 비어있거나 헤더만 존재합니다.");
            return;
        }

        // 에셋을 저장할 폴더 경로 설정
        string saveDirectory = "Assets/Database/Skills";
        if (!AssetDatabase.IsValidFolder(saveDirectory))
        {
            Debug.LogError($"저장 경로가 존재하지 않습니다. 폴더를 생성해주세요: {saveDirectory}");
            return;
        }

        int createCount = 0;

        // 2번째 줄(인덱스 1)부터 데이터 파싱
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = line.Split(',');

            // 현재 기획서 기준 최소 14개의 컬럼이 있어야 안전합니다.
            if (cols.Length < 14) continue; 

            // 새 SkillData 인스턴스 생성
            SkillData newSkill = ScriptableObject.CreateInstance<SkillData>();

            // [문자열 데이터 매핑]
            newSkill.dataName = cols[0].Trim();
            newSkill.id = cols[1].Trim();
            newSkill.description = cols[13].Trim();

            // [수치 데이터 매핑]
            if (float.TryParse(cols[6], out float power)) 
                newSkill.effectValue = Mathf.RoundToInt(power);

            if (float.TryParse(cols[8], out float mpCost))
            {
                newSkill.costValue = Mathf.RoundToInt(mpCost);
                newSkill.useHpCost = false; // 현재 테이블은 MPCost 기준이므로 false
            }
                
            if (float.TryParse(cols[10], out float chance)) 
                newSkill.statusEffectChance = chance / 100f; // 25.0 -> 0.25f 로 변환

            // [열거형(Enum) 매핑]
            // ElementType 매핑
            if (Enum.TryParse(cols[3], true, out ElementType el)) 
                newSkill.element = el;
            else 
                newSkill.element = ElementType.None;

            // TargetScope 매핑
            if (Enum.TryParse(cols[5], true, out TargetScope ts)) 
                newSkill.targetScope = ts;

            // StatusEffect 매핑 (독, 출혈 등)
            if (Enum.TryParse(cols[9], true, out StatusEffect se)) 
                newSkill.statusEffect = se;
            else 
                newSkill.statusEffect = StatusEffect.None;

            // EffectType 매핑 (CSV의 Type 컬럼 분석)
            string typeStr = cols[4].Trim();
            string skillName = cols[2].Trim().ToLower(); // 소문자로 통일하여 비교

            if (typeStr == "Attack")
            {
                newSkill.effectType = (newSkill.element == ElementType.Physical) ? EffectType.Special_Atk : EffectType.Magic_Atk;
            }
            else if (typeStr == "Recovery")
            {
                // 광역 회복과 구분을 위해 메디아 계열인지 판별
                if (skillName.StartsWith("me"))
                {
                    newSkill.effectType = EffectType.Recover_HP; // All_Allies 타겟과 결합하여 광역 회복 처리
                }
                else
                {
                    newSkill.effectType = EffectType.Recover_HP;
                }
            }
            else if (typeStr == "Assistance")
            {
                // 여신전생의 대표 보조 마법 키워드로 매핑 분기
                if (skillName.Contains("tarukaja"))
                    newSkill.effectType = EffectType.Buff_Phys_Atk; // 혹은 필요시 Buff_Magic_Atk 병합 연산
                else if (skillName.Contains("rakukaja"))
                    newSkill.effectType = EffectType.Buff_Phys_Def;
                else if (skillName.Contains("tarunda"))
                    newSkill.effectType = EffectType.Debuff_Phys_Atk;
                else if (skillName.Contains("rakunda"))
                    newSkill.effectType = EffectType.Debuff_Phys_Def;
                else
                    newSkill.effectType = EffectType.None;
            }

            // 에셋으로 구워내기 (.asset 확장자)
            string assetPath = $"{saveDirectory}/{newSkill.dataName}.asset";
            
            // 덮어쓰기 방지 및 업데이트 로직
            SkillData existingAsset = AssetDatabase.LoadAssetAtPath<SkillData>(assetPath);
            if (existingAsset != null)
            {
                EditorUtility.CopySerialized(newSkill, existingAsset); // 기존 파일이 있으면 데이터만 덮어씀
                AssetDatabase.SaveAssets();
            }
            else
            {
                AssetDatabase.CreateAsset(newSkill, assetPath); // 새 파일 생성
            }
            createCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>성공적으로 {createCount}개의 SkillData를 생성/업데이트했습니다!</color>");
    }
}
#endif