using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using Data;
using Unity.VisualScripting;
using Controller;

namespace UI.DungeonMapScene
{
    public class GridMap : MonoBehaviour
    {
        [Header("Settings")]
        public float spacing = 0f;
        public int viewSize = 9; // 보여질 그리드 크기 (홀수 권장)

        [Header("References")]
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

        public void Initialize(MapData mapData)
        {
            ClearMap();

            _map = mapData;
            
            if (_map == null) return;
            
            _curPx = _map.startX;
            _curPy = _map.startY;
            _gridHalfSize = viewSize / 2;

            CreateMapLayout();
            
            // 초기 상태 설정
            UpdateGridColors(_curPx, _curPy);
            SetDirection((int)_map.startDirection, 0f);
        }

        private void OnDisable()
        {
            // 씬 전환/비활성화 시 트윈 킬 (DOTween 안전장치)
            transform.DOKill(); 
            _mapParent?.DOKill();
            _arrowImg?.rectTransform.DOKill();
        }

        private void ClearMap()
        {
            // 1. 기존 트윈(애니메이션) 모두 종료
            transform.DOKill();
            if (_mapParent != null) _mapParent.DOKill();
            if (_arrowImg != null) _arrowImg.rectTransform.DOKill();

            // 2. 기존 MapContent 오브젝트 삭제
            if (_mapParent != null)
            {
                Destroy(_mapParent.gameObject);
                _mapParent = null;
            }

            // 3. 기존 화살표 오브젝트 삭제 (화살표도 매번 새로 만드므로)
            if (_arrowImg != null)
            {
                Destroy(_arrowImg.gameObject);
                _arrowImg = null;
            }

            // 4. 캐싱된 딕셔너리 초기화
            _gridCellDict.Clear();
        }

        private void CreateMapLayout()
        {
            Vector2 scale = Vector2.one;
            // 1. Map Parent 생성
            GameObject mapObj = new GameObject("MapContent");
            _mapParent = mapObj.AddComponent<RectTransform>();
            _mapParent.SetParent(transform);
            _mapParent.localPosition = Vector3.zero;
            _mapParent.localScale = scale;
            _mapParent.anchorMin = new Vector2(0.5f, 0.5f);
            _mapParent.anchorMax = new Vector2(0.5f, 0.5f);
            _mapParent.pivot = new Vector2(0.5f, 0.5f);
            _mapParent.sizeDelta = new Vector2(Screen.width, Screen.height); // 혹은 부모 사이즈에 맞춤

            // 2. 그리드 사이즈 계산
            Vector2 gridSize = Vector2.one * 11; // 기본 사이즈는 11x11
            _moveDistX = gridSize.x + spacing;
            _moveDistY = gridSize.y + spacing;

            // 전체 맵의 물리적 크기 계산 (중앙 정렬용)
            float totalWidth = _moveDistX * viewSize - spacing;
            float totalHeight = _moveDistY * viewSize - spacing;
            float startOffsetX = -(totalWidth * 0.5f) + (gridSize.x * 0.5f);
            float startOffsetY = (totalHeight * 0.5f) - (gridSize.y * 0.5f);

            // 3. 그리드 생성 및 캐싱
            for (int r = 0; r < viewSize; r++) // Row (Vertical)
            {
                for (int c = 0; c < viewSize; c++) // Col (Horizontal)
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

                    // GridCellController 컴포넌트를 미리 캐싱 (GetPixel 비용 제거)
                    // 키 값: (Row * Size + Col)
                    _gridCellDict.Add(r * viewSize + c, gridObj.GetComponent<GridCellController>());
                }
            }

            // 4. 화살표 생성 (부모는 MapParent가 아닌 이 컴포넌트의 transform - 화살표는 이동하지 않고 회전만 함)
            GameObject arrowObj = Instantiate(arrowPrefab, transform);
            _arrowImg = arrowObj.GetComponent<Image>();
            _arrowImg.rectTransform.localScale = scale;
            _arrowImg.rectTransform.localPosition = Vector3.zero; // 항상 중앙 유지
        }

        // 이동 완료 후 호출될 콜백
        private void OnMoveComplete()
        {
            // 1. 물리적 위치를 원점으로 리셋
            _mapParent.localPosition = Vector3.zero;
            
            // 2. 그리드의 색상(내용물)을 현재 좌표 기준으로 갱신
            UpdateGridColors(_curPx, _curPy);
        }

