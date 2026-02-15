using UnityEngine;
using Data;
namespace UI
{
    public class EditorGridVisualizer : MonoBehaviour
    {
        public MapData mapData;
        public float cellSize = 2.0f;
        public float wallHeight = 2.0f;
        public float wallThickness = 0.1f; // 벽의 두께를 얇게 설정

        // 텍스처 ID별 색상 (Inspector에서 설정)
        public Color[] textureColors = new Color[] 
        { 
            Color.red, Color.blue, Color.yellow, Color.cyan, Color.magenta, Color.green 
        };

        // 선택된 셀 좌표 (DungeonMapEditor에서 갱신)
        public Vector2Int selectedCoord = new Vector2Int(-1, -1);

        void OnDrawGizmos()
        {
            if (mapData == null || mapData.cells == null) return;

            for (int i = 0; i < mapData.cells.Length; i++)
            {
                CellData cell = mapData.cells[i];
                Vector3 center = GetCellCenter(cell.x, cell.y);

                // =========================================================
                // 1. 바닥 그리기 (항상 회색)
                // =========================================================
                
                // (1) 바닥 면 채우기 (Solid)
                Gizmos.color = Color.gray; 
                // 높이(Y)를 0.1f로 주어 얇은 판처럼 그립니다.
                Gizmos.DrawCube(center, new Vector3(cellSize, 0.1f, cellSize)); 

                // (2) 바닥 테두리 그리기 (Grid Line)
                // 회색 바닥끼리 겹치면 경계가 안 보이므로, 검은색 선으로 테두리를 그립니다.
                Gizmos.color = Color.black;
                Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.1f, cellSize));

                // 2. 벽 그리기 로직 (isWall이 true일 때만 텍스처 검사)
                // 각 방향별로 텍스처 ID가 유효하면(-1 아님) 그 면을 그림
                
                // North (Z+)
                if (cell.wallTextureIDs[0] >= 0) 
                    DrawWallFace(center, Vector3.forward, cell.wallTextureIDs[0]);

                // East (X+)
                if (cell.wallTextureIDs[1] >= 0) 
                    DrawWallFace(center, Vector3.right, cell.wallTextureIDs[1]);

                // South (Z-)
                if (cell.wallTextureIDs[2] >= 0) 
                    DrawWallFace(center, Vector3.back, cell.wallTextureIDs[2]);

                // West (X-)
                if (cell.wallTextureIDs[3] >= 0) 
                    DrawWallFace(center, Vector3.left, cell.wallTextureIDs[3]);
            }

            // 3. 선택된 셀 하이라이트 (깜빡임 효과)
            if (selectedCoord.x >= 0 && selectedCoord.y >= 0)
            {
                HighlightSelectedCell();
            }

            // 4. 플레이어 시작 위치 및 방향 그리기
            DrawPlayerStart(); 

