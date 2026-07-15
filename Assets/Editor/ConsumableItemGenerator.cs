#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using Data;

public class ConsumableItemGenerator : EditorWindow
{
    [MenuItem("Tools/Generate Consumable Items from CSV")]
    public static void GenerateItems()
    {
        string path = EditorUtility.OpenFilePanel("Select Consumable Item CSV", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(path)) return;

        string[] lines = File.ReadAllLines(path);
        if (lines.Length <= 1) return;

        // 에셋 저장 위치 지정
        string saveDirectory = "Assets/Database/Items/Consumables";
        if (!AssetDatabase.IsValidFolder(saveDirectory))
        {
            Debug.LogError($"저장 경로가 존재하지 않습니다. 폴더를 생성해주세요: {saveDirectory}");
            return;
        }

        int createCount = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = line.Split(',');
            if (cols.Length < 14) continue; // 14개의 모든 필드 존재 여부 확인

            ConsumableItemData newItem = ScriptableObject.CreateInstance<ConsumableItemData>();

            // 1. BaseRootData 필드 파싱
            newItem.id = cols[0].Trim();
            newItem.dataName = cols[1].Trim();
            newItem.description = cols[2].Trim();

            if (Enum.TryParse(cols[3], true, out EffectType effType)) newItem.effectType = effType;
            if (int.TryParse(cols[4], out int effVal)) newItem.effectValue = effVal;
            if (Enum.TryParse(cols[5], true, out ElementType elem)) newItem.element = elem;
            if (Enum.TryParse(cols[6], true, out TargetScope scope)) newItem.targetScope = scope;
            if (Enum.TryParse(cols[7], true, out UseType useType)) newItem.useType = useType;

            // 2. BaseItemData 필드 파싱
            if (int.TryParse(cols[8], out int price)) newItem.price = price;
            if (int.TryParse(cols[9], out int maxStack)) newItem.maxStackCount = maxStack;
            if (Enum.TryParse(cols[10], true, out ItemCategory cat)) newItem.itemCategory = cat;
            if (Enum.TryParse(cols[11], true, out StatusEffect statusEff)) newItem.statusEffect = statusEff;
            if (float.TryParse(cols[12], out float chance)) newItem.statusEffectChance = chance / 100f; // 백분율 보정
            if (int.TryParse(cols[13], out int delay)) newItem.actionDelay = delay;

            // 에셋 생성 및 업데이트
            string assetPath = $"{saveDirectory}/{newItem.dataName}.asset";
            ConsumableItemData existingAsset = AssetDatabase.LoadAssetAtPath<ConsumableItemData>(assetPath);

            if (existingAsset != null)
            {
                EditorUtility.CopySerialized(newItem, existingAsset);
                AssetDatabase.SaveAssets();
            }
            else
            {
                AssetDatabase.CreateAsset(newItem, assetPath);
            }
            createCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>성공적으로 {createCount}개의 ConsumableItemData를 생성/업데이트했습니다!</color>");
    }
}
#endif