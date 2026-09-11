using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using RPGProject.Feature.Negotiation;
using RPGProject.Feature.Characters;
using RPGProject.Infrastructure.DataAccess;
using RPGProject.Core;

namespace RPGProject.Feature.Dialogue
{
    public class DialogueManager : MonoBehaviour
    {
        [Header("Data Files")]
        public TextAsset negotiationCSV; // 인스펙터에서 할당
        public TextAsset eventScriptsCSV; // 인스펙터에서 할당

        // 일반 이벤트와 교섭 카테고리는 ID 충돌을 막기 위해 별도 보관
        private Dictionary<string, List<Dictionary<string, string>>> eventDatabase = new Dictionary<string, List<Dictionary<string, string>>>();

        private NegotiationDialogueCatalog negotiationCatalog;

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
            negotiationCatalog = null;
            if (negotiationCSV == null) { Debug.LogError("교섭 CSV가 없습니다."); return; }
            try
            {
                var rows = DialogueCsv.Read(negotiationCSV.text);
                negotiationCatalog = NegotiationDialogueCatalog.Create(rows);
            }
            catch (FormatException ex) { Debug.LogError("[교섭 CSV] " + ex.Message); }
        }

        public List<Dictionary<string, string>> GetNegotiationDialogues(MonsterDatabase.MonsterEntry sourceData)
        {
            if (sourceData == null || negotiationCatalog == null) return new List<Dictionary<string, string>>();
            var lines = negotiationCatalog.Resolve(sourceData.personality, sourceData.race, sourceData.gender, out _);
            var demandItem = ManagerRoot.Database != null ? ManagerRoot.Database.GetItem(NegotiationTradeRules.ItemDemandID) : null;
            foreach (var row in lines)
            {
                if (string.IsNullOrEmpty(NegotiationScriptValidator.Value(row, "Name"))) row["Name"] = sourceData.name;
                if (row.TryGetValue("Text", out var text))
                    row["Text"] = NegotiationTradeRules.ExpandText(text, demandItem != null ? demandItem.dataName : null)
                        .Replace("{CallName}", "너").Replace("{Gender_Call}", "너")
                        .Replace("{MonsterName}", sourceData.name ?? "");
            }
            return lines;
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