        public void TranslateToNewPosition(int targetX, int targetY, float duration = 0f)
        {
            if (targetX == _curPx && targetY == _curPy) return;

            float targetMoveX = 0;
            float targetMoveY = 0;

            // 기존: X변화 -> Y이동, Y변화 -> X이동
            // 변경: X변화 -> X이동, Y변화 -> Y이동

            // 1. targetX 변화 (이제 화면의 가로/X축 슬라이드 담당)
            if (targetX > _curPx) 
            {
                // X 증가 -> 오른쪽으로 이동했다면, 맵은 왼쪽으로 밀려야 함
                targetMoveX = -_moveDistX; 
            }
            else if (targetX < _curPx) 
            {
                // X 감소 -> 왼쪽으로 이동했다면, 맵은 오른쪽으로 밀려야 함
                targetMoveX = _moveDistX; 
            }
            
            // 2. targetY 변화 (이제 화면의 세로/Y축 슬라이드 담당)
            if (targetY > _curPy) 
            {
                // Y 감소 -> 위로 이동했다면, 맵은 아래로 밀려야 함
                targetMoveY = -_moveDistY; 
            }
            else if (targetY < _curPy) 
            {
                // Y 증가 -> 아래로 이동했다면, 맵은 위로 밀려야 함 (Unity UI Y축은 위가 +)   
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

        private void UpdateGridColors(int centerX, int centerY)
        {
            for (int r = 0; r < viewSize; r++)
            {
                for (int c = 0; c < viewSize; c++)
                {
                    // mapX: 화면 가로(c) 증가 -> 맵 동쪽(+X) (정방향)
                    int mapX = centerX + (c - _gridHalfSize); 
                    
                    // mapY: 화면 세로(r) 증가(아래로 감) -> 맵 남쪽(-Y) (역방향)
                    // 기존: centerY + (r - ...) -> 아래로 갈수록 좌표가 커져서 뒤집힘
                    // 수정: centerY - (r - ...) -> 아래로 갈수록 좌표가 작아짐 (정상)
                    int mapY = centerY - (r - _gridHalfSize);

                    CellData cellData = null;

                    if (mapX >= 0 && mapX < _map.width && 
                        mapY >= 0 && mapY < _map.height)
                    {
                        cellData = _map.GetCell(mapX, mapY); 
                    }

                    _gridCellDict[r * viewSize + c].UpdateWallState(cellData);
                }
            }
        }

        // 자유 이동 시 매 프레임 호출될 함수 (부드러운 회전 X, 즉시 회전 O)
        public void SetFreeDirection(float dirX, float dirY)
        {
            // 1. 기존에 실행 중이던 화살표 회전 애니메이션이 있다면 강제 종료 (충돌 방지)
            _arrowImg.rectTransform.DOKill();

            // 2. 벡터 -> 각도 변환 (Atan2)
            // 좌표계: North(0, 1), East(1, 0), South(0, -1), West(-1, 0)
            
            // Atan2(y, x)의 반환값 (동쪽 0도 기준 반시계 방향):
            // North(0, 1) -> 90도
            // East(1, 0)  -> 0도
            
            float angleRad = Mathf.Atan2(dirY, dirX); 
            float angleDeg = angleRad * Mathf.Rad2Deg;

            // 3. 좌표계 보정값 적용 (-90도)
            // North: 90도 - 90 = 0도 (UI Up)
            // East:   0도 - 90 = -90도 (UI Right)
            float uiAngle = angleDeg - 90f;

            // 4. 회전 적용 (Z축 회전)
            _arrowImg.rectTransform.localEulerAngles = new Vector3(0, 0, uiAngle);
        }

        // 모드 전환 시, 애니메이션 없이 즉시 해당 좌표와 방향으로 맵을 '스냅'하는 함수
        public void SnapToGrid(int targetX, int targetY, int dirIndex)
        {
            // 1. 현재 좌표 갱신
            _curPx = targetX;
            _curPy = targetY;

            // 2. 물리적 위치 초기화 (트윈 중단 및 원점 복귀)
            _mapParent.DOKill();
            _mapParent.localPosition = Vector3.zero;

            // 3. 그리드 색상 즉시 갱신
            UpdateGridColors(_curPx, _curPy);

            // 4. 화살표 방향 즉시 설정 (애니메이션 시간 0)
            SetDirection(dirIndex, 0f);
        }

        public void SetDirection(int dir, float duration = 0)
        {
            float angle = 0;
            switch (dir)
            {
                case (int)Direction.North: angle = 0f; break; // 360 대신 0 사용
                case (int)Direction.East: angle = -90f; break; // Unity UI 기준 -90도
                case (int)Direction.South: angle = 180f; break;
                case (int)Direction.West: angle = 90f; break;
            }

            // 화살표가 가리키는 방향을 Z축 회전으로 표현
            // 보통 2D UI에서 0도는 "오른쪽"이나 "위쪽"을 의미함. (스프라이트 원본 방향에 따라 다름)
            // 여기서는 North(0)를 기준으로 잡음.
            
            if (duration <= 0)
            {
                _arrowImg.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
            }
            else
            {
                _arrowImg.rectTransform.DORotate(new Vector3(0, 0, angle), duration)
                    .SetEase(Ease.OutBack); // 약간의 탄성 효과 추가
            }
        }
    }
}