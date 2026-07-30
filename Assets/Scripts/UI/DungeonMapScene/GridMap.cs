using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Data;
using Controller;

namespace UI.DungeonMapScene
{
    public class GridMap : MonoBehaviour
    {
        [Header("Settings")]
        public float spacing = 0f; // 그리드 간격
        public int mapSize = 7; // 그려질 맵의 크기
        public int visibleSize = 5; // 실제 보여질 맵의 크기
        public int rectScale = 2; // 맵의 스케일

        [Header("References")]
        public RectTransform parentRT;
        public GameObject gridPrefab;
        public GameObject arrowPrefab;

        private RectTransform _mapParent;
        private Image _arrowImg;
        private MapData _map;
        
        private Dictionary<int, GridCellController> _gridCellDict = new Dictionary<int, GridCellController>();
        
        private float _moveDistX;
        private float _moveDistY;
        
        private int _curPx;
        private int _curPy;
        private int _gridHalfSize;

        private int wallWidth = 11;

        private HashSet<int> illusionTextures;
        private HashSet<int> doorTextures = new HashSet<int>();

        public void Initialize(MapData mapData, List<int> illusions, List<DoorAnimConfig> doors)
        {
            ClearMap();

            if (illusions != null)
                illusionTextures = new HashSet<int>(illusions);
            else
                illusionTextures = new HashSet<int>();

            if (doors != null)
            {
                foreach(var door in doors) doorTextures.Add(door.closedTexId);
            }

            _map = mapData;
            
            if (_map == null) return;
            
            _curPx = _map.startX;
            _curPy = _map.startY;
            _gridHalfSize = mapSize / 2;

            CreateMapLayout();
            
            // 초기 상태 설정
            UpdateGridColors(_curPx, _curPy);
            SetDirection((int)_map.startDirection, 0f);
        }

        public void SetDirection(int dir, float duration = 0)
        {
            float angle = 0;
            switch (dir)
            {
                case (int)Direction.North: angle = 0f; break;
                case (int)Direction.East: angle = -90f; break;
                case (int)Direction.South: angle = 180f; break;
                case (int)Direction.West: angle = 90f; break;
            }

            if (duration <= 0)
            {
                _arrowImg.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
            }
            else
            {
                _arrowImg.rectTransform.DORotate(new Vector3(0, 0, angle), duration)
                    .SetEase(Ease.Linear);
            }
        }

        private void ClearMap()
        {
            transform.DOKill();

            illusionTextures = null;
            doorTextures.Clear();

            if (_mapParent != null) _mapParent.DOKill();
            if (_arrowImg != null) _arrowImg.rectTransform.DOKill();

            if (_mapParent != null)
            {
                Destroy(_mapParent.gameObject);
                _mapParent = null;
            }

            if (_arrowImg != null)
            {
                Destroy(_arrowImg.gameObject);
                _arrowImg = null;
            }

            _gridCellDict.Clear();
        }

        private void CreateMapLayout()
        {
            parentRT.localScale = Vector2.one * rectScale;
            
            parentRT.localPosition = new Vector2(-(Screen.width - visibleSize * wallWidth * rectScale) / 2f, 
                                                 -(Screen.height - visibleSize * wallWidth * rectScale) / 2f);
            parentRT.sizeDelta = new Vector2(visibleSize * wallWidth, visibleSize * wallWidth);
            
            GameObject mapObj = new GameObject("MapContent");
            _mapParent = mapObj.AddComponent<RectTransform>();
            _mapParent.SetParent(transform);
            _mapParent.localPosition = Vector3.zero;
            _mapParent.localScale = Vector2.one;
            _mapParent.anchorMin = new Vector2(0.5f, 0.5f);
            _mapParent.anchorMax = new Vector2(0.5f, 0.5f);
            _mapParent.pivot = new Vector2(0.5f, 0.5f);
            _mapParent.sizeDelta = new Vector2(Screen.width, Screen.height);

            Vector2 gridSize = Vector2.one * wallWidth;
            _moveDistX = gridSize.x + spacing;
            _moveDistY = gridSize.y + spacing;

            // 중앙 정렬을 위한 전체 맵 크기 계산
            float totalWidth = _moveDistX * mapSize - spacing;
            float totalHeight = _moveDistY * mapSize - spacing;
            float startOffsetX = -(totalWidth * 0.5f) + (gridSize.x * 0.5f);
            float startOffsetY = (totalHeight * 0.5f) - (gridSize.y * 0.5f);

            // 그리드 생성 및 캐싱
            for (int r = 0; r < mapSize; r++)
            {
                for (int c = 0; c < mapSize; c++)
                {
                    // 위치 계산: c가 증가하면 X(우측)로, r이 증가하면 Y(아래)로
                    Vector2 pos = new Vector2(
                        startOffsetX + c * _moveDistX,
                        startOffsetY - r * _moveDistY
                    );

                    GameObject gridObj = Instantiate(gridPrefab, _mapParent);
                    gridObj.name = $"Grid_{r}_{c}";
                    RectTransform rt = gridObj.GetComponent<RectTransform>();
                    rt.anchoredPosition = pos;

                    // GridCellController 캐싱. 키 값: (Row * Size + Col)
                    _gridCellDict.Add(r * mapSize + c, gridObj.GetComponent<GridCellController>());
                }
            }

            // 화살표 생성
            GameObject arrowObj = Instantiate(arrowPrefab, transform);
            _arrowImg = arrowObj.GetComponent<Image>();
            _arrowImg.rectTransform.localScale = Vector2.one;
            _arrowImg.rectTransform.localPosition = Vector3.zero; // 항상 중앙 유지
        }

