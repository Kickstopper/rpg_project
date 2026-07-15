#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using Data;

public class EquipmentDataGenerator : EditorWindow
{
    // =====================================================================
    // 1. 근접 무기 및 총기 (Weapon) 자동 생성기
    // =====================================================================
    [MenuItem("Tools/Generate Weapons from CSV")]
    public static void GenerateWeapons()
    {
        string path = EditorUtility.OpenFilePanel("Select WeaponData CSV", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(path)) return;

        string[] lines = File.ReadAllLines(path);
        if (lines.Length <= 1) return;

        string saveDirectory = "Assets/Database/Items/Weapons";
        EnsureDirectoryExists(saveDirectory);

        int createCount = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = line.Split(',');
            if (cols.Length < 14) continue; // 무기 스키마(14개 컬럼) 검증

            WeaponData newItem = ScriptableObject.CreateInstance<WeaponData>();
            newItem.itemCategory = ItemCategory.Weapon; // 카테고리 강제 고정
            newItem.maxStackCount = 1;              // 무기는 겹칠 수 없음

            // 데이터 파싱
            newItem.id = cols[0].Trim();
            newItem.dataName = cols[1].Trim();
            newItem.description = cols[2].Trim();
            
            if (int.TryParse(cols[3], out int price)) newItem.price = price;
            if (Enum.TryParse(cols[4], true, out ElementType elem)) newItem.element = elem;
            
            if (Enum.TryParse(cols[5], true, out StatusEffect statusEff)) newItem.statusEffect = statusEff;
            if (float.TryParse(cols[6], out float chance)) newItem.statusEffectChance = chance / 100f; // 확률 백분율 변환
            
            if (Enum.TryParse(cols[7], true, out WeaponType type)) newItem.type = type;
            if (Enum.TryParse(cols[8], true, out WeaponCategory wCat)) newItem.weaponCategory = wCat; // 변수명 충돌 수정 반영
            if (Enum.TryParse(cols[9], true, out TargetScope range)) newItem.attackRange = range;
            
            if (int.TryParse(cols[10], out int atk)) newItem.attackPower = atk;
            if (int.TryParse(cols[11], out int hitBonus)) newItem.hitRateBonus = hitBonus;
            if (int.TryParse(cols[12], out int min)) newItem.minHits = min;
            if (int.TryParse(cols[13], out int max)) newItem.maxHits = max;

            SaveAsset(newItem, saveDirectory, newItem.dataName);
            createCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=cyan>[Weapon] 성공적으로 {createCount}개의 무기 데이터를 생성/업데이트했습니다!</color>");
    }

    // =====================================================================
    // 탄환 (Ammo) 자동 생성기
    // =====================================================================
    [MenuItem("Tools/Generate Ammo from CSV")]
    public static void GenerateAmmo()
    {
        string path = EditorUtility.OpenFilePanel("Select AmmoData CSV", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(path)) return;

        string[] lines = File.ReadAllLines(path);
        if (lines.Length <= 1) return;

        string saveDirectory = "Assets/Database/Items/Ammo";
        EnsureDirectoryExists(saveDirectory);

        int createCount = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = line.Split(',');
            if (cols.Length < 10) continue; // 탄환 스키마(10개 컬럼) 검증

            AmmoData newItem = ScriptableObject.CreateInstance<AmmoData>();
            newItem.itemCategory = ItemCategory.Weapon; // 무기류에 귀속

            // 데이터 파싱
            newItem.id = cols[0].Trim();
            newItem.dataName = cols[1].Trim();
            newItem.description = cols[2].Trim();
            
            if (int.TryParse(cols[3], out int price)) newItem.price = price;
            if (int.TryParse(cols[4], out int maxStack)) newItem.maxStackCount = maxStack;
            
            if (Enum.TryParse(cols[5], true, out ElementType elem)) newItem.element = elem;
            if (Enum.TryParse(cols[6], true, out StatusEffect statusEff)) newItem.statusEffect = statusEff;
            if (float.TryParse(cols[7], out float chance)) newItem.statusEffectChance = chance / 100f;
            
            if (int.TryParse(cols[8], out int dmgBonus)) newItem.damageBonus = dmgBonus;
            if (int.TryParse(cols[9], out int hitBonus)) newItem.hitRateBonus = hitBonus;

            SaveAsset(newItem, saveDirectory, newItem.dataName);
            createCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=orange>[Ammo] 성공적으로 {createCount}개의 탄환 데이터를 생성/업데이트했습니다!</color>");
    }

    // =====================================================================
    // 유틸리티 함수: 에셋 저장 및 폴더 검증
    // =====================================================================
    private static void SaveAsset(ScriptableObject asset, string directory, string id)
    {
        string assetPath = $"{directory}/{id}.asset";
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

    private static void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            // 폴더가 없으면 에러를 띄우지 않고 자동으로 상위 폴더들을 추적하여 생성해 줍니다.
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