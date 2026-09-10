using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using RPGProject.Feature.Negotiation;
using RPGProject.Feature.Characters;
using RPGProject.Infrastructure.DataAccess;

namespace RPGProject.Feature.Dialogue
{
    public class DialogueManager : MonoBehaviour
    {
        [Header("Data Files")]
        public TextAsset negotiationCSV; // 인스펙터에서 할당
        public TextAsset eventScriptsCSV; // 인스펙터에서 할당

        // 일반 이벤트와 교섭 카테고리는 ID 충돌을 막기 위해 별도 보관
        private Dictionary<string, List<Dictionary<string, string>>> eventDatabase = new Dictionary<string, List<Dictionary<string, string>>>();

        private readonly Dictionary<string, List<Dictionary<string, string>>> negotiationDatabase = new Dictionary<string, List<Dictionary<string, string>>>();

        void Awake()
        {
            LoadData();
        }

        void LoadData()
        {
            ParseCSVToDatabase(eventScriptsCSV, "일반 대화", true);
            LoadNegotiations();
        }

        void ParseCSVToDatabase(TextAsset csvFile, string logName, bool useDialogueCsv = false)
        {
            if (csvFile == null)
            {
                Debug.LogError($"[{logName}] CSV 파일이 할당되지 않았습니다!");
                return;
            }

            List<Dictionary<string, string>> parsedData;
            try
            {
                parsedData = useDialogueCsv ? DialogueCsv.Read(csvFile.text) : CSVReader.Read(csvFile);
            }
            catch (System.FormatException ex)
            {
                Debug.LogError($"[{logName}] {csvFile.name}: {ex.Message}");
                return;
            }
            int rowCount = 0;

            foreach (var row in parsedData)
            {
                // EventID(교섭에서는 SLY_FEMALE 등)를 키값으로 사용
                if (row.ContainsKey("EventID") && !string.IsNullOrEmpty(row["EventID"]))
                {
                    string id = row["EventID"];
                    if (!eventDatabase.ContainsKey(id))
                    {
                        eventDatabase[id] = new List<Dictionary<string, string>>();
                    }
                    eventDatabase[id].Add(row);
                    rowCount++;
                }
            }
            
            Debug.Log($"[DialogueManager] {logName} 데이터 로드 완료. 추가된 Rows: {rowCount}");
        }

        public bool HasEvent(string eventID)
        {
            return !string.IsNullOrEmpty(eventID) && eventDatabase.TryGetValue(eventID, out var lines) && lines.Count > 0;
        }

        public List<Dictionary<string, string>> GetEventData(string eventID)
        {
            if (eventDatabase.ContainsKey(eventID))
            {
                return DeepCopyList(eventDatabase[eventID]);
            }
            
            Debug.LogWarning($"Event ID '{eventID}' not found.");
            return new List<Dictionary<string, string>>();
        }

        private void LoadNegotiations()
        {
            negotiationDatabase.Clear();
            if (negotiationCSV == null) { Debug.LogError("교섭 CSV가 없습니다."); return; }
            try
            {
                var rows = DialogueCsv.Read(negotiationCSV.text);
                foreach (var row in rows)
                    foreach (var key in new[] { "EventID", "Seq", "Type", "Condition", "Action", "NextID" })
                        if (row.ContainsKey(key)) row[key] = row[key].Trim();
                foreach (var group in rows.GroupBy(r => NegotiationScriptValidator.Value(r, "EventID")))
                {
                    var lines = group.ToList();
                    var errors = NegotiationScriptValidator.Validate(lines);
                    if (string.IsNullOrWhiteSpace(group.Key)) errors.Add("EventID가 없습니다.");
                    if (errors.Count > 0)
                    {
                        Debug.LogError($"[교섭 CSV] {group.Key}:\n" + string.Join("\n", errors));
                        continue;
                    }
                    negotiationDatabase.Add(group.Key, lines);
                }
            }
            catch (FormatException ex) { Debug.LogError("[교섭 CSV] " + ex.Message); }
        }

        public List<Dictionary<string, string>> GetNegotiationDialogues(MonsterDatabase.MonsterEntry sourceData)
        {
            if (sourceData == null) return new List<Dictionary<string, string>>();
            string personality = sourceData.personality.ToString().ToUpperInvariant();
            string gender = sourceData.gender.ToString().ToUpperInvariant();
            foreach (string key in new[] { personality + "_" + gender, personality, "DEFAULT" })
            {
                if (!negotiationDatabase.TryGetValue(key, out var source)) continue;
                var lines = DeepCopyList(source);
                foreach (var row in lines)
                {
                    if (string.IsNullOrEmpty(NegotiationScriptValidator.Value(row, "Name"))) row["Name"] = sourceData.name;
                    if (row.TryGetValue("Text", out var text))
                        row["Text"] = text.Replace("{CallName}", "너").Replace("{Gender_Call}", "너");
                }
                return lines;
            }
            Debug.LogWarning($"[교섭] {personality}_{gender} 또는 DEFAULT 대사가 없습니다.");
            return new List<Dictionary<string, string>>();
        }

        private List<Dictionary<string, string>> DeepCopyList(List<Dictionary<string, string>> original)
        {
            var copy = new List<Dictionary<string, string>>();
            foreach (var dict in original)
            {
                copy.Add(new Dictionary<string, string>(dict));
            }
            return copy;
        }
    }
}