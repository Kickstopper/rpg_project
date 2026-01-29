using Data;
using UnityEngine;

namespace Manager
{
    public class MapManager : MonoBehaviour
    {
        public static MapManager Instance;

        [Header("Current Session Data")]
        public string currentDungeonId; // 현재 맵 ID (SaveData에 저장될 값)
        public int currentPx { get; private set; }    // 저장되거나 이동 전의 마지막 그리드 x좌표
        public int currentPy { get; private set; }    // 저장되거나 이동 전의 마지막 그리드 y좌표
        public Direction lastDirection { get; private set; } // 플레이어가 바라보는 방향 (North, East, South, West)

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
        }

        public void UpdatePlayerPosition(int px, int py, Direction direction, string dungeonID)
        {
            currentPx = px;
            currentPy = py;
            this.lastDirection = direction;
            currentDungeonId = dungeonID;
        }

        public void ResetPositionData()
        {
            currentPx = 0;
            currentPy = 0;
            currentDungeonId = string.Empty;
        }
    }
}