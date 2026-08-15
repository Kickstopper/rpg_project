using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Data;
using Controller;

namespace UI.DungeonMapScene
{
    public class FullGridMapUI : MonoBehaviour
    {
        [Header("Settings")]
        public float spacing = 0f; 
        public int wallWidth = 11; // GridCell 프리팹의 가로/세로 기본 크기
        public float mapScale = 1f;

        [Header("References")]
        public RectTransform parentRT; // 뷰포트 (ScrollRect 컨텐츠 등)
        public GameObject gridPrefab;  // GridCellController가 붙은 UI 프리팹
        public GameObject arrowPrefab; // 플레이어 화살표 프리팹

        [Header("Enemy Icons")]
        public bool showEnemyIcons = true; // 몬스터 아이콘 표시 여부
        public GameObject enemyIconPrefab; 
        public Color enemyNormalColor = Color.red;
        public Color enemyFallenColor = Color.yellow;
        
        private RectTransform _mapParent;
        private Image _arrowImg;
        private List<Image> _enemyIcons = new List<Image>();
        private MapData _map;
        private DungeonMapState _mapState;
        
        private GridCellController[,] _gridCells; // 검색 및 접근을 위한 캐싱

        private float _moveDistX;
        private float _moveDistY;

        private HashSet<int> _illusionTextures;
        private HashSet<int> _doorTextures;

        // 맵 초기 진입 시 한 번만 호출
        public void Initialize(MapData mapData, DungeonMapState mapState, List<int> illusions, List<DoorAnimConfig> doors)
        {
            ClearMap();

            _map = mapData;
            _mapState = mapState;
            
            _illusionTextures = illusions != null ? new HashSet<int>(illusions) : new HashSet<int>();
            _doorTextures = new HashSet<int>();
            
            if (doors != null)
            {
                foreach (var door in doors) _doorTextures.Add(door.closedTexId);
            }

            if (_map == null) return;

            CreateMapLayout();
            UpdateAllCells();
        }

        private void ClearMap()
        {
            if (_mapParent != null) Destroy(_mapParent.gameObject);
            if (_arrowImg != null) Destroy(_arrowImg.gameObject);
            foreach (var icon in _enemyIcons)
            {
                if (icon != null && icon.gameObject != null) Destroy(icon.gameObject);
            }
            _enemyIcons.Clear();
            
            _gridCells = null;
        }

        private void CreateMapLayout()
        {
            _moveDistX = wallWidth + spacing;
            _moveDistY = wallWidth + spacing;

            float totalWidth = _map.width * _moveDistX;
            float totalHeight = _map.height * _moveDistY;

            parentRT.localScale = Vector2.one * mapScale;

            // 전체 맵 타일들을 묶어줄 컨테이너 생성
            GameObject mapObj = new GameObject("FullMapContent");
            _mapParent = mapObj.AddComponent<RectTransform>();
            _mapParent.SetParent(parentRT);

            // 중앙 정렬을 위한 앵커 및 피벗 설정
            _mapParent.anchorMin = new Vector2(0.5f, 0.5f);
            _mapParent.anchorMax = new Vector2(0.5f, 0.5f);
            _mapParent.pivot = new Vector2(0.5f, 0.5f);
            _mapParent.anchoredPosition = Vector2.zero;
            _mapParent.localScale = Vector2.one;
            _mapParent.sizeDelta = new Vector2(totalWidth, totalHeight);

            // (0,0) 좌표가 컨테이너의 왼쪽-아래에 오도록 기준점 계산
            float startOffsetX = -(totalWidth * 0.5f) + (_moveDistX * 0.5f);
            float startOffsetY = -(totalHeight * 0.5f) + (_moveDistY * 0.5f);

            _gridCells = new GridCellController[_map.width, _map.height];

            // 맵 크기만큼 Grid 프리팹을 스폰하여 좌표에 배치
            for (int x = 0; x < _map.width; x++)
            {
                for (int y = 0; y < _map.height; y++)
                {
                    Vector2 pos = new Vector2(
                        startOffsetX + x * _moveDistX,
                        startOffsetY + y * _moveDistY
                    );

                    GameObject gridObj = Instantiate(gridPrefab, _mapParent);
                    gridObj.name = $"Grid_{x}_{y}";
                    RectTransform rt = gridObj.GetComponent<RectTransform>();
                    rt.anchoredPosition = pos;

                    GridCellController cellCtrl = gridObj.GetComponent<GridCellController>();
                    _gridCells[x, y] = cellCtrl;

                    // 최초 생성 시에는 안개로 덮여있어야 하므로 비활성화 처리
                    gridObj.SetActive(false); 
                }
            }

            // 플레이어 아이콘(화살표) 생성
            GameObject arrowObj = Instantiate(arrowPrefab, _mapParent);
            _arrowImg = arrowObj.GetComponent<Image>();
            _arrowImg.rectTransform.localScale = Vector2.one;
            _arrowImg.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _arrowImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            // 아직 위치가 갱신되지 않았으므로 숨김
            _arrowImg.gameObject.SetActive(false); 
        }

        // 전체 맵 상태를 한 번에 동기화 (단축키로 맵 UI를 열 때 호출)
        public void UpdateAllCells()
        {
            if (_map == null || _mapState == null || _gridCells == null) return;

            for (int x = 0; x < _map.width; x++)
            {
                for (int y = 0; y < _map.height; y++)
                {
                    RevealCell(x, y);
                }
            }
        }

