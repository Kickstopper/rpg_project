using UnityEngine;
using UnityEngine.UI;
using Data;

namespace UI.DungeonMapScene
{
    public class AutoMapRenderer : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        public RawImage mapDisplay;
        
        [Header("플레이어 아이콘")]
        public RectTransform playerIcon; // 인스펙터에서 PlayerIcon UI 연결

        [Header("맵 설정")]
        private int cellSize = 28;
        private int wallThickness = 2;

        [Header("색상 설정")]
        public Color fogColor = Color.black; // 안개 색상
        public Color defaultColor = Color.white;
        public Color[] floorColors;
        public Color[] wallColors;

        private Texture2D mapTexture;
        private MapData currentMapData;
        private DungeonMapState currentMapState; // 현재 탐험 상태 참조

        // 초기화 및 전체 그리기
        public void DrawFullMap(MapData mapData, DungeonMapState mapState)
        {
            if (mapData == null || mapState == null) return;

            currentMapData = mapData;
            currentMapState = mapState;

            int width = mapData.width;
            int height = mapData.height;

            // 텍스처 생성 (크기가 다를 때만)
            int texWidth = width * cellSize;
            int texHeight = height * cellSize;

            if (mapTexture == null || mapTexture.width != texWidth || mapTexture.height != texHeight)
            {
                mapTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
                mapTexture.filterMode = FilterMode.Point;
                mapTexture.wrapMode = TextureWrapMode.Clamp;
            }

            // 전체 셀 순회
            for (int i = 0; i < mapData.cells.Length; i++)
            {
                CellData cell = mapData.cells[i];
                
                // 방문 여부에 따라 그리기 분기
                if (mapState.IsVisited(cell.x, cell.y))
                {
                    DrawCellGraphic(cell); // 맵 내용 그리기
                }
                else
                {
                    DrawFogGraphic(cell.x, cell.y); // 안개 그리기
                }
            }

            mapTexture.Apply();
            mapDisplay.texture = mapTexture;
            
            // 1. 사이즈 확정
            mapDisplay.rectTransform.sizeDelta = new Vector2(texWidth, texHeight);

            // 2. 화면 중앙 정렬
            CenterMapPosition(texWidth, texHeight);
        }

        // 맵 크기에 맞춰 위치를 중앙으로 보정하는 함수
        private void CenterMapPosition(float width, float height)
        {
            RectTransform rt = mapDisplay.rectTransform;

            // 1. 앵커를 부모의 정중앙(Center)으로 설정
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);

            // 2. 피벗은 (0, 0) 좌하단 유지 (아이콘 좌표 계산을 위해 필수!)
            rt.pivot = new Vector2(0f, 0f);

