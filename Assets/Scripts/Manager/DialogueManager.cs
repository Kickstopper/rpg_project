using System.Collections.Generic;
using UnityEngine;

namespace Manager
{
    public class DialogueManager : MonoBehaviour
    {
        [Header("Data Files")]
        public TextAsset negotiationCSV; // 인스펙터에서 할당
        public TextAsset eventScriptsCSV; // 인스펙터에서 할당

        // 일반 대화와 교섭 대화가 모두 저장될 단일 데이터베이스
        private Dictionary<string, List<Dictionary<string, string>>> eventDatabase = new Dictionary<string, List<Dictionary<string, string>>>();

        void Awake()
        {
            LoadData();
        }

        void LoadData()
        {
            ParseCSVToDatabase(eventScriptsCSV, "일반 대화");
            ParseCSVToDatabase(negotiationCSV, "교섭 대화");
        }

        void ParseCSVToDatabase(TextAsset csvFile, string logName)
        {
            if (csvFile == null)
            {
                Debug.LogError($"[{logName}] CSV 파일이 할당되지 않았습니다!");
                return;
            }

            var parsedData = CSVReader.Read(csvFile);
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

        public List<Dictionary<string, string>> GetEventData(string eventID)
        {
            if (eventDatabase.ContainsKey(eventID))
            {
                return DeepCopyList(eventDatabase[eventID]);
            }
            
            Debug.LogWarning($"Event ID '{eventID}' not found.");
            return new List<Dictionary<string, string>>();
        }

        public List<Dictionary<string, string>> GetNegotiationDialogues(MonsterDatabase.MonsterEntry sourceData)
        {
            string key = "SLY_FEMALE";// $"{sourceData.personality}_{sourceData.gender}";
            string monsterName = sourceData.name;
            
            List<Dictionary<string, string>> eventLines = GetEventData(key);

            if (eventLines.Count == 0)
            {
                Debug.LogWarning($"[{key}] 카테고리의 교섭 스크립트가 없습니다!");
                return eventLines;
            }

            string callName = "너";

            foreach (var dict in eventLines)
            {
                // Name이 CSV에 없으면 실제 몬스터 이름으로 치환
                if (dict.ContainsKey("Name") && string.IsNullOrEmpty(dict["Name"]))
                {
                    dict["Name"] = monsterName;
                }

                // 텍스트 변수 치환
                if (dict.ContainsKey("Text") && !string.IsNullOrEmpty(dict["Text"]))
                {
                    dict["Text"] = dict["Text"].Replace("{CallName}", callName).Replace("{Gender_Call}", callName);
                }
            }

            return eventLines;
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