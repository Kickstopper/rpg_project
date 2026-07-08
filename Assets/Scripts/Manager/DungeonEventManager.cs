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
                
                
                LoadTriggerData();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        public void SetCurrentMapID(string mapID)
        {
            currentMapID = mapID;
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

        // New Game을 시작할 때 모든 이벤트의 진행 상태를 초기화합니다.
        public void ResetAllEvents()
        {
            foreach (List<EventTriggerData> eventList in triggerMap.Values)
            {
                foreach (EventTriggerData trigger in eventList)
                {
                    trigger.IsCompleted = false;
                }
            }
            
            Debug.Log("[DungeonEventManager] 모든 1회성 이벤트 플래그가 초기화되었습니다.");
        }

        // 현재 맵에서 완료된 1회성 이벤트들의 고유 키 목록을 반환 (세이브용)
        public List<string> GetCompletedTriggers()
        {
            List<string> completedList = new List<string>();

            // triggerMap의 Key는 "MapID_X_Y" 형태
            foreach (var kvp in triggerMap)
            {
                foreach (EventTriggerData trigger in kvp.Value)
                {
                    // 반복 가능하지 않은 1회성 이벤트 중, 이미 완료된 것만 저장
                    if (!trigger.Repeatable && trigger.IsCompleted)
                    {
                        // 고유 식별자 생성: "MapID_X_Y_EventID"
                        string uniqueKey = $"{kvp.Key}_{trigger.EventID}";
                        completedList.Add(uniqueKey);
                    }
                }
            }
            
            return completedList;
        }

        // 세이브 데이터로부터 이벤트 완료 상태를 복구 (로드용)
        public void ApplyCompletedTriggers(List<string> savedCompletedList)
        {
            // 혹시 모를 찌꺼기 데이터를 막기 위해 일단 전부 초기화
            ResetAllEvents();

            // 세이브 데이터가 비어있다면 여기서 종료
            if (savedCompletedList == null || savedCompletedList.Count == 0) return;

            // 맵 데이터를 순회하며 저장된 기록이 있는지 확인 후 상태 복구
            foreach (var kvp in triggerMap)
            {
                foreach (EventTriggerData trigger in kvp.Value)
                {
                    string uniqueKey = $"{kvp.Key}_{trigger.EventID}";
                    
                    // 세이브 파일 리스트에 이 고유 키가 존재한다면 완료 처리
                    if (savedCompletedList.Contains(uniqueKey))
                    {
                        trigger.IsCompleted = true;
                    }
                }
            }

            Debug.Log($"[DungeonEventManager] {savedCompletedList.Count}개의 완료된 이벤트를 로드했습니다.");
        }

    }
}
