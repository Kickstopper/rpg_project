using System.Collections.Generic;

namespace Utilities {
    public enum Grid
    {
        CORRIDOR = 0,
        DOOR = 1,
        ENTRANCE = 2, // Start
        EXIT = 3,
        WALL = 4
    }
    
    public class MapGenerator
    {

        private static System.Random rand = new System.Random();
        
        /**
        * width와 height는 반드시 홀수여야 함
        * startX는 반드시 0 이상이고 width 미만이어야 함
        * startY는 반드시 0 이상이고 height 미만이어야 함
        */
        public static int[,] GenerateMap(int width, int height, int startX, int startY)
        {
            var map = new int[height, width];

            for (int i = 0; i < height; i++) {
                for (int j = 0; j < width; j++) {
                    map[i, j] = (int)Grid.WALL; // 모두 벽으로 초기화
                }
            }

            // DFS 알고리듬으로 미로 생성
            var stack = new Stack<(int, int)>();

            map[startY, startX] = (int)Grid.ENTRANCE; // 시작점 설정
    
            stack.Push((startX, startY));

            while (stack.Count > 0) {
                var (x, y) = stack.Peek();
                var neighbors = GetUnvisitedNeighbors(map, x, y);

                if (neighbors.Count > 0) {
                    var (nx, ny) = neighbors[rand.Next(neighbors.Count)];
                    map[(y + ny) / 2, (x + nx) / 2] = (int)Grid.CORRIDOR; // 두 칸 사이를 통로로 설정
                    map[ny, nx] = (int)Grid.CORRIDOR; // 새로운 칸을 통로로 설정
                    stack.Push((nx, ny)); // 새로운 칸으로 이동
                }
                else {
                    stack.Pop(); // 더 이상 확장할 수 없는 경우 이전으로 돌아감
                }
            }

            map = WrapMapWithBorder(map, (int)Grid.WALL); // 맵의 외곽을 벽으로 감싼다

            var endPointList = new List<(int, int)>();
            var preferEndPoints = new List<(int, int)>();
            for (int i = 0; i < height; i++) {
                for (int j = 0; j < width; j++) {
                    if (map[i, j] == (int)Grid.CORRIDOR) //도착점 후보는 반드시 Corridor이며 한 방향만 뜷려있어야 한다 
                    {
                        // 도착점이 될 수 있는 통로 좌표를 모두 추가
                        if (IsExitLocation(map, j, i)) preferEndPoints.Add((j, i));
                        else endPointList.Add((j, i));
                    }
                }
            }

            var endPoints = preferEndPoints.Count > 0 ? preferEndPoints : endPointList;

            // 도착점 좌표 리스트 중 하나를 랜덤하게 선택
            var (endX, endY) = endPoints[rand.Next(endPoints.Count)];
            map[endY, endX] = (int)Grid.EXIT;

            // 시작 좌표와 출구 좌표가 연결되어 있는지 확인 후, 연결되지 않았으면 강제로 연결시킴
            if (!IsPathConnected(map, startX, startY, endX, endY)) {
                ConnectPath(map, startX, startY, endX, endY);
            }

            return map;
        }

        /*
         * 주변 8방향을 체크하여 Exit가 한 쪽만 뚫려있는지 확인하는 메서드
         */
        private static bool IsExitLocation(int[,] map, int x, int y)
        {
            int wallCount = 0;
            
            for (int dy = -1; dy <= 1; dy++) {
                for (int dx = -1; dx <= 1; dx++) {
                    if (dy == 0 && dx == 0) continue; // 현재 위치는 제외 

                    int nx = x + dx;
                    int ny = y + dy;

                    // 주어진 좌표 주변 8방향을 체크하여 벽의 개수를 센다
                    if (nx >= 0 && nx < map.GetLength(1) && ny >= 0 && ny < map.GetLength(0)) {
                        if (map[ny, nx] == (int)Grid.WALL) {
                            wallCount++;
                        }
                    }
                }
            }
            
            return wallCount >= 7; // 1면을 제외한 나머지가 벽이면 적합 판정 
        }

