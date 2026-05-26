using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Data;

namespace UI.DungeonMapScene
{
    public class DungeonPlayer
    {
        public float PosX { get; private set; }
        public float PosY { get; private set; }
        public int LogicX { get; private set; }
        public int LogicY { get; private set; }
        
        public float DirX { get; private set; }
        public float DirY { get; private set; }
        public float PlaneX { get; private set; }
        public float PlaneY { get; private set; }

        public int DirectionIdx { get; private set; } = 0; // 0:N, 1:E, 2:S, 3:W
        public bool IsMoving { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsGridMove { get; set; } = true;

        public float Pitch { get; set; } = 0f;
        public float JumpOffset { get; private set; } = 0f;
        
        private readonly float _fovScale;
        public float BackwardOffset { get; set; }
        
        private MonoBehaviour _runner; // 코루틴 실행용
        private MapData _currentMap;
        private List<int> _illusionTextureIds;

        public event System.Action OnMoveStepTaken;

        public DungeonPlayer(MonoBehaviour runner, float fovScale, float backwardOffset, List<int> illusions)
        {
            _runner = runner;
            _fovScale = fovScale;
            BackwardOffset = backwardOffset;
            _illusionTextureIds = illusions;
        }

        public void SetMapData(MapData map, int startX, int startY, Direction startDir)
        {
            _currentMap = map;
            DirectionIdx = (int)startDir;
            LogicX = startX;
            LogicY = startY;

            UpdateDirectionVectors();
            
            // 초기 위치 계산 (Offset 적용)
            Vector2 pos = GetOffsetPosition(startX, startY, DirectionIdx);
            PosX = pos.x;
            PosY = pos.y;
        }

        public void SetDirectPosition(float x, float y, int dir)
        {
            PosX = x;
            PosY = y;
            DirectionIdx = dir;
            LogicX = Mathf.FloorToInt(x);
            LogicY = Mathf.FloorToInt(y);
            UpdateDirectionVectors();
        }

        private void UpdateDirectionVectors()
        {
            (Vector2 dir, Vector2 plane) = GetVectorsForDirection(DirectionIdx);
            DirX = dir.x; DirY = dir.y;
            PlaneX = plane.x; PlaneY = plane.y;
        }

        private (Vector2 dir, Vector2 plane) GetVectorsForDirection(int dirIndex)
        {
            switch (dirIndex)
            {
                case 0: return (new Vector2(0, 1), new Vector2(_fovScale, 0)); // North
                case 1: return (new Vector2(1, 0), new Vector2(0, -_fovScale)); // East
                case 2: return (new Vector2(0, -1), new Vector2(-_fovScale, 0)); // South
                case 3: return (new Vector2(-1, 0), new Vector2(0, _fovScale)); // West
                default: return (new Vector2(0, 1), new Vector2(_fovScale, 0));
            }
        }

        public Vector2 GetOffsetPosition(int gridX, int gridY, int dirIdx)
        {
            Vector2 centerPos = new Vector2(gridX + 0.5f, gridY + 0.5f);
            (Vector2 dirVec, Vector2 _) = GetVectorsForDirection(dirIdx);
            return centerPos - (dirVec * BackwardOffset);
        }

        public void SetRunning(bool isRunning) => IsRunning = isRunning;

        // ================= Grid Move Coroutines =================

        public IEnumerator MoveGridRoutine(int targetX, int targetY, float duration, System.Action<float> onUpdate = null)
        {
            IsMoving = true;
            float elapsed = 0f;
            float startX = PosX;
            float startY = PosY;

            // 목표 지점은 그리드 중앙이 아니라, 방향에 따라 약간 뒤로 물러난 위치
            Vector2 targetPos = GetOffsetPosition(targetX, targetY, DirectionIdx);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                PosX = Mathf.Lerp(startX, targetPos.x, t);
                PosY = Mathf.Lerp(startY, targetPos.y, t);
                onUpdate?.Invoke(t);
                yield return null;
            }

            PosX = targetPos.x;
            PosY = targetPos.y;
            LogicX = targetX;
            LogicY = targetY;
            IsMoving = false;
            
            OnMoveStepTaken?.Invoke();
        }

        public IEnumerator RotateGridRoutine(int step, float duration, System.Action onRender)
        {
            IsMoving = true;
            int prevDir = DirectionIdx;
            // 음수 모듈러 연산 보정: (a % n + n) % n
            int nextDir = ((DirectionIdx + step) % 4 + 4) % 4;
            
            float targetAngle = (step == 1) ? -90f : (step == -1) ? 90f : 180f;

            (Vector2 startDir, Vector2 startPlane) = GetVectorsForDirection(prevDir);
            Vector2 center = new Vector2(LogicX + 0.5f, LogicY + 0.5f);
            Vector2 startOffset = GetOffsetPosition(LogicX, LogicY, prevDir) - center;

            float elapsed = 0f;
            while(elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float rad = Mathf.Lerp(0f, targetAngle, t) * Mathf.Deg2Rad;
                
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                // 방향 벡터 회전
                DirX = startDir.x * cos - startDir.y * sin;
                DirY = startDir.x * sin + startDir.y * cos;
                PlaneX = startPlane.x * cos - startPlane.y * sin;
                PlaneY = startPlane.x * sin + startPlane.y * cos;

                // 위치 공전 (Orbit)
                float offX = startOffset.x * cos - startOffset.y * sin;
                float offY = startOffset.x * sin + startOffset.y * cos;
                
                PosX = center.x + offX;
                PosY = center.y + offY;

                onRender?.Invoke();
                yield return null;
            }

            DirectionIdx = nextDir;
            UpdateDirectionVectors();
            
            // 정확한 위치로 스냅
            Vector2 final = GetOffsetPosition(LogicX, LogicY, DirectionIdx);
            PosX = final.x;
            PosY = final.y;
            
            IsMoving = false;
            onRender?.Invoke();
        }

