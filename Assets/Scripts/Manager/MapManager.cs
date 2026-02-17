using System.Collections.Generic;
using System.Linq; // List 변환을 위해 필요
using Data;
using UnityEngine;

namespace Manager
{
    public class MapManager : MonoBehaviour
    {
        public static MapManager Instance;

        [Header("Current Session Data")]
        public string currentDungeonId; // 현재 맵 ID
        public int currentPx { get; private set; }    
        public int currentPy { get; private set; }    
        public Direction currentDirection { get; private set; } 

        // 모든 맵의 방문 상태를 관리하는 Dictionary (Key: MapID)
        private Dictionary<string, DungeonMapState> _mapStates = new Dictionary<string, DungeonMapState>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
        }

        /// <summary>
        /// 플레이어의 위치 정보를 갱신.
        /// </summary>
        public void UpdatePlayerPosition(int px, int py, Direction direction, string dungeonID)
        {
            currentPx = px;
            currentPy = py;
            currentDirection = direction;
            currentDungeonId = dungeonID;
        }

        // =========================================================
        // 맵 상태 관리 (State Management)
        // =========================================================

        /// <summary>
        /// 특정 맵 ID에 해당하는 상태가 있는지 확인하고 반환.
        /// (LevelManager에서 맵 로드 시 호출하여 기존 방문 기록을 가져옴)
        /// </summary>
        public DungeonMapState GetMapState(string mapID)
        {
            if (_mapStates.ContainsKey(mapID))
            {
                return _mapStates[mapID];
            }
            return null; // 방문한 적 없음
        }

        /// <summary>
        /// 새로운 맵 상태를 등록하거나, 기존 상태를 갱신.
        /// (새 던전에 처음 진입하여 DungeonMapState를 새로 생성했을 때 호출)
        /// </summary>
        public void RegisterMapState(DungeonMapState state)
        {
            if (state == null || string.IsNullOrEmpty(state.mapID)) return;

            if (_mapStates.ContainsKey(state.mapID))
            {
                _mapStates[state.mapID] = state;
            }
            else
            {
                _mapStates.Add(state.mapID, state);
            }
        }

        // =========================================================
        // 저장 및 불러오기 지원 (Save/Load Support)
        // =========================================================

        /// <summary>
        /// 저장(Save): Dictionary에 있는 모든 맵 상태를 List 형태로 반환.
        /// SaveManager.SaveGame()에서 호출.
        /// </summary>
        public List<DungeonMapState> GetAllMapStates()
        {
            // Dictionary의 Values를 리스트로 변환하여 반환
            return _mapStates.Values.ToList();
        }

        /// <summary>
        /// 불러오기(Load): 저장된 List 데이터를 받아 Dictionary를 재구축.
        /// SaveManager.LoadGame()에서 호출.
        /// </summary>
        public void LoadMapStates(List<DungeonMapState> loadedStates)
        {
            _mapStates.Clear(); // 기존 데이터 초기화

            if (loadedStates == null) return;

            foreach (var state in loadedStates)
            {
                if (!string.IsNullOrEmpty(state.mapID) && !_mapStates.ContainsKey(state.mapID))
                {
                    _mapStates.Add(state.mapID, state);
                }
            }
            
            Debug.Log($"[MapManager] {_mapStates.Count}개의 맵 상태가 로드되었습니다.");
        }

        // =========================================================
        // 유틸리티
        // =========================================================

        public void ResetPositionData()
        {
            currentPx = 0;
            currentPy = 0;
            currentDungeonId = string.Empty;
            // 주의: _mapStates(방문 기록)는 여기서 초기화하지 않음. (게임 오버 시 유지하거나 별도 초기화 필요)
        }
        
        /// <summary>
        /// 새 게임 시작 시 모든 맵 데이터를 날려야 할 경우 사용
        /// </summary>
        public void ClearAllMapData()
        {
            _mapStates.Clear();
            ResetPositionData();
        }
    }
}