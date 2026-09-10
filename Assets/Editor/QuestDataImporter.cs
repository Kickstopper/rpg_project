using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Dialogue;
using RPGProject.Feature.Quests;

using UnityEditor;
using UnityEngine;

public class QuestDataImporter : EditorWindow
{
    private string csvFilePath = "Assets/CSV/QuestList.csv";
    private string savePath = "Assets/Database/Quest";
    [MenuItem("Tools/CSV/Quest Data Importer")]
    public static void ShowWindow() { GetWindow<QuestDataImporter>("Quest Importer"); }
    private void OnGUI()
    {
        csvFilePath = EditorGUILayout.TextField("CSV", csvFilePath);
        savePath = EditorGUILayout.TextField("출력 폴더", savePath);
        if (GUILayout.Button("CSV 선택"))
        {
            string picked = EditorUtility.OpenFilePanel("Quest CSV", Application.dataPath, "csv");
            if (!string.IsNullOrEmpty(picked)) csvFilePath = picked;
        }
        EditorGUILayout.HelpBox("전체 검증을 통과한 경우에만 에셋을 갱신합니다. 기존 에셋의 GUID를 유지합니다.", MessageType.Info);
        if (GUILayout.Button("검증 후 가져오기")) ImportCSV(csvFilePath, savePath);
    }
    public static void ImportCSV(string path, string folder)
    {
        var staged = new List<QuestData>();
        try
        {
            if (!AssetDatabase.IsValidFolder(folder) || !folder.StartsWith("Assets/", StringComparison.Ordinal))
                throw new FormatException("Assets 아래의 기존 출력 폴더를 선택하세요.");
            var rows = DialogueCsv.Read(File.ReadAllText(path));
            var ids = new HashSet<string>();
            var monsterDB = AssetDatabase.LoadAssetAtPath<MonsterDatabase>("Assets/Database/MonsterDatabase.asset");
            if (monsterDB == null) throw new FormatException("MonsterDatabase.asset을 찾을 수 없습니다.");
            int line = 1;
            foreach (var row in rows)
            {
                line++;
                string Read(string key) => row.TryGetValue(key, out var value) ? value.Trim() : "";
                int Number(string key)
                {
                    if (!int.TryParse(Read(key), out int value)) throw new FormatException($"{line}행 {key}: 숫자가 필요합니다.");
                    return value;
                }
                var q = CreateInstance<QuestData>(); staged.Add(q);
                q.QuestID = Read("QuestID"); q.QuestName = Read("QuestName"); q.QuestType = Read("QuestType");
                q.Location = Read("Location"); q.locationID = Read("locationID");
                q.Reward = Number("Reward"); q.Risk = Number("Risk"); q.Description = Read("Description");
                if (q.QuestID.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || q.QuestID.Contains("/") || q.QuestID.Contains("\\") || !ids.Add(q.QuestID))
                    throw new FormatException($"{line}행: 중복/잘못된 QuestID");
                foreach (var raw in Read("Targets").Split('|').Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    var pair = raw.Split(':');
                    if (pair.Length != 2 || !int.TryParse(pair[1], out int count) || count <= 0)
                        throw new FormatException($"{line}행: 목표는 몬스터ID:양수 형식이어야 합니다: {raw}");
                    string id = pair[0].Trim();
                    if (monsterDB.GetEntry(id) == null) throw new FormatException($"{line}행: 없는 몬스터 {id}");
                    q.Targets.Add(new QuestTarget { monsterID = id, requiredCount = count });
                }
                var existing = AssetDatabase.LoadAssetAtPath<QuestData>($"{folder}/{q.QuestID}.asset");
                q.completionFlag = row.ContainsKey("CompletionFlag") ? Read("CompletionFlag") : existing != null ? existing.completionFlag : "";
                q.prerequisiteQuestIDs = row.ContainsKey("Prerequisites")
                    ? Read("Prerequisites").Split('|').Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList()
                    : new List<string>(existing != null && existing.prerequisiteQuestIDs != null ? existing.prerequisiteQuestIDs : new List<string>());
                string error = QuestDefinitionValidation.Error(q);
                if (error != null) throw new FormatException($"{line}행: {error}");
            }
            if (staged.Count == 0) throw new FormatException("퀘스트가 없습니다.");
            var table = staged.ToDictionary(q => q.QuestID);
            var visiting = new HashSet<string>(); var done = new HashSet<string>();
            void Visit(string id)
            {
                if (done.Contains(id)) return;
                if (!table.ContainsKey(id)) throw new FormatException("CSV에 없는 선행 퀘스트: " + id);
                if (!visiting.Add(id)) throw new FormatException("선행 퀘스트 순환: " + id);
                foreach (var required in table[id].prerequisiteQuestIDs) Visit(required);
                visiting.Remove(id); done.Add(id);
            }
            foreach (var id in ids) Visit(id);
            var database = AssetDatabase.LoadAssetAtPath<QuestDatabase>("Assets/Database/QuestDatabase.asset");
            if (database == null) throw new FormatException("QuestDatabase.asset을 찾을 수 없습니다.");
            Undo.RecordObject(database, "Import quests");
            foreach (var q in staged)
            {
                string assetPath = $"{folder}/{q.QuestID}.asset";
                var target = AssetDatabase.LoadAssetAtPath<QuestData>(assetPath);
                if (target != null) { Undo.RecordObject(target, "Import quest"); EditorUtility.CopySerialized(q, target); EditorUtility.SetDirty(target); }
                else { target = Instantiate(q); AssetDatabase.CreateAsset(target, assetPath); Undo.RegisterCreatedObjectUndo(target, "Import quest"); }
                if (!database.db.Contains(target)) database.db.Add(target);
            }
            EditorUtility.SetDirty(database); AssetDatabase.SaveAssets();
            Debug.Log($"퀘스트 {staged.Count}개 갱신 완료. 기존 CSV 밖의 의뢰는 유지했습니다.");
        }
        catch (Exception ex) { Debug.LogError("퀘스트 가져오기 실패: " + ex.Message); }
        finally { foreach (var q in staged) DestroyImmediate(q); }
    }
}
