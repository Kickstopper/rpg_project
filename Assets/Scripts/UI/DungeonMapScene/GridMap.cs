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
        public float spacing = 0f; 
        public int mapSize = 7; 
        public int visibleSize = 5; 
        public int rectScale = 3; 

        [Header("Enemy Icons")]
        public GameObject enemyPrefab; // 인스펙터에서 할당 안 하면 기본 빨간 점 생성
        public Color enemyNormalColor = Color.red;
        public Color enemyFallenColor = Color.gray;

        [Header("References")]
        public RectTransform parentRT;
        public GameObject gridPrefab;
        public GameObject arrowPrefab;

        private RectTransform _mapParent;
        private Image _arrowImg;
        private MapData _map;
        
        private Dictionary<int, GridCellController> _gridCellDict = new Dictionary<int, GridCellController>();
        private List<Image> _enemyIcons = new List<Image>(); // 에너미 아이콘 풀링 리스트
        
        private float _moveDistX;
        private float _moveDistY;
        
        private int _curPx;
        private int _curPy;
        private int _gridOriginX; // 그리드가 생성된 기준 X좌표 (이동 애니메이션 보정용)
        private int _gridOriginY; // 그리드가 생성된 기준 Y좌표
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
            _gridOriginX = _curPx;
            _gridOriginY = _curPy;
            _gridHalfSize = mapSize / 2;

            CreateMapLayout();
            
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

            if (_mapParent != null) _mapParent.DOKill(true);
            if (_arrowImg != null) _arrowImg.rectTransform.DOKill();

            foreach (var icon in _enemyIcons)
            {
                if (icon != null && icon.gameObject != null) Destroy(icon.gameObject);
            }
            _enemyIcons.Clear();

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
            Vector2 gridSize = Vector2.one * wallWidth;
            _moveDistX = gridSize.x + spacing;
            _moveDistY = gridSize.y + spacing;

            float totalWidth = (_moveDistX * mapSize);
            float totalHeight = (_moveDistY * mapSize);

            parentRT.localScale = Vector2.one * rectScale;
            parentRT.anchorMin = new Vector2(0, 0);
            parentRT.anchorMax = new Vector2(0, 0);
            parentRT.pivot = new Vector2(0, 0);
            parentRT.anchoredPosition = new Vector2(30f, 30f); 
            parentRT.sizeDelta = new Vector2(visibleSize * wallWidth, visibleSize * wallWidth);
            
            GameObject mapObj = new GameObject("MapContent");
            _mapParent = mapObj.AddComponent<RectTransform>();
            _mapParent.SetParent(parentRT);
            
            _mapParent.anchorMin = new Vector2(0.5f, 0.5f);
            _mapParent.anchorMax = new Vector2(0.5f, 0.5f);
            _mapParent.pivot = new Vector2(0.5f, 0.5f);
            _mapParent.anchoredPosition = Vector2.zero; 
            _mapParent.localScale = Vector2.one;
            _mapParent.sizeDelta = new Vector2(totalWidth, totalHeight);

            float startOffsetX = -(totalWidth * 0.5f) + (gridSize.x * 0.5f);
            float startOffsetY = (totalHeight * 0.5f) - (gridSize.y * 0.5f);

            for (int r = 0; r < mapSize; r++)
            {
                for (int c = 0; c < mapSize; c++)
                {
                    Vector2 pos = new Vector2(
                        startOffsetX + c * _moveDistX,
                        startOffsetY - r * _moveDistY
                    );

                    GameObject gridObj = Instantiate(gridPrefab, _mapParent);
                    gridObj.name = $"Grid_{r}_{c}";
                    RectTransform rt = gridObj.GetComponent<RectTransform>();
                    rt.anchoredPosition = pos;

                    _gridCellDict.Add(r * mapSize + c, gridObj.GetComponent<GridCellController>());
                }
            }

            GameObject arrowObj = Instantiate(arrowPrefab, transform);
            _arrowImg = arrowObj.GetComponent<Image>();
            _arrowImg.rectTransform.localScale = Vector2.one;
            _arrowImg.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _arrowImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _arrowImg.rectTransform.anchoredPosition = Vector2.zero; 
        }

        public void TranslateToNewPosition(int targetX, int targetY, float duration = 0f)
        {
            if (targetX == _curPx && targetY == _curPy) return;

            int deltaX = targetX - _curPx;
            int deltaY = targetY - _curPy;

            float targetMoveX = -deltaX * _moveDistX;
            float targetMoveY = -deltaY * _moveDistY;

            _curPx = targetX;
            _curPy = targetY;

            _mapParent.DOKill(true);
            _mapParent.anchoredPosition = Vector2.zero;

            _mapParent.DOAnchorPos(new Vector2(targetMoveX, targetMoveY), duration)
                .SetEase(Ease.Linear)
                .OnComplete(OnMoveComplete);
        }

        private void OnMoveComplete()
        {
            _mapParent.anchoredPosition = Vector2.zero;
            _gridOriginX = _curPx; // 이동이 완료되면 맵 논리 기준점 갱신
            _gridOriginY = _curPy; 
            UpdateGridColors(_curPx, _curPy);
        }

        private void UpdateGridColors(int centerX, int centerY)
        {
            for (int r = 0; r < mapSize; r++)
            {
                for (int c = 0; c < mapSize; c++)
                {
                    int mapX = centerX + (c - _gridHalfSize); 
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

        public void SetFreeDirection(float dirX, float dirY)
        {
            _arrowImg.rectTransform.DOKill();
            float angleRad = Mathf.Atan2(dirY, dirX); 
            float angleDeg = angleRad * Mathf.Rad2Deg;
            float uiAngle = angleDeg - 90f;
            _arrowImg.rectTransform.localEulerAngles = new Vector3(0, 0, uiAngle);
        }

        public void SnapToGrid(int targetX, int targetY, int dirIndex)
        {
            _curPx = targetX;
            _curPy = targetY;
            _gridOriginX = _curPx;
            _gridOriginY = _curPy;

            _mapParent.DOKill(true);
            _mapParent.anchoredPosition = Vector2.zero;

            UpdateGridColors(_curPx, _curPy);
            SetDirection(dirIndex, 0f);
        }

        // --- 에너미 마커 실시간 반영 핵심 로직 --- //
        public void UpdateEnemyIcons(List<RaycastingController.MapEnemy> enemies)
        {
            if (_mapParent == null) return;

            // 1. 필요한 아이콘 개수만큼 풀링(생성)
            while (_enemyIcons.Count < enemies.Count)
            {
                GameObject iconObj;
                if (enemyPrefab != null)
                {
                    iconObj = Instantiate(enemyPrefab, _mapParent);
                }
                else
                {
                    // 프리팹이 없다면 기본 빨간 정사각형 생성
                    iconObj = new GameObject("EnemyIcon");
                    iconObj.transform.SetParent(_mapParent);
                    Image img = iconObj.AddComponent<Image>();
                    
                    RectTransform rt = img.rectTransform;
                    rt.sizeDelta = new Vector2(6f, 6f); // 그리드 칸(11)보다 약간 작은 크기
                }
                
                RectTransform rectT = iconObj.GetComponent<RectTransform>();
                rectT.anchorMin = new Vector2(0.5f, 0.5f);
                rectT.anchorMax = new Vector2(0.5f, 0.5f);
                rectT.pivot = new Vector2(0.5f, 0.5f);
                rectT.localScale = Vector2.one;

                _enemyIcons.Add(iconObj.GetComponent<Image>());
            }

            // 2. 사용하지 않는 남은 아이콘 비활성화
            for (int i = enemies.Count; i < _enemyIcons.Count; i++)
            {
                _enemyIcons[i].gameObject.SetActive(false);
            }

            // 3. 현재 맵 기준점(_gridOrigin)을 토대로 로컬 위치 부드럽게 보정
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                var iconImg = _enemyIcons[i];
                
                if (!enemy.isAlive)
                {
                    iconImg.gameObject.SetActive(false);
                    continue;
                }

                iconImg.gameObject.SetActive(true);

                // enemy.x, y는 실수(Float). 중앙 기준을 0.5로 보고 기준점 대비 칸수를 픽셀 간격으로 환산
                float localX = (enemy.x - 0.5f - _gridOriginX) * _moveDistX;
                float localY = (enemy.y - 0.5f - _gridOriginY) * _moveDistY;

                iconImg.rectTransform.anchoredPosition = new Vector2(localX, localY);

                // 방향 회전
                float angle = 0;
                switch (enemy.direction)
                {
                    case 0: angle = 0f; break;   // North
                    case 1: angle = -90f; break; // East
                    case 2: angle = 180f; break; // South
                    case 3: angle = 90f; break;  // West
                }
                iconImg.rectTransform.localEulerAngles = new Vector3(0, 0, angle);

                // 상태에 따른 색상 변경
                iconImg.color = enemy.isFallen ? enemyFallenColor : enemyNormalColor;
            }
        }

        private void OnDisable()
        {
            transform.DOKill(); 
            _mapParent?.DOKill();
            _arrowImg?.rectTransform.DOKill();
        }
    }
}