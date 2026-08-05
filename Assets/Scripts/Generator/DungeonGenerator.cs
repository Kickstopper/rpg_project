using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Generator
{
    public static class DungeonGenerator
    {
        /// <summary>
        /// 랜덤한 미로형 던전을 절차적으로 생성함
        /// </summary>
        /// <param name="width">맵 가로 크기 (홀수 권장, 예: 21)</param>
        /// <param name="height">맵 세로 크기 (홀수 권장, 예: 21)</param>
        /// <param name="mapID">부여할 맵 ID</param>
        /// <param name="themeID">적용할 던전 테마 ID</param>
        /// <param name="wallTexId">벽으로 칠할 기본 텍스처 ID (일반적으로 0)</param>
        /// <param name="loopChance">막힌 벽을 뚫어 루프를 만들 확률 (0.0 ~ 1.0)</param>
        public static MapData GenerateRandomMaze(int width, int height, string mapID, string themeID, int wallTexId = 0, float loopChance = 0.05f)
        {
            // 미로 생성 알고리즘 특성상 가로세로 크기는 무조건 홀수여야 함
            if (width % 2 == 0) width++;
            if (height % 2 == 0) height++;

            int[,] grid = new int[width, height];
            
            // 맵 전체를 꽉 막힌 벽(1)으로 초기화
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    grid[x, y] = 1;
                }
            }

            // 재귀적 백트래킹(Recursive Backtracker)으로 길(0) 뚫기
            Stack<Vector2Int> stack = new Stack<Vector2Int>();
            Vector2Int startPos = new Vector2Int(1, 1);
            
            grid[startPos.x, startPos.y] = 0;
            stack.Push(startPos);

            Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

            while (stack.Count > 0)
            {
                Vector2Int current = stack.Peek();
                List<Vector2Int> unvisitedNeighbors = new List<Vector2Int>();

                // 두 칸 앞이 벽(1)이고 맵 범위를 벗어나지 않는 유효한 이웃 찾기
                foreach (var dir in directions)
                {
                    int nx = current.x + dir.x * 2;
                    int ny = current.y + dir.y * 2;

                    if (nx > 0 && nx < width - 1 && ny > 0 && ny < height - 1 && grid[nx, ny] == 1)
                    {
                        unvisitedNeighbors.Add(dir);
                    }
                }

                if (unvisitedNeighbors.Count > 0)
                {
                    // 갈 수 있는 방향 중 하나를 무작위로 선택
                    Vector2Int chosenDir = unvisitedNeighbors[Random.Range(0, unvisitedNeighbors.Count)];
                    
                    // 현재 칸과 두 칸 앞 이웃 사이의 벽을 허뭄
                    grid[current.x + chosenDir.x, current.y + chosenDir.y] = 0;
                    grid[current.x + chosenDir.x * 2, current.y + chosenDir.y * 2] = 0;

                    stack.Push(new Vector2Int(current.x + chosenDir.x * 2, current.y + chosenDir.y * 2));
                }
                else
                {
                    stack.Pop(); // 더 이상 갈 곳이 없으면 뒤로 돌아감
                }
            }

            // 루프 생성: 정통 미로는 갈림길이 만나지 않으므로, 일부 벽을 무작위로 뚫어 고전적 DRPG 느낌을 줌
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    if (grid[x, y] == 1)
                    {
                        // 상하가 뚫려있거나 좌우가 뚫려있는 단일 벽일 경우 확률적으로 파괴
                        bool isVerticalWall = grid[x, y + 1] == 0 && grid[x, y - 1] == 0;
                        bool isHorizontalWall = grid[x + 1, y] == 0 && grid[x - 1, y] == 0;

                        if ((isVerticalWall || isHorizontalWall) && Random.value < loopChance)
                        {
                            grid[x, y] = 0;
                        }
                    }
                }
            }

            // 막다른 골목을 모두 찾아 시작점과 출구 결정
            List<Vector2Int> deadEnds = new List<Vector2Int>();
            List<Vector2Int> floorTiles = new List<Vector2Int>();

            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    if (grid[x, y] == 0)
                    {
                        floorTiles.Add(new Vector2Int(x, y));

                        // 상하좌우 벽(1)의 개수를 셈
                        int wallCount = 0;
                        if (grid[x, y + 1] == 1) wallCount++;
                        if (grid[x, y - 1] == 1) wallCount++;
                        if (grid[x + 1, y] == 1) wallCount++;
                        if (grid[x - 1, y] == 1) wallCount++;

                        // 벽이 3개면 막다른 골목
                        if (wallCount >= 3)
                        {
                            deadEnds.Add(new Vector2Int(x, y));
                        }
                    }
                }
            }

            Vector2Int finalStartPos = new Vector2Int(1, 1);
            Vector2Int finalExitPos = new Vector2Int(width - 2, height - 2);
            Direction finalStartDir = Direction.North;

            if (deadEnds.Count >= 2)
            {
                // 시작 지점을 무작위 막다른 골목으로 선택
                int startIndex = Random.Range(0, deadEnds.Count);
                finalStartPos = deadEnds[startIndex];
                deadEnds.RemoveAt(startIndex); // 출구랑 겹치지 않게 리스트에서 제거

                // 시작 지점으로부터 가장 먼 막다른 골목들을 출구 후보로 정렬 (내림차순)
                deadEnds.Sort((a, b) => 
                    Vector2.Distance(finalStartPos, b).CompareTo(Vector2.Distance(finalStartPos, a))
                );
                
                // 거리가 가장 먼 최상위 3곳 중 하나를 출구로 선택 (매번 가장 먼 곳에만 생기는 뻔한 패턴 방지)
                int exitIndex = Random.Range(0, Mathf.Min(3, deadEnds.Count));
                finalExitPos = deadEnds[exitIndex];
            }
            else if (floorTiles.Count >= 2)
            {
                // 루프가 너무 많이 뚫려 막다른 길이 부족한 경우를 대비한 안전 장치
                finalStartPos = floorTiles[0];
                finalExitPos = floorTiles[floorTiles.Count - 1];
            }

            // 시작 지점의 뚫린 방향을 확인하여 플레이어가 막힌 벽이 아닌 길을 바라보도록 설정
            if (grid[finalStartPos.x, finalStartPos.y + 1] == 0) finalStartDir = Direction.North;
            else if (grid[finalStartPos.x + 1, finalStartPos.y] == 0) finalStartDir = Direction.East;
            else if (grid[finalStartPos.x, finalStartPos.y - 1] == 0) finalStartDir = Direction.South;
            else if (grid[finalStartPos.x - 1, finalStartPos.y] == 0) finalStartDir = Direction.West;

            // 생성된 2D 배열을 엔진 전용 MapData로 변환 및 오토 타일링
            MapData mapData = new MapData
            {
                width = width,
                height = height,
                mapID = mapID,
                themeID = themeID,
                startX = finalStartPos.x,
                startY = finalStartPos.y,
                startDirection = finalStartDir,
                cells = new CellData[width * height],
                entrances = new List<EntranceData>()
            };

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int index = y * width + x;
                    CellData cell = new CellData { x = x, y = y };

                    if (grid[x, y] == 1)
                    {
                        // 벽인 공간은 진입 불가(-1) 처리
                        cell.value = -1;
                        cell.wallTextureIDs = new int[] { -1, -1, -1, -1 };
                    }
                    else
                    {
                        // 바닥(0)인 공간은 4방향을 검사하여 벽 텍스처를 자동으로 발라줌
                        cell.value = 0;
                        cell.wallTextureIDs[0] = (y < height - 1 && grid[x, y + 1] == 1) ? wallTexId : -1; // 북
                        cell.wallTextureIDs[1] = (x < width - 1 && grid[x + 1, y] == 1) ? wallTexId : -1;  // 동
                        cell.wallTextureIDs[2] = (y > 0 && grid[x, y - 1] == 1) ? wallTexId : -1;          // 남
                        cell.wallTextureIDs[3] = (x > 0 && grid[x - 1, y] == 1) ? wallTexId : -1;          // 서
                    }

                    mapData.cells[index] = cell;
                }
            }

            // 다음 층으로 가는 EntranceData 배치
            EntranceData exitPortal = new EntranceData
            {
                type = EntranceType.Map,
                sourceX = finalExitPos.x,
                sourceY = finalExitPos.y,
                isWallEntrance = false, // 바닥형 포탈로 설정 (밟으면 이동)
                isWorldMap = false,
                destinationID = "NextRandomFloor",
                targetX = 1,
                targetY = 1,
                targetDirection = Direction.North
            };
            mapData.entrances.Add(exitPortal);

            return mapData;
        }
    }
}