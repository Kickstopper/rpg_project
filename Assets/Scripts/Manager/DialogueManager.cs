using System.Collections.Generic;
using UnityEngine;
namespace Manager
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance;

        private List<Dictionary<string, string>> allDialogueData;
        
        private Dictionary<string, List<Dictionary<string, string>>> eventDatabase;

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
            // Resources/Event_Scripts.csv 로드
            TextAsset csvFile = Resources.Load<TextAsset>("Event_Scripts");
            
            if (csvFile == null)
            {
                Debug.LogError("CSV File not found in Resources folder!");
                return;
            }

            allDialogueData = CSVReader.Read(csvFile);
            
            // 데이터를 EventID 기준으로 그룹화하여 Dictionary에 저장
            eventDatabase = new Dictionary<string, List<Dictionary<string, string>>>();

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
            
            Debug.Log($"Dialogue Database Loaded. Total Rows: {allDialogueData.Count}");
        }

        public List<Dictionary<string, string>> GetEventData(string eventID)
        {
            if (eventDatabase.ContainsKey(eventID))
                return eventDatabase[eventID];
            
            Debug.LogWarning($"Event ID '{eventID}' not found.");
            return new List<Dictionary<string, string>>();
        }
    }
}