        /**
        * 2차원 배열의 외곽 요소를 임의의 int로 감싸서 반환함
        */
        private static int[,] WrapMapWithBorder(int[,] map, int borderGrid)
        {
            int oldRows = map.GetLength(0);
            int oldCols = map.GetLength(1);
            
            // 주어진 맵 배열의 외곽을 감쌀 수 있도록 각 행과 열의 요소가 2씩 커진 배열을 새로 생성함
            int newRows = oldRows + 2;
            int newCols = oldCols + 2;
            int[,] newMap = new int[newRows, newCols];

            for (int row = 0; row < newRows; row++) {
                for (int col = 0; col < newCols; col++) {
                    if (row == 0 || row == newRows - 1 || col == 0 || col == newCols - 1) {
                        newMap[row, col] = borderGrid; // 외곽을 임의의 요소로 채움
                    }
                    else {
                        newMap[row, col] = map[row - 1, col - 1]; // 내부는 기존 배열의 요소와 같은 값으로 채움
                    }
                }
            }

            return newMap;
        }

        /**
        * 확인하지 않은 칸을 반환
        */
        private static List<(int x, int y)> GetUnvisitedNeighbors(int[,] map, int x, int y)
        {
            var neighbors = new List<(int, int)>();

            // 상하좌우 방향 검사
            if (y > 1 && map[y - 2, x] == (int)Grid.WALL) neighbors.Add((x, y - 2)); // 위
            if (y < map.GetLength(0) - 2 && map[y + 2, x] == (int)Grid.WALL) neighbors.Add((x, y + 2)); // 아래
            if (x > 1 && map[y, x - 2] == (int)Grid.WALL) neighbors.Add((x - 2, y)); // 왼쪽
            if (x < map.GetLength(1) - 2 && map[y, x + 2] == (int)Grid.WALL) neighbors.Add((x + 2, y)); // 오른쪽

            return neighbors;
        }


        /**
        * 통로가 연결되어 있는지를 확인
        */
        private static bool IsPathConnected(int[,] map, int startX, int startY, int endX, int endY)
        {
            int height = map.GetLength(0);
            int width = map.GetLength(1);
            var visited = new bool[height, width];
            var queue = new Queue<(int, int)>();
            queue.Enqueue((startX, startY));

            while (queue.Count > 0) {
                var (x, y) = queue.Dequeue();
                if (x == endX && y == endY) return true; // 도착점까지 통로가 연결되어있음

                foreach (var (nx, ny) in GetNeighbors(map, x, y)) {
                    if (!visited[ny, nx]) {
                        visited[ny, nx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            return false; // 연결된 통로가 없음
        }

        /**
        * BFS 알고리듬으로 통로를 연결함
        */
        private static void ConnectPath(int[,] map, int startX, int startY, int endX, int endY)
        {
            var queue = new Queue<(int, int)>();
            queue.Enqueue((startX, startY));
            var visited = new bool[map.GetLength(0), map.GetLength(1)];

            while (queue.Count > 0) {
                var (x, y) = queue.Dequeue();

                foreach (var (nx, ny) in GetNeighbors(map, x, y)) {
                    if (!visited[ny, nx]) {
                        visited[ny, nx] = true;
                        if (nx == endX && ny == endY) {
                            map[(y + ny) / 2, (x + nx) / 2] = (int)Grid.CORRIDOR; // wall을 Corridor로 바꿈
                            return;
                        }

                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }

        /**
        * 체크하지 않은 이웃한 경로를 반환
        */
        private static List<(int x, int y)> GetNeighbors(int[,] map, int x, int y)
        {
            List<(int, int)> neighbors = new List<(int, int)>();
            if (y > 0 && map[y - 1, x] != (int)Grid.WALL) neighbors.Add((x, y - 1)); // 위
            if (y < map.GetLength(0) - 1 && map[y + 1, x] != (int)Grid.WALL) neighbors.Add((x, y + 1)); // 아래
            if (x > 0 && map[y, x - 1] != (int)Grid.WALL) neighbors.Add((x - 1, y)); // 왼쪽
            if (x < map.GetLength(1) - 1 && map[y, x + 1] != (int)Grid.WALL) neighbors.Add((x + 1, y)); // 오른쪽
            return neighbors;
        }
    }
}
