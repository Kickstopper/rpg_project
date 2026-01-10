using System;
using System.Collections.Generic;
namespace Data
{
    public enum Direction { North, East, South, West }
    
    [Serializable]
    public class MapData
    {
        public int width;
        public int height;
        public int mapID;
        public string themeName; // JSON에서 불러올 테마 이름
        
        public int startX; // 플레이어의 시작 좌표
        public int startY; // 플레이어의 시작 좌표
        public Direction startDirection;

        public CellData[] cells;

        // 이 맵에 존재하는 모든 워프 포인트 목록
        public List<WarpData> warps = new List<WarpData>();

        // 헬퍼 함수: 특정 좌표에 워프가 있는지 확인
        public WarpData GetWarpAt(int x, int y)
        {
            // 리스트에서 해당 좌표(sourceX, sourceY)를 가진 워프 데이터를 찾음
            return warps.Find(w => w.sourceX == x && w.sourceY == y);
        }
        
        // 1차원 배열을 2차원 좌표로 접근하기 편하게 돕는 헬퍼 함수
        public CellData GetCell(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return null;
            return cells[y * width + x];
        }
    }

    [Serializable]
    public class CellData
    {
        public int x, y;
        public int value = 0;
        public int[] wallTextureIDs = new int[4] { -1, -1, -1, -1 }; // left, up, right, down
        public bool HasWall()
        {
            foreach(var i in wallTextureIDs)
            {
                if (i > -1) return true;
            }
            return false;
        }
    }

    // 워프 정보를 담을 데이터 클래스
    [Serializable]
    public class WarpData
    {
        // 워프가 위치한 좌표 (벽의 좌표)
        public int sourceX;
        public int sourceY;
        
        // true면 벽에 부딪혔을 때 발동, false면 해당 타일을 밟았을 때 발동
        public bool isWallWarp; 
        // 워프 발동을 위한 진입 방향 (플레이어가 어느 방향으로 움직이다 벽을 쳤는가?)
        public Direction triggerDirection;

        public string targetMapName; // 이동할 맵 이름
        public int targetX;          // 이동 후 스폰될 X
        public int targetY;          // 이동 후 스폰될 Y
        public Direction targetDirection; // 이동 후 바라볼 방향
    }
}