        public IEnumerator BumpRoutine(Vector2Int dirVec, float duration = 0.2f, float intensity = 0.5f, System.Action onRender = null)
        {
            IsMoving = true;
            
            // 후진 체크. 바라보는 방향과 이동 방향이 반대면 후진
            Vector2Int fwd = GetForwardVector();
            bool isMovingBackward = (fwd.x * dirVec.x + fwd.y * dirVec.y) < 0;

            // 후진 충돌 시엔 등 뒤 공간이 없으므로 물리적 이동을 차단
            float actualIntensity = isMovingBackward ? 0f : intensity;

            float elapsed = 0f;
            float startX = PosX;
            float startY = PosY;
            float startPitch = Pitch;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                
                float t = Mathf.Clamp01(elapsed / duration); // t값이 1.0을 초과하여 사인파가 음수가 되는 것을 차단
                float sine = Mathf.Sin(t * Mathf.PI);
                
                PosX = startX + dirVec.x * actualIntensity * sine;
                PosY = startY + dirVec.y * actualIntensity * sine;
                
                if (isMovingBackward)
                    Pitch = startPitch + Mathf.Sin(t * Mathf.PI * 2f) * 15f; // 후진 시 상하 흔들기

                onRender?.Invoke();
                yield return null;
            }
            
            PosX = startX;
            PosY = startY;
            if (isMovingBackward) Pitch = startPitch;

            onRender?.Invoke();
            IsMoving = false;
        }

        public IEnumerator JumpRoutine(float duration, float height, System.Action onRender)
        {
            float elapsed = 0f;
            while(elapsed < duration)
            {
                elapsed += Time.deltaTime;
                JumpOffset = Mathf.Sin((elapsed / duration) * Mathf.PI) * height;
                onRender?.Invoke();
                yield return null;
            }
            JumpOffset = 0f;
            onRender?.Invoke();
        }

        public bool IsWalkable(float targetX, float targetY, float deltaX, float deltaY)
        {
            // 현재 위치 및 목표 위치 그리드 계산
            int currentX = Mathf.FloorToInt(PosX);
            int currentY = Mathf.FloorToInt(PosY);
            int targetGridX = Mathf.FloorToInt(targetX);
            int targetGridY = Mathf.FloorToInt(targetY);

            // 맵 데이터 참조 확인
            if (_currentMap == null) return false;

            // ---------------------------------------------------------
            // 맵 범위 체크
            // ---------------------------------------------------------
            if (targetGridX < 0 || targetGridX >= _currentMap.width || 
                targetGridY < 0 || targetGridY >= _currentMap.height)
                return false;

            // 이동 방향에 따른 검사할 벽면 인덱스 결정 (원본 코드 DDA 로직 기준)
            // side 0 (X축 이동): East(+) -> [0], West(-) -> [2]
            // side 1 (Y축 이동): North(+) -> [3], South(-) -> [1]
            
            // targetEnterFace: 목표 셀로 진입할 때 검사해야 할 벽 (타겟의 외벽)
            // currentExitFace: 현재 셀에서 나갈 때 검사해야 할 벽 (현재 셀의 내벽)
            int targetEnterFace = -1;
            int currentExitFace = -1;

            if (Mathf.Abs(deltaX) > 0.0001f) // X축 이동
            {
                if (deltaX > 0) // East 이동
                {
                    targetEnterFace = 3; // Target의 West면(3) 검사
                    currentExitFace = 1; // Current의 East면(1) 검사
                }
                else // West 이동
                {
                    targetEnterFace = 1; // Target의 East면(1) 검사
                    currentExitFace = 3; // Current의 West면(3) 검사
                }
            }
            else if (Mathf.Abs(deltaY) > 0.0001f) // Y축 이동
            {
                if (deltaY > 0) // North 이동
                {
                    targetEnterFace = 2; // Target의 South면(2) 검사
                    currentExitFace = 0; // Current의 North면(0) 검사
                }
                else // South 이동
                {
                    targetEnterFace = 0; // Target의 North면(0) 검사
                    currentExitFace = 2; // Current의 South면(2) 검사
                }
            }

            // ---------------------------------------------------------
            // 현재 셀 탈출 검사 (Exit Check)
            // ---------------------------------------------------------
            // 현재 셀 내부에 갇혀있는지 확인 (예: 내 방의 북쪽 벽이 막혀있음)
            if (currentX >= 0 && currentX < _currentMap.width && 
                currentY >= 0 && currentY < _currentMap.height)
            {
                CellData currentCell = _currentMap.GetCell(currentX, currentY);
                if (currentCell != null && currentCell.HasWall() && currentExitFace != -1)
                {
                    int texID = currentCell.wallTextureIDs[currentExitFace];
                    // 벽 텍스처가 존재하고(-1 아님), 일루전 월(통과 가능)이 아니라면 이동 불가
                    if (texID != -1 && !_illusionTextureIds.Contains(texID)) 
                        return false;
                }
            }

            // ---------------------------------------------------------
            // 목표 셀 진입 검사 (Enter Check)
            // ---------------------------------------------------------
            CellData targetCell = _currentMap.GetCell(targetGridX, targetGridY);
            
            // 목표 셀이 없거나 null이면 이동 불가 (안전장치)
            if (targetCell == null) return false;

            // 목표 셀이 void(-1) 바닥인 경우 이동 불가
            if (targetCell.value == -1) return false;

            // 목표 셀에 벽이 있다면 진입 방향의 면을 검사
            if (targetCell.HasWall() && targetEnterFace != -1)
            {
                int texID = targetCell.wallTextureIDs[targetEnterFace];
                if (texID != -1 && !_illusionTextureIds.Contains(texID)) 
                    return false;
            }

            return true;
        }

        
        // Free Move & Snap Logic
        

