using System;
using System.Collections.Generic;
using UnityEngine;
namespace Data
{
    public enum Direction { North, East, South, West }
    public enum EntranceType { Map, Shop, Elevator, Terminal, Office, FieldMap, RandomMaze } // 던전맵, 상점, 엘리베이터, 터미널, 오피스, 필드맵, 랜덤생성던전맵
    public enum ElevatorDoorType { Split, SlideLeft, SlideUp }

    // 지역 이동 목적지 데이터 구조
    [Serializable]
    public class FieldMapDestData
    {
        public string mapID;           // 대상 맵 ID
        public string displayName;     // UI에 표시될 지역 이름
        public float distance;         // 거리 (km)
        public float timeHours;          // 소요 게임 시간 (시간 단위)
        
        public int targetX;
        public int targetY;
        public Direction targetDir;
    }
    
    [Serializable]
    public class RouteData
    {
        public string fromMapID;      // 출발지 맵 ID
        public string toMapID;        // 도착지 맵 ID
        
        public float distance;        // 이동 거리
        public float timeHours;         // 소요 시간
    }

    [Serializable]
    public class MapNodeData
    {
        public Sprite backgroundImage;
        public string mapID;          // 맵 ID (Key)
        public string displayName;    // 화면에 표시될 이름
        
        [Header("Default Spawn Info")]
        public int spawnX;
        public int spawnY;
        public Direction spawnDir;
    }

    [Serializable]
    public struct FloorData 
    {
        public int floorNumber;         // 정렬 및 내부 로직용 층수 (예: -1, 1, 2)
        public string displayName;      // UI에 표시될 이름 (예: "B1", "1F", "옥상")
        
        public string mapID;            // 이동할 맵의 ID
        public int mapX;                // 스폰될 X 좌표
        public int mapY;                // 스폰될 Y 좌표
        public Direction targetDirection; // 내렸을 때 바라볼 방향
    }

    [Serializable]
    public class ElevatorData 
    {
        public string id;               // 엘리베이터 고유 ID (entrance.destinationID 와 매칭)

        public int minFloor; // 추가: 최저층 (예: -2)
        public int maxFloor; // 추가: 최고층 (예: 5)
        public ElevatorDoorType doorType = ElevatorDoorType.Split;
        public FloorData[] floorData;   // 이 엘리베이터에서 갈 수 있는 층 목록

        public string GetDisplayName(string mapID)
        {
            if (floorData == null) return string.Empty;

            foreach(var f in floorData)
            {
                if (f.mapID == mapID)
                    return f.displayName;
            }
            return string.Empty;
        }
    }

    [Serializable]
    public class MapData
    {
        public int width;
        public int height;
        public string mapID;
        public string themeID; // JSON에서 불러올 테마의 ID
        public string locationID; // 여러 맵이 공유하는 상위 지역의 ID. 퀘스트 등에서 참조
        public bool hasCeil = true;
        public int startX; // 플레이어의 시작 좌표
        public int startY; // 플레이어의 시작 좌표
        public Direction startDirection;

        public CellData[] cells;

        // 이 맵에 존재하는 모든 입구 포인트 목록
        public List<EntranceData> entrances = new List<EntranceData>();

        // 헬퍼 함수: 특정 좌표에 입구가 있는지 확인
        public EntranceData GetEntranceAt(int x, int y)
        {
            // 리스트에서 해당 좌표(sourceX, sourceY)를 가진 입구 데이터를 찾음
            return entrances.Find(w => w.sourceX == x && w.sourceY == y);
        }
        
        // 1차원 배열을 2차원 좌표로 접근하기 편하게 돕는 헬퍼 함수
        public CellData GetCell(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return null;
            return cells[y * width + x];
        }
    }

    // 다중 이벤트를 처리하기 위한 새로운 데이터 구조
    [Serializable]
    public class CellEventData
    {
        public string eventID = "";
        public string requiredFlag = "";
        public bool requiredFlagState = true;
        public bool isEventRepeatable = false;
        
