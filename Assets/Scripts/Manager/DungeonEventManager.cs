using UnityEngine;
using System.Collections.Generic;
using Data;

namespace Manager
{
    public class DungeonEventManager : MonoBehaviour
    {
        public static DungeonEventManager Instance;

        // Key: "FloorID_X_Y" 형태의 문자열 (예: "F1_3_5")
        // Value: 트리거 정보
        private Dictionary<string, EventTriggerData> triggerMap = new Dictionary<string, EventTriggerData>();

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
            // Map_Triggers.csv 로드
            var rawData = CSVReader.Read(Resources.Load<TextAsset>("Map_Triggers"));

            foreach (var row in rawData)
            {
                // CSV 컬럼: MapID, X, Y, EventID, Repeatable
                string mapID = row["MapID"];
                string x = row["X"];
                string y = row["Y"];
                string key = $"{mapID}_{x}_{y}"; // 고유 키 생성

                bool repeatable = row["Repeatable"].ToUpper() == "TRUE";
                string eventID = row["EventID"];

                triggerMap[key] = new EventTriggerData(eventID, repeatable);
            }
        }

        // 플레이어가 이동을 마칠 때마다 호출
        public string CheckEvent(int x, int y)
        {
            string key = $"{currentMapID}_{x}_{y}";

            string eventID = null;

            if (triggerMap.ContainsKey(key))
            {
                EventTriggerData trigger = triggerMap[key];

                // 이미 완료된 1회성 이벤트라면 무시
                if (!trigger.Repeatable && trigger.IsCompleted)
                {
                    return null;
                }

                // 이벤트 발생! -> DialogueManager에게 "내용 재생해줘"라고 요청
                Debug.Log($"Event Triggered: {trigger.EventID}");
                // 상태 업데이트
                trigger.IsCompleted = true;
                
                return trigger.EventID;
            }
            return eventID;
        }

        // 층 이동 시 호출
        public void SetCurrentMapID(string mapID)
        {
            currentMapID = mapID;
        }
    }
}
