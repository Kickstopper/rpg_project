using System;
namespace Data
{
    [Serializable]
    public class DungeonMapState
    {
        public string mapID;
        public int width;
        public int height;

        // 방문 여부 체크 (true: 밝혀짐, false: 안개)
        public bool[] visitedCells; 

        public DungeonMapState(int w, int h, string id)
        {
            width = w;
            height = h;
            mapID = id;
            visitedCells = new bool[w * h];
        }

        // 특정 좌표를 방문 처리하는 함수
        public void MarkVisited(int x, int y)
        {
            if (IsValid(x, y))
            {
                visitedCells[y * width + x] = true;
            }
        }

        // 방문 여부 확인 함수
        public bool IsVisited(int x, int y)
        {
            if (!IsValid(x, y)) return false;
            return visitedCells[y * width + x];
        }

        private bool IsValid(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }
    }
}