            // 3. 위치 보정
            // Pivot이 좌하단이므로, (0,0)에 두면 맵의 왼쪽 아래 귀퉁이가 화면 중앙에 온다.
            // 따라서 맵 너비/높이의 절반만큼 왼쪽(-x), 아래(-y)로 이동시켜야 정중앙에 온다.
            rt.anchoredPosition = new Vector2(-width * 0.5f, -height * 0.5f);
        }

        // 플레이어 이동 시 호출: 특정 좌표만 밝힘
        public void RevealCell(int x, int y)
        {
            if (currentMapData == null || currentMapState == null) return;

            // 1. 해당 좌표의 셀 데이터 가져오기
            CellData cell = currentMapData.GetCell(x, y);
            
            // 2. 만약 현재 상태(mapState)에서 해당 셀이 방문된 상태라면 그리기 수행
            // (이미 방문했더라도 텍스처가 갱신 안 된 경우를 대비해 다시 그림)
            if (cell != null)
            {
                DrawCellGraphic(cell);
                mapTexture.Apply(); // GPU에 텍스처 업데이트 전달
            }
        }

        // 맵 그래픽 그리기 (바닥을 먼저 꽉 채워서 안개를 지움)
        void DrawCellGraphic(CellData cell)
        {
            int startX = cell.x * cellSize;
            int startY = cell.y * cellSize;

            // 1. 바닥 채우기 (Alpha 1.0f로 안개 완벽 제거)
            Color fColor = (cell.value > -1 && cell.value < floorColors.Length) 
                ? floorColors[cell.value] : defaultColor;

            // 성능 향상을 위해 배열로 한 번에 채우기
            Color[] fillColors = new Color[cellSize * cellSize];
            for (int i = 0; i < fillColors.Length; i++) fillColors[i] = fColor;
            mapTexture.SetPixels(startX, startY, cellSize, cellSize, fillColors);

            // 2. 벽 그리기
            // Index 0: 왼쪽 (West)
            DrawWallLine(cell.wallTextureIDs[0], startX, startY, wallThickness, cellSize);
            
            // Index 1: 위쪽 (North)
            DrawWallLine(cell.wallTextureIDs[1], startX, startY + cellSize - wallThickness, cellSize, wallThickness);
            
            // Index 2: 오른쪽 (East)
            DrawWallLine(cell.wallTextureIDs[2], startX + cellSize - wallThickness, startY, wallThickness, cellSize);
            
            // Index 3: 아래쪽 (South)
            DrawWallLine(cell.wallTextureIDs[3], startX, startY, cellSize, wallThickness);
        }

        // 안개 그래픽 그리기 (검은색 채우기)
        void DrawFogGraphic(int cellX, int cellY)
        {
            int startX = cellX * cellSize;
            int startY = cellY * cellSize;
            Color color = fogColor;
            color.a = 1.0f; // 안개도 불투명하게

            for (int y = 0; y < cellSize; y++)
            {
                for (int x = 0; x < cellSize; x++)
                {
                    mapTexture.SetPixel(startX + x, startY + y, color);
                }
            }
        }

        void DrawWallLine(int wallID, int x, int y, int width, int height)
        {
            Color wColor = defaultColor;
            if (wallID < 0)
            {
                //wColor = Color.white;
            }
            else if (wallID < wallColors.Length)
            {
                wColor =  wallColors[wallID];
            }

            for (int py = 0; py < height; py++)
                for (int px = 0; px < width; px++)
                    mapTexture.SetPixel(x + px, y + py, wColor);
        }

        // 자유 이동용 플레이어 아이콘 업데이트 (float 좌표, 방향 벡터 사용)
        public void UpdatePlayerIconFree(float x, float y, float dirX, float dirY)
        {
            if (playerIcon == null) return;

            // 1. 아이콘 활성화
            playerIcon.gameObject.SetActive(true);

            // 2. 픽셀 좌표 계산
            // RaycastScreen의 좌표계: 0.5가 셀의 중앙, 0.0이 셀의 경계선
            // 따라서 cellSize를 곱하기만 하면 픽셀 좌표가 나옴
            // (기존 int 버전은 정수 인덱스라 +0.5f를 해줬지만, 여기선 x, y가 이미 중앙값(x.5)을 가지고 있음)
            float px = x * cellSize;
            float py = y * cellSize;

            playerIcon.anchoredPosition = new Vector2(px, py);

            // 3. 방향 회전 (벡터 -> 각도)
            // 좌표계: North(0, 1) -> UI 0도
            // Atan2(1, 0) = 90도 -> -90 해야 0도
            float angleRad = Mathf.Atan2(dirY, dirX);
            float angleDeg = angleRad * Mathf.Rad2Deg;
            float uiAngle = angleDeg - 90f; // 보정값 적용

            playerIcon.localRotation = Quaternion.Euler(0, 0, uiAngle);
        }

        // 플레이어 위치 및 방향 업데이트 함수
        public void UpdatePlayerIcon(int x, int y, Direction dir)
        {
            if (playerIcon == null) return;

            // 1. 아이콘 활성화 (혹시 꺼져있다면)
            playerIcon.gameObject.SetActive(true);
            
            // 2. 픽셀 좌표 계산
            // 맵의 피벗이 (0,0) 좌하단 기준일 때:
            // x * cellSize = 셀의 왼쪽 구석
            // + cellSize * 0.5f = 셀의 정중앙
            float px = (x * cellSize) + (cellSize * 0.5f);
            float py = (y * cellSize) + (cellSize * 0.5f);

            // 3. 위치 이동 (AnchoredPosition 사용)
            playerIcon.anchoredPosition = new Vector2(px, py);

            // 4. 방향 회전 (Z축 회전)
            float rotationAngle = GetRotationFromDirection(dir);
            playerIcon.localRotation = Quaternion.Euler(0, 0, rotationAngle);
        }

        // Direction Enum을 각도로 변환
        float GetRotationFromDirection(Direction dir)
        {
            // 화살표 스프라이트가 기본적으로 "위(North)"를 가리키고 있다고 가정
            switch (dir)
            {
                case Direction.North: return 0f;
                case Direction.East:  return -90f; // 오른쪽
                case Direction.South: return 180f; // 아래
                case Direction.West:  return 90f;  // 왼쪽
                default: return 0f;
            }
        }
    }
}
