using UnityEngine;
using System.Collections.Generic;
using Data;

namespace Manager
{
    public class DungeonEventManager : MonoBehaviour
    {
        public static DungeonEventManager Instance;

        [Header("Data Files")]
        public TextAsset mapTriggerCSV; // 인스펙터에서 할당

        private Dictionary<string, List<EventTriggerData>> triggerMap = new Dictionary<string, List<EventTriggerData>>();

        private string currentMapID = string.Empty; // 현재 층 ID

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                LoadTriggerData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void LoadTriggerData()
        {
            var rawData = CSVReader.Read(mapTriggerCSV);

            foreach (var row in rawData)
            {
                string mapID = row["MapID"];
                string x = row["X"];
                string y = row["Y"];
                string key = $"{mapID}_{x}_{y}";

                bool repeatable = row.ContainsKey("Repeatable") && row["Repeatable"].ToUpper() == "TRUE";
                string eventID = row.ContainsKey("EventID") ? row["EventID"] : "";

                int forceDir = -1;
                if (row.ContainsKey("ForceDir") && int.TryParse(row["ForceDir"], out int parsedDir))
                {
                    forceDir = parsedDir;
                }

                EventTriggerData newData = new EventTriggerData(eventID, repeatable, forceDir);

                if (!triggerMap.ContainsKey(key)) 
                    triggerMap[key] = new List<EventTriggerData>();

                triggerMap[key].Add(newData);
            }
        }

        public (string eventID, int forceDir) CheckEvent(int x, int y)
        {
            string key = $"{currentMapID}_{x}_{y}";

            if (triggerMap.ContainsKey(key))
            {
                List<EventTriggerData> eventsAtLocation = triggerMap[key];

                foreach (EventTriggerData trigger in eventsAtLocation)
                {
                    // 이미 완료된 1회성 이벤트는 무시하고 다음 이벤트로 넘김
                    if (!trigger.Repeatable && trigger.IsCompleted)
                    {
                        continue;
                    }

                    // 실행 조건을 만족하는 첫 번째 이벤트를 완료 처리함
                    trigger.IsCompleted = true;
                    
                    return (trigger.EventID, trigger.ForceDir);
                }
            }

            return (null, -1); 
        }

        public void SetCurrentMapID(string mapID)
        {
            currentMapID = mapID;
        }
    }
}
