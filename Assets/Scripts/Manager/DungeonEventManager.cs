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

        public (string eventID, int forceDir) CheckEvent(int x, int y)
        {
            // 던전 데이터가 로드되어 있지 않으면 무시
            if (ManagerRoot.Dungeon == null || ManagerRoot.Dungeon.CurrentDungeonData == null)
                return (null, -1);

            // 현재 밟은 좌표의 셀 데이터를 직접 가져옴
            CellData cell = ManagerRoot.Dungeon.CurrentDungeonData.GetCell(x, y);
            
            // 타일에 이벤트 ID가 할당되어 있지 않으면 무시
            if (cell == null || string.IsNullOrEmpty(cell.eventID))
                return (null, -1);

            string eventID = cell.eventID;
            string uniqueKey = $"{currentMapID}_{x}_{y}_{eventID}";

            // 1회성 이벤트인데 이미 예전에 완료(저장)된 기록이 있다면 스킵
            if (!cell.isEventRepeatable && completedEvents.Contains(uniqueKey))
            {
                return (null, -1);
            }

            // 이벤트가 실행될 것이므로, 1회성(보스 등)이라면 완료 목록에 즉시 추가
            if (!cell.isEventRepeatable)
            {
                completedEvents.Add(uniqueKey);
            }

            // 강제 시점 전환이 켜져있다면 해당 방향(int)을, 아니라면 -1 반환
            int forceDir = cell.useForceDir ? (int)cell.evForceDir : -1;
            
            return (eventID, forceDir);
        }

        // New Game을 시작할 때 모든 이벤트의 진행 상태를 초기화
        public void ResetAllEvents()
        {
            completedEvents.Clear();
            Debug.Log("[DungeonEventManager] 모든 1회성 이벤트 플래그가 초기화되었습니다.");
        }

        // 현재 맵에서 완료된 1회성 이벤트들의 고유 키 목록을 반환 (세이브용)
        public List<string> GetCompletedTriggers()
        {
            return new List<string>(completedEvents);
        }

        // 세이브 데이터로부터 이벤트 완료 상태를 복구 (로드용)
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