        // 특정 좌표 한 칸만 갱신
        public void RevealCell(int x, int y)
        {
            if (_map == null || _gridCells == null) return;
            if (x < 0 || x >= _map.width || y < 0 || y >= _map.height) return;

            bool isVisited = _mapState.IsVisited(x, y);
            GridCellController cellCtrl = _gridCells[x, y];

            if (isVisited)
            {
                // 방문한 칸이면 오브젝트를 켜고 색상/텍스트 갱신
                if (!cellCtrl.gameObject.activeSelf) cellCtrl.gameObject.SetActive(true);
                
                CellData cellData = _map.GetCell(x, y);
                cellCtrl.UpdateWallState(cellData, _illusionTextures, _doorTextures);
            }
            else
            {
                // 미방문 칸은 프리팹을 꺼서 안 보이게 처리
                if (cellCtrl.gameObject.activeSelf) cellCtrl.gameObject.SetActive(false);
            }
        }

        // 플레이어 아이콘 위치 및 방향 업데이트
        public void UpdatePlayerIcon(int x, int y, Direction dir, bool focusCenter = false)
        {
            if (_arrowImg == null || _map == null) return;

            if (!_arrowImg.gameObject.activeSelf) _arrowImg.gameObject.SetActive(true);

            float totalWidth = _map.width * _moveDistX;
            float totalHeight = _map.height * _moveDistY;
            float startOffsetX = -(totalWidth * 0.5f) + (_moveDistX * 0.5f);
            float startOffsetY = -(totalHeight * 0.5f) + (_moveDistY * 0.5f);

            float px = startOffsetX + x * _moveDistX;
            float py = startOffsetY + y * _moveDistY;

            _arrowImg.rectTransform.anchoredPosition = new Vector2(px, py);

            float angle = 0;
            switch (dir)
            {
                case Direction.North: angle = 0f; break;
                case Direction.East: angle = -90f; break;
                case Direction.South: angle = 180f; break;
                case Direction.West: angle = 90f; break;
            }
            _arrowImg.rectTransform.localEulerAngles = new Vector3(0, 0, angle);

            // 맵 컨테이너 자체를 이동시켜 플레이어가 화면 정중앙에 오도록 보정
            if (focusCenter && _mapParent != null)
            {
                // 화살표가 있는 곳(px, py)을 중심으로 맞추기 위해 맵 전체를 역방향(-px, -py)으로 이동
                _mapParent.anchoredPosition = new Vector2(-px, -py);
            }
        }

        public void UpdateEnemyIcons(List<RaycastingController.MapEnemy> enemies)
        {
            if (_mapParent == null || _map == null) return;

            // 표시 설정이 꺼져있다면 모든 아이콘 숨김
            if (!showEnemyIcons)
            {
                foreach (var icon in _enemyIcons)
                {
                    if (icon != null && icon.gameObject.activeSelf) icon.gameObject.SetActive(false);
                }
                return;
            }

            // 아이콘 개수 풀링
            while (_enemyIcons.Count < enemies.Count)
            {
                GameObject iconObj;
                if (enemyIconPrefab != null) iconObj = Instantiate(enemyIconPrefab, _mapParent);
                else
                {
                    iconObj = new GameObject("EnemyIcon");
                    iconObj.transform.SetParent(_mapParent);
                    Image img = iconObj.AddComponent<Image>();
                    RectTransform rt = img.rectTransform;
                    //rt.sizeDelta = new Vector2(wallWidth * 0.8f, wallWidth * 0.8f); // 칸 크기보다 약간 작게
                }
                
                RectTransform rectT = iconObj.GetComponent<RectTransform>();
                rectT.anchorMin = new Vector2(0.5f, 0.5f);
                rectT.anchorMax = new Vector2(0.5f, 0.5f);
                rectT.pivot = new Vector2(0.5f, 0.5f);
                rectT.localScale = Vector2.one;

                _enemyIcons.Add(iconObj.GetComponent<Image>());
            }

            // 남는 잉여 아이콘 비활성화
            for (int i = enemies.Count; i < _enemyIcons.Count; i++)
            {
                _enemyIcons[i].gameObject.SetActive(false);
            }

            // 기준 위치 캐싱
            float totalWidth = _map.width * _moveDistX;
            float totalHeight = _map.height * _moveDistY;
            float startOffsetX = -(totalWidth * 0.5f) + (_moveDistX * 0.5f);
            float startOffsetY = -(totalHeight * 0.5f) + (_moveDistY * 0.5f);

            // 좌표 및 방향 갱신
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                var iconImg = _enemyIcons[i];
                
                int gridX = Mathf.FloorToInt(enemy.x);
                int gridY = Mathf.FloorToInt(enemy.y);

                // 몬스터가 죽었거나, 몬스터가 서있는 칸이 아직 미방문 상태라면 아이콘을 그리지 않음
                if (!enemy.isAlive || (_mapState != null && !_mapState.IsVisited(gridX, gridY)))
                {
                    iconImg.gameObject.SetActive(false);
                    continue;
                }

                iconImg.gameObject.SetActive(true);

                // enemy.x 와 y는 내부적으로 0.5(중앙)이 더해진 실수 상태이므로 이를 보정하여 위치 산출
                float px = startOffsetX + (enemy.x - 0.5f) * _moveDistX;
                float py = startOffsetY + (enemy.y - 0.5f) * _moveDistY;

                iconImg.rectTransform.anchoredPosition = new Vector2(px, py);

                // 방향 전환 적용
                float angle = 0;
                switch (enemy.direction)
                {
                    case 0: angle = 0f; break;
                    case 1: angle = -90f; break;
                    case 2: angle = 180f; break;
                    case 3: angle = 90f; break;
                }
                iconImg.rectTransform.localEulerAngles = new Vector3(0, 0, angle);

                // 기절/일반 상태에 따른 색상 변환
                iconImg.color = enemy.isFallen ? enemyFallenColor : enemyNormalColor;
            }
        }
    }
}