            // 워프 그리기
            DrawWarps();
        }

        // 워프 시각화 함수
        void DrawWarps()
        {
            if (mapData.warps == null) return;

            foreach (var warp in mapData.warps)
            {
                Vector3 center = GetCellCenter(warp.sourceX, warp.sourceY);
                
                // 1. 워프 위치 표시 (보라색 구체)
                Gizmos.color = new Color(1f, 0f, 1f, 0.6f); // 마젠타색, 반투명
                Gizmos.DrawSphere(center + Vector3.up * (wallHeight * 0.5f), cellSize * 0.3f);
                
                // 2. 외곽선
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(center + Vector3.up * (wallHeight * 0.5f), cellSize * 0.35f);

                // 3. 트리거 방향 표시 (어느 쪽에서 부딪혀야 하는지 화살표)
                // 트리거 방향이 North라면, 플레이어는 남쪽에서 북쪽으로 이동하며 부딪힘.
                // 따라서 화살표는 남쪽 -> 북쪽(벽 중심)을 가리키게 그림.
                
                Vector3 dirVec = GetDirectionVector(warp.triggerDirection);
                Vector3 arrowEnd = center + Vector3.up * (wallHeight * 0.5f);
                // 화살표 시작점 (벽 중심에서 반대 방향으로 약간 떨어진 곳)
                Vector3 arrowStart = arrowEnd - (dirVec * cellSize * 0.6f);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(arrowStart, arrowEnd);
                
                // 화살표 머리
                Vector3 right = Quaternion.Euler(0, 90, 0) * dirVec;
                Gizmos.DrawLine(arrowEnd, arrowEnd - dirVec * 0.2f + right * 0.1f);
                Gizmos.DrawLine(arrowEnd, arrowEnd - dirVec * 0.2f - right * 0.1f);
            }
        }

        void DrawPlayerStart()
        {
            if (mapData == null) return;
            
            int x = mapData.startX;
            int y = mapData.startY;
            Direction dir = mapData.startDirection;

            // 시작 위치의 중앙
            Vector3 center = GetCellCenter(x, y);
            
            // 1. 시작 셀 전체를 청록색으로 표시
            Gizmos.color = new Color(0, 1, 1, 0.5f); // 청록색, 반투명
            // 높이 0.2m 정도의 얇은 큐브를 그려 바닥과 구분
            Gizmos.DrawCube(center + Vector3.up * 0.1f, new Vector3(cellSize, 0.2f, cellSize)); 

            // 2. 방향 화살표 그리기
            Vector3 directionVector = GetDirectionVector(dir);
            
            Gizmos.color = Color.yellow; // 노란색 화살표
            
            // 화살표 시작점 (셀 중심보다 살짝 위에)
            Vector3 startPoint = center + Vector3.up * 0.2f;
            
            // 화살표 끝점 (중심에서 해당 방향으로 0.8 * cellSize 만큼 이동)
            Vector3 endPoint = startPoint + directionVector * (cellSize * 0.4f);

            // 화살표 선 그리기
            Gizmos.DrawLine(startPoint, endPoint);
            
            // 화살표 머리 (끝점으로부터 양쪽으로 작은 선 2개)
            Vector3 crossDir = Quaternion.Euler(0, 90, 0) * directionVector; // 90도 회전한 방향
            Gizmos.DrawLine(endPoint, endPoint - (directionVector * 0.1f) + (crossDir * 0.1f));
            Gizmos.DrawLine(endPoint, endPoint - (directionVector * 0.1f) - (crossDir * 0.1f));
        }

        // Enum에 따른 월드 방향 벡터 반환
        Vector3 GetDirectionVector(Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return Vector3.forward; // Z+
                case Direction.East: return Vector3.right;    // X+
                case Direction.South: return Vector3.back;    // Z-
                case Direction.West: return Vector3.left;     // X-
                default: return Vector3.forward;
            }
        }

        // 단일 벽면을 그리는 함수
        void DrawWallFace(Vector3 cellCenter, Vector3 direction, int textureID)
        {
            // 1. 색상 결정 (ID 범위 체크)
            Color faceColor = (textureID < textureColors.Length) ? textureColors[textureID] : Color.white;
            Gizmos.color = faceColor;

            // 2. 위치 계산 (중심에서 해당 방향 끝으로 이동)
            // 벽의 중심점 = 셀 중심 + (방향 * (셀크기 절반 - 두께 절반))
            // 두께 절반을 빼는 이유는 벽이 셀 밖으로 튀어 나가지 않게 하기 위함 (Inner alignment)
            Vector3 faceCenter = cellCenter + (direction * ((cellSize * 0.5f) - (wallThickness * 0.5f)));
            faceCenter.y += wallHeight * 0.5f; // 높이 보정

            // 3. 크기 계산
            Vector3 faceSize;

            // 북/남쪽 벽은 가로(X)가 넓고, 동/서쪽 벽은 세로(Z)가 넓어야 함
            if (direction == Vector3.forward || direction == Vector3.back)
            {
                // X축 길이: cellSize, Z축 길이: wallThickness
                faceSize = new Vector3(cellSize, wallHeight, wallThickness);
            }
            else // Right or Left
            {
                // X축 길이: wallThickness, Z축 길이: cellSize - (겹침 방지용 보정)
                // 모서리에서 겹치는 현상(Z-Fighting)을 줄이기 위해 두께만큼 살짝 줄여줄 수도 있지만, 
                // 여기서는 깔끔하게 꽉 채우기 위해 cellSize 사용
                faceSize = new Vector3(wallThickness, wallHeight, cellSize);
            }

            // 4. 큐브 그리기
            Gizmos.DrawCube(faceCenter, faceSize);
            
            // 5. 외곽선 (잘 보이게 검은색)
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(faceCenter, faceSize);
        }

        Vector3 GetCellCenter(int x, int y)
        {
            return new Vector3(x * cellSize + cellSize * 0.5f, 0, y * cellSize + cellSize * 0.5f);
        }

        void HighlightSelectedCell()
        {
            Vector3 center = GetCellCenter(selectedCoord.x, selectedCoord.y);
            Vector3 drawCenter = center + Vector3.up * (wallHeight * 0.5f);
            
            // 깜빡이는 노란색 박스
            float alpha = Mathf.PingPong(Time.realtimeSinceStartup * 3.0f, 0.5f) + 0.1f;
            Gizmos.color = new Color(1f, 0.92f, 0.016f, alpha);
            
            // 선택 박스는 벽보다 살짝 크게 그려서 덮어씌움
            Gizmos.DrawWireCube(drawCenter, new Vector3(cellSize * 1.05f, wallHeight * 1.05f, cellSize * 1.05f));
        }
    }
}