        // 이벤트 발동 시점
        // false: 해당 칸으로 이동을 완료한 직후 발생
        // true: 해당 칸으로 이동을 시도할 때 발생
        public bool triggerOnAttempt = false;
        public bool useForceDir = false;
        public Direction evForceDir = Direction.North;
    }

    [Serializable]
    public class CellData
    {
        public int x, y;
        public int value = 0;
        public int[] wallTextureIDs = new int[4] { -1, -1, -1, -1 }; // up, right, down, left
        public int floorTexIdx = -1; // -1이면 DungeonTheme의 floorTexIdx를 따름
        public int ceilTexIdx = -1;  // -1이면 DungeonTheme의 ceilTexIdx를 따름
        
        public int centerObjectID = -1;
        public int[] faceObjectIDs = new int[4] { -1, -1, -1, -1 }; // North, East, South, West
        
        public List<CellEventData> events = new List<CellEventData>(); // 다중 이벤트 처리

        [Header("Object Interaction")]
        public bool canInteract = false;               // 상호작용 가능 여부
        
        // 조건 및 상태 저장 플래그
        public string interactReqFlag = "";            // 이 상호작용을 위해 필요한 선행 플래그
        public bool interactReqFlagState = true; 
        public string interactSetFlag = "";            // 상호작용 완료 시 켤/끌 플래그 (영구 저장용)
        public bool interactSetFlagState = true;   
        
        // 시각적 변화 및 이벤트
        public int interactTargetTexID = -1;           // 상호작용의 대상이 되는 특정 텍스처 ID (-1이면 제한 없음)
        public int interactChangeObjectID = -1;        // 상호작용 후 변경될 텍스처 ID (-1이면 유지)
        public string interactEventID = "";            // 아이템 획득, 맵 전환 등을 처리할 실제 이벤트 ID
        public string interactSystemMessage = "조사했다."; // 연결된 이벤트가 없을 때 출력할 기본 텍스트
        
        public bool HasWall()
        {
            foreach(var i in wallTextureIDs)
            {
                if (i > -1) return true;
            }
            return false;
        }
    }

    public enum StairType
    {
        None,       // 일반 포탈 (바로 이동)
        Upstairs,   // 올라가는 계단 연출
        Downstairs  // 내려가는 계단 연출
    }

    // 입구 정보를 담을 데이터 클래스
    [Serializable]
    public class EntranceData
    {
        public EntranceType type;

        // 입구가 위치한 좌표 (벽의 좌표)
        public int sourceX;
        public int sourceY;
        
        // true면 벽에 부딪혔을 때 발동, false면 해당 타일을 밟았을 때 발동
        public bool isWallEntrance; 
        // 입구 발동을 위한 진입 방향 (플레이어가 어느 방향으로 움직이다 벽을 쳤는가?)
        public StairType stairType = StairType.None; // 계단 연출 타입 지정
        public bool isWorldMap;
        public string destinationID; // 도착 장소 ID (맵이면 맵 ID, 상점이면 상점 ID, 월드맵이면 regionId)
        
        public int targetX = -1;          // 이동 후 스폰될 X
        public int targetY = -1;          // 이동 후 스폰될 Y
        public Direction targetDirection; // 이동 후 바라볼 방향 (DungeonMapScene만 사용)

        // Random Maze(절차적 랜덤 맵) 전용 파라미터
        [Header("Random Maze Settings")]
        public string randomMapThemeID = "";
        public int randomMapWidth = 11;
        public int randomMapHeight = 11;
        public int randomMapMaxCount = 5;    // 고정된 전체 층수 (찌꺼기 데이터 검사 및 UI 참조용)
        public int randomMapRepeatCount = 5; // 차감되는 남은 층수 (0이 되면 최종 목적지로)
        public string finalDestinationID = ""; // 돌파 후 귀환할 맵 ID
    }
}
