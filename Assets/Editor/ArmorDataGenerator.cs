#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using Data;

public class ArmorDataGenerator : EditorWindow
{
    [MenuItem("Tools/CSV/Generate Armor from CSV")]
    public static void GenerateArmor()
    {
        string path = EditorUtility.OpenFilePanel("Select ArmorData CSV", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(path)) return;

        string[] lines = File.ReadAllLines(path);
        if (lines.Length <= 1) return;

        string saveDirectory = "Assets/Database/Items/Armors";
        EnsureDirectoryExists(saveDirectory);

        int createCount = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = line.Split(',');
            if (cols.Length < 18) continue; // 방어구 스키마(18개 컬럼) 검증

            ArmorData newItem = ScriptableObject.CreateInstance<ArmorData>();
            newItem.itemCategory = ItemCategory.Armor; // 장비류 분류 고정
            newItem.maxStackCount = 1;             // 방어구는 겹칠 수 없음

            // 1. 기본 정보 매핑
            newItem.id = cols[0].Trim();
            newItem.dataName = cols[1].Trim();
            newItem.description = cols[2].Trim();
            
            if (int.TryParse(cols[3], out int price)) newItem.price = price;
            
            // 2. 방어구 전용 정보 매핑
            if (Enum.TryParse(cols[4], true, out ArmorSlot slot)) newItem.slot = slot;
            if (int.TryParse(cols[5], out int def)) newItem.defense = def;
            if (int.TryParse(cols[6], out int eva)) newItem.evasionMod = eva;

            // 3. 스탯 보너스 (StatData) 조립
            // 만약 StatData가 클래스/구조체라면 새로 생성하여 값을 넣어줍니다.
            newItem.statBonus = new StatData();
            if (int.TryParse(cols[7], out int str)) newItem.statBonus.str = str;
            if (int.TryParse(cols[8], out int mag)) newItem.statBonus.mag = mag;
            if (int.TryParse(cols[9], out int vit)) newItem.statBonus.vit = vit;
            if (int.TryParse(cols[10], out int agi)) newItem.statBonus.agi = agi;
            if (int.TryParse(cols[11], out int luc)) newItem.statBonus.luc = luc;

            // 4. 내성 데이터 (ResistanceData) 조립
            newItem.resistanceMod = new ResistanceData();
            if (Enum.TryParse(cols[12], true, out ResistTier phys)) newItem.resistanceMod.phys = phys;
            if (Enum.TryParse(cols[13], true, out ResistTier fire)) newItem.resistanceMod.fire = fire;
            if (Enum.TryParse(cols[14], true, out ResistTier ice)) newItem.resistanceMod.ice = ice;
            if (Enum.TryParse(cols[15], true, out ResistTier elec)) newItem.resistanceMod.elec = elec;
            if (Enum.TryParse(cols[16], true, out ResistTier force)) newItem.resistanceMod.force = force;
            if (Enum.TryParse(cols[17], true, out ResistTier psyche)) newItem.resistanceMod.psyche = psyche;

            // 에셋 저장
            SaveAsset(newItem, saveDirectory, newItem.dataName);
            createCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=lime>[Armor] 성공적으로 {createCount}개의 방어구 데이터를 생성/업데이트했습니다!</color>");
    }

    // 유틸리티 함수: 에셋 덮어쓰기 및 저장
    private static void SaveAsset(ScriptableObject asset, string directory, string name)
    {
        string assetPath = $"{directory}/{name}.asset";
        ScriptableObject existingAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

        if (existingAsset != null)
        {
            EditorUtility.CopySerialized(asset, existingAsset);
        }
        else
        {
            AssetDatabase.CreateAsset(asset, assetPath);
        }
    }

    // 유틸리티 함수: 안전한 자동 폴더 생성
    private static void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] folders = path.Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(currentPath + "/" + folders[i]))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath += "/" + folders[i];
            }
        }
    }
}
#endif