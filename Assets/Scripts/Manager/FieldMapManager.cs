using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Manager
{
    public class FieldMapManager : MonoBehaviour
    {
        [Header("전체 월드맵 목적지 DB")]
        public List<FieldMapDestData> allDestinations = new List<FieldMapDestData>();

        // 스토리 진행에 따라 해금된 지역 ID 목록
        private HashSet<string> _unlockedDestinations = new HashSet<string>();

        void Start()
        {
            UnlockDestination("Outpost");
            UnlockDestination("Tower_0");
        }

        // 새로운 지역 해금
        public void UnlockDestination(string mapID)
        {
            if (!_unlockedDestinations.Contains(mapID))
            {
                _unlockedDestinations.Add(mapID);
                Debug.Log($"[FieldMap] 새로운 지역 해금: {mapID}");
            }
        }

        // 현재 이동 가능한(해금된) 목적지 목록만 UI용으로 반환
        public List<FieldMapDestData> GetAvailableDestinations(string currentMapID)
        {
            List<FieldMapDestData> available = new List<FieldMapDestData>();
            foreach (var dest in allDestinations)
            {
                // 자기 자신(현재 맵)은 제외하고, 해금된 지역만 목록에 추가
                if (dest.mapID != currentMapID && _unlockedDestinations.Contains(dest.mapID))
                {
                    available.Add(dest);
                }
            }
            return available;
        }

        // 세이브/로드용 메서드
        public List<string> GetUnlockedList() => new List<string>(_unlockedDestinations);
        public void LoadUnlockedList(List<string> savedList) => _unlockedDestinations = new HashSet<string>(savedList ?? new List<string>());
    }
}