        // 자유 이동 (Free Move) - 충돌 처리 포함 (슬라이딩)
        public void MoveFree(float speed)
        {
            // X축 이동 시도
            float nextX = PosX + DirX * speed;
            if (IsWalkable(nextX, PosY, DirX * speed, 0))
            {
                PosX = nextX;
            }

            // Y축 이동 시도
            float nextY = PosY + DirY * speed;
            if (IsWalkable(PosX, nextY, 0, DirY * speed))
            {
                PosY = nextY;
            }

            // 논리 좌표 갱신
            LogicX = Mathf.FloorToInt(PosX);
            LogicY = Mathf.FloorToInt(PosY);
            
            // 걸음 수 이벤트 (Free Move에서는 이동 거리에 따라 호출하거나 생략 가능)
            // 여기서는 생략하고, 필요시 거리를 누적해서 호출
        }

        // 자유 회전 (Free Rotate)
        public void RotateFree(float rotSpeed)
        {
            float oldDirX = DirX;
            DirX = oldDirX * Mathf.Cos(rotSpeed) - DirY * Mathf.Sin(rotSpeed);
            DirY = oldDirX * Mathf.Sin(rotSpeed) + DirY * Mathf.Cos(rotSpeed);
            
            float oldPlaneX = PlaneX;
            PlaneX = oldPlaneX * Mathf.Cos(rotSpeed) - PlaneY * Mathf.Sin(rotSpeed);
            PlaneY = oldPlaneX * Mathf.Sin(rotSpeed) + PlaneY * Mathf.Cos(rotSpeed);
        }

        // 그리드 모드로 복귀 시 위치 및 각도 보정 (Snap)
        public void SnapToGrid()
        {
            // 가장 가까운 정수 좌표로 스냅
            int gridX = Mathf.RoundToInt(PosX);
            int gridY = Mathf.RoundToInt(PosY);

            // 맵 범위 제한
            if (_currentMap != null)
            {
                gridX = Mathf.Clamp(gridX, 0, _currentMap.width - 1);
                gridY = Mathf.Clamp(gridY, 0, _currentMap.height - 1);
            }

            // 가장 가까운 4방향(N, E, S, W) 찾기
            int bestDir = 0;
            float maxDot = -2.0f;

            for (int i = 0; i < 4; i++)
            {
                var vectors = GetVectorsForDirection(i);
                // 현재 바라보는 방향(DirX, DirY)과 4방향 벡터 내적
                float dot = DirX * vectors.dir.x + DirY * vectors.dir.y;
                if (dot > maxDot)
                {
                    maxDot = dot;
                    bestDir = i;
                }
            }

            // 데이터 확정
            DirectionIdx = bestDir;
            LogicX = gridX;
            LogicY = gridY;

            // 벡터 및 위치 재설정
            UpdateDirectionVectors();
            Vector2 finalPos = GetOffsetPosition(gridX, gridY, DirectionIdx);
            PosX = finalPos.x;
            PosY = finalPos.y;
        }

        public Vector2Int GetForwardVector()
        {
            switch (DirectionIdx)
            {
                case 0: return new Vector2Int(0, 1);
                case 1: return new Vector2Int(1, 0);
                case 2: return new Vector2Int(0, -1);
                case 3: return new Vector2Int(-1, 0);
                default: return Vector2Int.zero;
            }
        }

        public Vector2Int GetRightVector()
        {
            // Forward 벡터를 시계방향 90도 회전: (x, y) -> (y, -x)
            Vector2Int fwd = GetForwardVector();
            return new Vector2Int(fwd.y, -fwd.x);
        }
    }
}