        public void TranslateToNewPosition(int targetX, int targetY, float duration = 0f)
        {
            if (targetX == _curPx && targetY == _curPy) return;

            float targetMoveX = 0;
            float targetMoveY = 0;

            if (targetX > _curPx) 
            {
                // 오른쪽으로 이동했다면, 맵은 왼쪽으로 밀림
                targetMoveX = -_moveDistX; 
            }
            else if (targetX < _curPx) 
            {
                //왼쪽으로 이동했다면, 맵은 오른쪽으로 밀림
                targetMoveX = _moveDistX; 
            }
            
            if (targetY > _curPy) 
            {
                // 위로 이동했다면, 맵은 아래로 밀림
                targetMoveY = -_moveDistY; 
            }
            else if (targetY < _curPy) 
            {
                // 아래로 이동했다면, 맵은 위로 밀림   
                targetMoveY = _moveDistY; 
            }

            // 좌표 갱신
            _curPx = targetX;
            _curPy = targetY;

            // 이동 애니메이션 실행
            _mapParent.DOLocalMove(new Vector3(targetMoveX, targetMoveY, 0), duration)
                .SetEase(Ease.Linear)
                .OnComplete(OnMoveComplete);
        }

        private void OnMoveComplete()
        {
            // 슬라이드 애니메이션이 끝나면 다시 중앙으로 (눈속임) 
            _mapParent.localPosition = Vector3.zero;
            
            // 현재 좌표 기준으로 그리드의 색상을 갱신
            UpdateGridColors(_curPx, _curPy);
        }

        private void UpdateGridColors(int centerX, int centerY)
        {
            for (int r = 0; r < mapSize; r++)
            {
                for (int c = 0; c < mapSize; c++)
                {
                    // mapX: 화면 가로(c) 증가 -> 맵 동쪽(+X) (정방향)
                    int mapX = centerX + (c - _gridHalfSize); 
                    
                    // mapY: 화면 세로(r) 증가(아래로 감) -> 맵 남쪽(-Y) (역방향)
                    int mapY = centerY - (r - _gridHalfSize);

                    CellData cellData = null;

                    if (mapX >= 0 && mapX < _map.width && 
                        mapY >= 0 && mapY < _map.height)
                    {
                        cellData = _map.GetCell(mapX, mapY); 
                    }

                    _gridCellDict[r * mapSize + c].UpdateWallState(cellData, illusionTextures, doorTextures);
                }
            }
        }

        // 자유 이동 시 매 프레임 호출될 함수 (부드러운 회전 X, 즉시 회전 O)
        public void SetFreeDirection(float dirX, float dirY)
        {
            // 기존에 실행 중이던 화살표 회전 애니메이션이 있다면 강제 종료
            _arrowImg.rectTransform.DOKill();

            // 벡터에서 각도로 변환 (Atan2)
            // 좌표계: North(0, 1), East(1, 0), South(0, -1), West(-1, 0)
            // Atan2(y, x)의 반환값 (동쪽 0도 기준 반시계 방향):
            // North(0, 1) -> 90도
            // East(1, 0)  -> 0도
            float angleRad = Mathf.Atan2(dirY, dirX); 
            float angleDeg = angleRad * Mathf.Rad2Deg;

            // 좌표계 보정값 적용 (-90도)
            // North: 90도 - 90 = 0도 (UI Up)
            // East:   0도 - 90 = -90도 (UI Right)
            float uiAngle = angleDeg - 90f;

            // 회전 적용 (Z축 회전)
            _arrowImg.rectTransform.localEulerAngles = new Vector3(0, 0, uiAngle);
        }

        // 모드 전환 시, 애니메이션 없이 즉시 해당 좌표와 방향으로 맵을 '스냅'하는 함수
        public void SnapToGrid(int targetX, int targetY, int dirIndex)
        {
            _curPx = targetX;
            _curPy = targetY;

            _mapParent.DOKill();
            _mapParent.localPosition = Vector3.zero;

            UpdateGridColors(_curPx, _curPy);
            SetDirection(dirIndex, 0f);
        }

        private void OnDisable()
        {
            transform.DOKill(); 
            _mapParent?.DOKill();
            _arrowImg?.rectTransform.DOKill();
        }
    }
}