using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Manager
{
    [System.Serializable]
    public class TerminalData
    {
        public string terminalID;     // 터미널 고유 ID (예: "Terminal_Cave_1")
        public string displayName;    // UI에 표시될 이름 (예: "지하 동굴 1층 거점")
        public string mapID;          // 이동할 맵의 JSON 파일 이름
        public int targetX;           // 도착 시 플레이어 X 좌표
        public int targetY;           // 도착 시 플레이어 Y 좌표
        public Direction targetDir;   // 도착 시 플레이어 방향 (보통 터미널 밖을 바라보게 설정)
        public Sprite destinationImage; // 상단 이미지
        public int floorNumber;         // 층수 표시용
        [TextArea]
        public string description;      // 장소 특징/설명
    }

    public class TerminalManager : MonoBehaviour
    {
        [Header("전체 터미널 DB")]
        public List<TerminalData> allTerminals = new List<TerminalData>();

        // 플레이어가 지금까지 방문해서 활성화시킨 터미널 ID 목록 (저장/불러오기 대상)
        private HashSet<string> _unlockedTerminals = new HashSet<string>();

        // 터미널 방에 진입했을 때 호출하여 해금
        public void UnlockTerminal(string terminalID)
        {
            if (!_unlockedTerminals.Contains(terminalID))
            {
                _unlockedTerminals.Add(terminalID);
                Debug.Log($"[Terminal] 새로운 터미널 활성화: {terminalID}");
                // TODO: 화면에 "새로운 거점이 등록되었습니다." 같은 메시지를 띄워도 좋습니다.
            }
        }

        // 현재 위치한 터미널을 제외하고, 해금된 터미널 목록만 UI용으로 반환
        public List<TerminalData> GetAvailableTerminals(string currentTerminalID)
        {
            List<TerminalData> available = new List<TerminalData>();
            foreach (var terminal in allTerminals)
            {
                //if (terminal.terminalID != currentTerminalID && _unlockedTerminals.Contains(terminal.terminalID))
                {
                    available.Add(terminal);
                }
            }
            return available;
        }

        // 세이브/로드용 (필요시 사용)
        public List<string> GetUnlockedList() => new List<string>(_unlockedTerminals);
        public void LoadUnlockedList(List<string> savedList) => _unlockedTerminals = new HashSet<string>(savedList ?? new List<string>());
    }
}