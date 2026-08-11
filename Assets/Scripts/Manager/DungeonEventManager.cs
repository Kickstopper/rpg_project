using UnityEngine;
using System.Collections.Generic;
using Data;

namespace Manager
{
    public class DungeonEventManager : MonoBehaviour
    {
        // 완료된 1회성 이벤트의 고유 키("MapID_X_Y_EventID")를 담아두는 해시세트
        private HashSet<string> completedEvents = new HashSet<string>();

        private string currentMapID = string.Empty;

        public void SetCurrentMapID(string mapID)
        {
            currentMapID = mapID;
        }

        public (string eventID, int forceDir) CheckEvent(int x, int y, bool checkOnAttempt)
        {
            if (ManagerRoot.Dungeon == null || ManagerRoot.Dungeon.CurrentDungeonData == null)
                return (null, -1);

            CellData cell = ManagerRoot.Dungeon.CurrentDungeonData.GetCell(x, y);
            
            if (cell == null || cell.events == null || cell.events.Count == 0)
                return (null, -1);

            foreach (var ev in cell.events)
            {
                if (string.IsNullOrEmpty(ev.eventID)) continue;
                
                // 요구하는 발동 시점이 맞지 않으면 패스
                if (ev.triggerOnAttempt != checkOnAttempt) continue; 

                // 플래그 조건 검사
                if (!string.IsNullOrEmpty(ev.requiredFlag))
                {
                    bool currentFlagState = ManagerRoot.Flag.CheckFlag(ev.requiredFlag); 
                    if (currentFlagState != ev.requiredFlagState) continue; 
                }
                
                string uniqueKey = $"{currentMapID}_{x}_{y}_{ev.eventID}";

                // 1회성 이벤트인데 이미 완료되었다면 스킵
                if (!ev.isEventRepeatable && completedEvents.Contains(uniqueKey)) continue;

                // 모든 조건을 통과한 첫 번째 이벤트를 실행 확정
                if (!ev.isEventRepeatable)
                {
                    completedEvents.Add(uniqueKey);
                }

                int forceDir = ev.useForceDir ? (int)ev.evForceDir : -1;
                return (ev.eventID, forceDir);
            }

            return (null, -1); // 조건을 만족하는 이벤트가 없음
        }

        public void ResetAllEvents()
        {
            completedEvents.Clear();
            Debug.Log("[DungeonEventManager] 모든 1회성 이벤트 플래그가 초기화되었습니다.");
        }

        public List<string> GetCompletedTriggers()
        {
            return new List<string>(completedEvents);
        }

        public void ApplyCompletedTriggers(List<string> savedCompletedList)
        {
            completedEvents.Clear();
            if (savedCompletedList == null || savedCompletedList.Count == 0) return;

            foreach (string key in savedCompletedList)
            {
                completedEvents.Add(key);
            }
            Debug.Log($"[DungeonEventManager] {completedEvents.Count}개의 완료된 이벤트를 로드했습니다.");
        }
    }
}