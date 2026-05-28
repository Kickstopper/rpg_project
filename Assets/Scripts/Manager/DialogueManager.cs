using System.Collections.Generic;
using Data;
using Unity.Android.Gradle.Manifest;
using UnityEngine;

namespace Manager
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance;
        
        [Header("Data Files")]
        // 인스펙터에서 할당
        public TextAsset negotiationCSV;
        public TextAsset eventScriptsCSV;

        private List<Dictionary<string, string>> allDialogueData;
        private Dictionary<string, List<Dictionary<string, string>>> eventDatabase = new();
        private Dictionary<string, NegotiationData> negotiationDB = new();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                LoadData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void LoadData()
        {
            ParseNegotiationCSV();
            ParseDialogueCSV();
        }

        void ParseNegotiationCSV()
        {
            if (negotiationCSV == null)
            {
                Debug.LogError("교섭 CSV 파일이 할당되지 않았습니다.");
                return;
            }

            var parsedData = CSVReader.Read(negotiationCSV);
            foreach (var row in parsedData)
            {
                // 데이터 무결성 검사
                if (!row.ContainsKey("Seq") || string.IsNullOrEmpty(row["Seq"])) continue;
                
                NegotiationData data = new NegotiationData
                {
                    Seq = row.ContainsKey("Seq") ? row["Seq"] : "",
                    Type = row.ContainsKey("Type") ? row["Type"] : "",
                    Category = row.ContainsKey("Category") ? row["Category"] : "",
                    Situation = row.ContainsKey("Situation") ? row["Situation"] : "",
                    Name = row.ContainsKey("Name") ? row["Name"] : "",
                    CharacterID = row.ContainsKey("CharacterID") ? row["CharacterID"] : "",
                    Text = row.ContainsKey("Text") ? row["Text"] : "",
                    NextID = row.ContainsKey("NextID") ? row["NextID"] : "",
                    Param = row.ContainsKey("Param") ? row["Param"] : "",
                };

                if (!negotiationDB.ContainsKey(data.Seq))
                {
                    negotiationDB.Add(data.Seq, data);
                }
            }
            
            Debug.Log($"[DialogueManager] 교섭 데이터 {negotiationDB.Count}건 로드 완료.");
        }

        void ParseDialogueCSV()
        {
            if (eventScriptsCSV == null)
            {
                Debug.LogError("일반 대화(Event_Scripts) CSV 파일이 할당되지 않았습니다!");
                return;
            }

            allDialogueData = CSVReader.Read(eventScriptsCSV);
            foreach (var row in allDialogueData)
            {
                if (row.ContainsKey("EventID"))
                {
                    string id = row["EventID"];
                    if (!eventDatabase.ContainsKey(id))
                    {
                        eventDatabase[id] = new List<Dictionary<string, string>>();
                    }
                    eventDatabase[id].Add(row);
                }
            }
            
            Debug.Log($"[DialogueManager] 일반 대화 데이터 로드 완료. Total Rows: {allDialogueData.Count}");
        }

        public List<Dictionary<string, string>> GetEventData(string eventID)
        {
            if (eventDatabase.ContainsKey(eventID))
                return eventDatabase[eventID];
            
            Debug.LogWarning($"Event ID '{eventID}' not found.");
            return new List<Dictionary<string, string>>();
        }

        public List<Dictionary<string, string>> GetNegotiationDialogues(MonsterDatabase.MonsterEntry sourceData)
        {
            string key = $"{sourceData.personality}_{sourceData.gender}";
            string monsterName = sourceData.name;
            
            List<Dictionary<string, string>> eventLines = new List<Dictionary<string, string>>();

            foreach (var data in negotiationDB.Values)
            {
                if (data.Category == key)
                {
                    Dictionary<string, string> dict = data.ToDictionary();

                    dict["Name"] = monsterName;

                    string callName = "너";
                    
                    dict["Text"] = dict["Text"].Replace("{CallName}", callName);

                    eventLines.Add(dict);
                }
            }

            return eventLines;
        }
    }
}