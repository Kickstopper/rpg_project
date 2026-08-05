using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Helper
{
    public static class DungeonMapHelper
    {
        public static List<(int x, int y)> GetShortestPath(int[,] map, (int x, int y) start, (int x, int y) end)
        {
            int row = map.GetLength(0);
            int col = map.GetLength(1);

            if (!IsInBounds(start, row, col) || !IsInBounds(end, row, col)) {
                throw new ArgumentOutOfRangeException("Start or end point is out of bounds.");
            }

            if (map[start.x, start.y] > (int)Utilities.Grid.CORRIDOR || map[end.x, end.y] > (int)Utilities.Grid.CORRIDOR) {
                return new List<(int x, int y)>(); // 이동 불가
            }

            (int dx, int dy)[] directions = { (0, 1), (1, 0), (0, -1), (-1, 0) };

            var queue = new Queue<(int x, int y)>();
            var visitedNodes = new HashSet<(int x, int y)>();
            var parents = new Dictionary<(int x, int y), (int x, int y)?>();

            queue.Enqueue(start);
            visitedNodes.Add(start);
            parents[start] = null;

            while (queue.Count > 0) {
                var current = queue.Dequeue();
                if (current == end) {
                    return GetOrderedPath(parents, start, end);
                }

                foreach (var direction in directions) {
                    int newX = current.x + direction.dx;
                    int newY = current.y + direction.dy;

                    if (IsMovePossible(newX, newY, row, col, map) &&
                        !visitedNodes.Contains((newX, newY))) {
                        queue.Enqueue((newX, newY));
                        visitedNodes.Add((newX, newY));
                        parents[(newX, newY)] = current;
                    }
                }
            }

            return new List<(int x, int y)>(); // 이동 불가
        }

        private static List<(int x, int y)> GetOrderedPath(Dictionary<(int x, int y), (int x, int y)?> parents, (int x, int y) start, (int x, int y) end)
        {
            var path = new List<(int x, int y)>();
            (int x, int y)? current = end;

            while (current.HasValue) {
                path.Add(current.Value);
                current = parents[current.Value]; // 부모 노드를 통해 경로 추적
            }

            path.Reverse(); // 경로를 역순으로 정렬
            return path;
        }

        /**
         * 좌표(point)가 맵의 범위(row, col) 안에 있는지 체크
         */
        private static bool IsInBounds((int x, int y) point, int row, int col)
        {
            return point.x >= 0 && point.x < row && point.y >= 0 && point.y < col;
        }

        private static bool IsMovePossible(int x, int y, int row, int col, int[,] map)
        {
            return IsInBounds((x, y), row, col) && map[x, y] <= (int)Utilities.Grid.CORRIDOR;
        }

        /**
         * 특정 타입의 그리드 좌표를 찾아 반환
         */
        public static bool GetTypedGridPosition(int[,] map, int grid, out (int x, int y) position)
        {
            position = (-1, -1); // 디폴트값

            int row = map.GetLength(0);
            int col = map.GetLength(1);

            for (int i = 0; i < row; i++) {
                for (int j = 0; j < col; j++) {
                    if (map[i, j] == grid) {
                        position = (i, j);
                        return true;
                    }
                }
            }

            return false;
        }

        /**
        * 콘솔에 자동으로 생성된 맵을 그린다
        */
        public static void DrawRandomGeneratedMap(int[,] map)
        {
            var start = "S";
            var end = "E";
            var corridor = " ";
            var wall = "H";

            var line = string.Empty;
            for (int i = 0; i < map.GetLength(0); i++) {
                for (int j = 0; j < map.GetLength(1); j++) {
                    int grid = map[i, j];
                    line += grid == (int)Utilities.Grid.WALL ? wall : (grid == (int)Utilities.Grid.CORRIDOR ? corridor : (grid == (int)Utilities.Grid.EXIT ? end : start));
                }
                line += "\n";
            }
            Debug.Log(line);
        }

        /**
        * 콘솔에 맵을 그린다
        */
        public static void DrawMap(int[,] map)
        {
            var corridor = " ";
            var wall = "H";

            var line = string.Empty;
            for (int i = 0; i < map.GetLength(0); i++) {
                for (int j = 0; j < map.GetLength(1); j++) {
                    var grid = map[i, j];
                    line += grid > 0 ? wall : corridor;
                }
                line += "\n";
            }
            Debug.Log(line);
        }

        /*
         * 3D맵의 평면도를 PNG로 저장하기 위한 함수
         */
        public static void ExportMapToPNG(int[,] map, Texture2D[] textures)
        {
            int texWidth = textures[0].width;
            int texHeight = textures[0].height;

            // 저장될 이미지의 사이즈 
            int width = map.GetLength(0) * texWidth;
            int height = map.GetLength(1) * texHeight;

            Texture2D outputTexture = new Texture2D(width, height);

            for (int x = 0; x < map.GetLength(0); x++) {
                for (int y = 0; y < map.GetLength(1); y++) {
                    int value = map[x, y];

                    int texIdx = value - 1; // 맵의 빈 공간(통로)을 제외한 곳의 value가 1부터 시작하므로 
                    if (texIdx < 0) texIdx = 0;

                    // 텍스처 배열에서 맵의 값에 매칭된 텍스처를 가져와서 지정된 위치에 배치
                    Texture2D texture = null;

                    if (value > 0) texture = textures[texIdx];

                    for (int i = 0; i < texWidth; i++) {

                        for (int j = 0; j < texHeight; j++) {

                            // 텍스처의 픽셀을 복사
                            Color color = texture != null ? texture.GetPixel(i, j) : Color.black;
                            outputTexture.SetPixel(x * texWidth + i, y * texHeight + j, color);
                        }
                    }
                }
            }

            outputTexture.Apply();

            SaveTextureToPNG(outputTexture);
        }

        /*
         * 텍스처를 PNG로 저장하는 함수
         */
        private static void SaveTextureToPNG(Texture2D texture)
        {
            string folderPath = Path.Combine(Application.dataPath, "GeneratedMap");


            if (!Directory.Exists(folderPath)) {
                Directory.CreateDirectory(folderPath);
            }

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = "map_" + timestamp + ".png";

            string path = Path.Combine(folderPath, filename);

            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(path, pngData);

            Debug.Log("Texture saved successfully! check " + path);
        }

    }

}

