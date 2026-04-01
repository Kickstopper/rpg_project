using System;
using UnityEngine;
using Data;
using Manager;

namespace UI.DungeonMapScene
{
    public class RaycastRenderEngine
    {
        private Color32[] _buffer;
        private Color32[] _leftEyeBuffer;
        private float[] _zBuffer;
        private Color32[] _flatTexturePixels; // 최적화된 텍스처 메모리
        
        private MapData _worldMap;
        private Texture2D[] _textures;
        private SpriteInfo[] _sprtData;
        
        // Sprite Sorting
        private SpriteSortInfo[] _spriteSortList;
        
        private TileAnimState[,] _tileAnimStates;
        
        private int _texWidth, _texHeight;
        private int _screenWidth, _screenHeight;
        private int _ceilTexIdx, _floorTexIdx;
        
        private bool _isScanning;
        private float _currentScanRadius;

        public Texture2D ScreenTexture { get; private set; }

        public void Initialize(int width, int height)
        {
            _screenWidth = width;
            _screenHeight = height;
            _buffer = new Color32[width * height];
            _leftEyeBuffer = new Color32[width * height];
            _zBuffer = new float[width];
            
            ScreenTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            ScreenTexture.filterMode = FilterMode.Point;
        }

        public void LoadAssets(Texture2D[] textures, int texW, int texH, SpriteInfo[] sprites)
        {
            _textures = textures;
            _texWidth = texW;
            _texHeight = texH;
            _sprtData = sprites;
            
            if (_sprtData != null)
            {
                _spriteSortList = new SpriteSortInfo[_sprtData.Length];
            }

            PrecomputeTextures();
        }

        public void SetMapData(MapData map, DungeonTheme theme, TileAnimState[,] animStates)
        {
            _worldMap = map;
            _ceilTexIdx = theme.ceilingTexIdx;
            _floorTexIdx = theme.floorTexIdx;
            _tileAnimStates = animStates;
        }

        public void SetScanState(bool scanning, float radius)
        {
            _isScanning = scanning;
            _currentScanRadius = radius;
        }

        private void PrecomputeTextures()
        {
            if (_textures == null || _textures.Length == 0) return;
            int pxPerTex = _texWidth * _texHeight;
            _flatTexturePixels = new Color32[_textures.Length * pxPerTex];

            for (int i = 0; i < _textures.Length; i++)
            {
                Color[] src = _textures[i].GetPixels();
                int offset = i * pxPerTex;
                for (int p = 0; p < src.Length; p++)
                    _flatTexturePixels[offset + p] = (Color32)src[p];
            }
        }

        private Color32 GetPixelFast(int texIdx, int x, int y)
        {
            if (texIdx < 0 || texIdx >= _textures.Length) return new Color32(255, 0, 255, 255);
            x &= (_texWidth - 1);
            y &= (_texHeight - 1);
            return _flatTexturePixels[(texIdx * _texWidth * _texHeight) + (y * _texWidth) + x];
        }

        // ================= 메인 렌더링 루프 =================
        public void RenderFrame(DungeonPlayer player, RenderSettings settings)
        {
            // 유기체 애니메이션을 위한 주입
            settings.animTime = Time.time;

            Array.Clear(_buffer, 0, _buffer.Length);
            Array.Clear(_zBuffer, 0, _zBuffer.Length);

            if (GameSettingManager.Instance.useAnaglyph)
            {
                RenderStereo(player, settings);
            }
            else
            {
                PerformPass(player, settings, 1, false);
            }

            ScreenTexture.LoadRawTextureData(
                System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(_buffer, 0),
                _buffer.Length * 4
            );
            ScreenTexture.Apply();
        }

        // 색상 병합 로직 및 눈 위치 보정
        private void RenderStereo(DungeonPlayer player, RenderSettings settings)
        {
            // 왼쪽 눈 (Red Channel)
            PerformPass(player, settings, 2, true, -settings.stereoSeparation);
            
            Array.Copy(_buffer, _leftEyeBuffer, _buffer.Length);
            
            Array.Clear(_buffer, 0, _buffer.Length);

            // 오른쪽 눈 (Cyan: Green + Blue Channel)
            PerformPass(player, settings, 2, true, settings.stereoSeparation);

            // 병합 (Merge)
            for (int i = 0; i < _buffer.Length; i++)
            {
                Color32 left = _leftEyeBuffer[i];
                Color32 right = _buffer[i];

                byte alpha = (byte)Mathf.Max(left.a, right.a);
                _buffer[i] = new Color32(left.r, right.g, right.b, alpha);
            }
        }

        private void PerformPass(DungeonPlayer player, RenderSettings settings, int step, bool isStereo, float stereoOffset = 0f)
        {
            float rPosX = player.PosX;
            float rPosY = player.PosY;
            
            // 3D 효과를 위해 카메라 평면 벡터 방향으로 위치 이동
            if (isStereo)
            {
                rPosX += player.PlaneX * stereoOffset;
                rPosY += player.PlaneY * stereoOffset;
            }

            CastFloorCeiling(player, settings, step, rPosX, rPosY);
            CastWalls(player, settings, step, rPosX, rPosY);
            if (_sprtData != null && _sprtData.Length > 0)
                CastSprites(player, settings, step, rPosX, rPosY);
        }

        // ================= 세부 렌더링 로직 =================
        private void CastWalls(DungeonPlayer player, RenderSettings settings, int step, float px, float py)
        {
            Color32 pulseColor = settings.pulseColor;
            Color32 wireframeColor = settings.wireframeColor;
            float pulseWidth = settings.pulseWidth;

            for (int x = 0; x < _screenWidth; x += step)
            {
                float cameraX = 2 * x / (float)_screenWidth - 1;
                float rayDirX = player.DirX + player.PlaneX * cameraX;
                float rayDirY = player.DirY + player.PlaneY * cameraX;

                int mapX = (int)px;
                int mapY = (int)py;

                float sideDistX, sideDistY;
                float deltaDistX = (rayDirX == 0) ? 1e30f : Mathf.Abs(1 / rayDirX);
                float deltaDistY = (rayDirY == 0) ? 1e30f : Mathf.Abs(1 / rayDirY);
                float perpWallDist;

                int stepX, stepY;
                int hit = 0, side = 0;
                bool hitBackFace = false;
                int hitTexId = 0;

                if (rayDirX < 0) { stepX = -1; sideDistX = (px - mapX) * deltaDistX; }
                else { stepX = 1; sideDistX = (mapX + 1.0f - px) * deltaDistX; }
                if (rayDirY < 0) { stepY = -1; sideDistY = (py - mapY) * deltaDistY; }
                else { stepY = 1; sideDistY = (mapY + 1.0f - py) * deltaDistY; }

                // DDA 알고리즘
                // 바닥 구멍용
                float voidEdgeDist = -1f;
                int   voidEdgeSide = 0;
                int   clipY = 0; // 앞쪽 바닥에 의해 가려지는 한계선(화면 Y좌표)
                // 천장 구멍용
                float ceilEdgeDist = -1f;
                int   ceilEdgeSide = 0;
                int   ceilClipY = _screenHeight; // 천장은 위로 갈수록 Y가 커지므로 화면 맨 위로 초기화
                
                int horizon = 0;

                while (hit == 0)
                {
                    int prevX = mapX, prevY = mapY;
                    if (sideDistX < sideDistY) { sideDistX += deltaDistX; mapX += stepX; side = 0; }
                    else { sideDistY += deltaDistY; mapY += stepY; side = 1; }

                    if (mapX < 0 || mapX >= _worldMap.width || mapY < 0 || mapY >= _worldMap.height)
                    {
                        hit = 1; hitBackFace = true;
                        hitTexId = GetBoundaryTex(prevX, prevY, side, stepX, stepY);
                        CellData prevCell = _worldMap.GetCell(prevX, prevY);
                        bool isPrevSolid = (prevCell != null && prevCell.value != -1);
                        if (!isPrevSolid && voidEdgeDist < 0f)
                        {
                            voidEdgeDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                            voidEdgeSide = side;
                        }

                        // 천장 맵 경계 처리
                        bool isPrevCeilSolid = (prevCell != null && prevCell.value != 1);
                        if (!isPrevCeilSolid && ceilEdgeDist < 0f)
                        {
                            ceilEdgeDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                            ceilEdgeSide = side;
                        }
                    }
                    else
                    {
                        CellData prevCell = _worldMap.GetCell(prevX, prevY);
                        CellData currCell = _worldMap.GetCell(mapX,  mapY);

                        bool isPrevSolid = (prevCell != null && prevCell.value != -1);
                        bool isCurrSolid = (currCell != null && currCell.value != -1);

                        // Solid에서 Void로 (가까운 구멍 경계). 이 경계보다 아래쪽(작은 Y)은 앞쪽 바닥이므로 그리지 못하게 막음
                        if (isPrevSolid && !isCurrSolid && voidEdgeDist < 0f)
                        {
                            float nearDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                            
                            // 가림선(clipY)에도 벽과 동일한 실린더 효과를 적용하여 어긋남 방지
                            if (settings.useCylinderEffect)
                            {
                                float distFactor = cameraX * cameraX;
                                nearDist *= (1.0f + distFactor * settings.cylinderStrength);
                            }
                            
                            if (nearDist <= 0.001f) nearDist = 0.001f;
                            int nearLineHeight = (int)((_screenHeight / nearDist) * 0.66f);
                            horizon = (int)(_screenHeight / 2 - player.JumpOffset + player.Pitch);
                            int currentClipY = horizon - nearLineHeight / 2;
                            
                            clipY = Mathf.Max(clipY, currentClipY); 
                        }

                        // Void에서 Solid로 (먼 구멍 경계 또는 측면). 실제로 화면에 보일 내벽 저장
                        if (!isPrevSolid && isCurrSolid)
                        {
                            if (voidEdgeDist < 0f) // 첫 번째로 마주친 구멍 내벽만 저장
                            {
                                voidEdgeDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                                voidEdgeSide = side;
                            }
                        }

                        // 천장 구멍 판정
                        bool isPrevCeilSolid = (prevCell != null && prevCell.value != 1);
                        bool isCurrCeilSolid = (currCell != null && currCell.value != 1);

                        // 천장 Solid에서 Void로의 가림선(ceilClipY) 갱신
                        if (isPrevCeilSolid && !isCurrCeilSolid && ceilEdgeDist < 0f)
                        {
                            float nearDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                            if (settings.useCylinderEffect) nearDist *= (1.0f + cameraX * cameraX * settings.cylinderStrength);
                            if (nearDist <= 0.001f) nearDist = 0.001f;
                            
                            int nearLineHeight = (int)((_screenHeight / nearDist) * 0.66f);
                            horizon = (int)(_screenHeight / 2 - player.JumpOffset + player.Pitch);
                            
                            // 천장의 경계는 화면 위쪽(horizon + height/2)
                            int currentCeilClipY = horizon + nearLineHeight / 2;
                            ceilClipY = Mathf.Min(ceilClipY, currentCeilClipY); // 더 낮게 내려온(가까운) 천장 선을 기준으로 자름
                        }

                        // 천장 Void에서 Solid로의 내벽 거리 저장
                        if (!isPrevCeilSolid && isCurrCeilSolid)
                        {
                            if (ceilEdgeDist < 0f)
                            {
                                ceilEdgeDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                                ceilEdgeSide = side;
                            }
                        }

                        // 벽 충돌 처리
                        if (currCell != null && currCell.HasWall())
                        {
                            int fId = GetTextureIdOnSide(currCell, side, stepX, stepY, false);
                            if (fId != -1) { hit = 1; hitTexId = fId; hitBackFace = false; }
                        }
                        if (hit == 0)
                        {
                            if (prevCell != null && prevCell.HasWall())
                            {
                                int bId = GetTextureIdOnSide(prevCell, side, stepX, stepY, true);
                                if (bId != -1) { hit = 1; hitTexId = bId; hitBackFace = true; }
                            }
                        }
                    }
                }

                if (hitBackFace) { if (side == 0) mapX -= stepX; else mapY -= stepY; }

                // 애니메이션 텍스처 교체
                if (mapX >= 0 && mapX < _tileAnimStates.GetLength(0) && mapY >= 0 && mapY < _tileAnimStates.GetLength(1))
                {
                    var st = _tileAnimStates[mapX, mapY];
                    if (st != null && st.isAnimating && st.showAlt && hitTexId == st.config.baseTexId)
                        hitTexId = st.config.altTexId;
                }

                if (side == 0) perpWallDist = (sideDistX - deltaDistX);
                else perpWallDist = (sideDistY - deltaDistY);

                if (settings.useCylinderEffect)
                {
                    float distFactor = cameraX * cameraX;
                    perpWallDist *= (1.0f + distFactor * settings.cylinderStrength);
                }
                if (perpWallDist <= 0.001f) perpWallDist = 0.001f;
                
                float wallX;
                if (side == 0) wallX = py + perpWallDist * rayDirY;
                else wallX = px + perpWallDist * rayDirX;
                wallX -= Mathf.Floor(wallX);

                if (settings.useOrganicEffect)
                {
                    float t = settings.animTime * settings.organicSpeed;

                    // 각 지점이 독립적으로 꿈틀거리도록 공간 + 시간 노이즈
                    float spatialSeed = wallX * settings.organicFreqX;
                    float noise = Mathf.PerlinNoise(spatialSeed, t);          // 0~1
                    noise = noise * 2f - 1f;                                  // -1~1

                    // 느린 사인파 * 노이즈로 비규칙적 호흡
                    float breathNoise = Mathf.PerlinNoise(spatialSeed * 0.3f, t * 0.4f);
                    float liveAmplitude = settings.organicAmplitude
                                        * Mathf.Lerp(1f - settings.organicBreath,
                                                    1f,
                                                    breathNoise);

                    perpWallDist += noise * liveAmplitude;
                    if (perpWallDist <= 0.001f) perpWallDist = 0.001f;
                }
                
                // 화면 높이 계산
                float hScale = 0.66f; 
                horizon = (int)(_screenHeight / 2 - player.JumpOffset + player.Pitch);
                int lineHeight = (int)((_screenHeight / perpWallDist) * hScale);
                int drawStart = Mathf.Max(0, -lineHeight / 2 + horizon);
                int drawEnd = Mathf.Min(_screenHeight - 1, lineHeight / 2 + horizon);

                if (settings.useMeltEffect)
                {
                    float bumpSeed = wallX * 8.0f + x * 0.005f;
                    float t = settings.animTime * settings.meltEdgeSpeed;
                    int bump = (int)((Mathf.PerlinNoise(bumpSeed, t) * 2f - 1f) * settings.meltEdgeBump);
                    drawStart = Mathf.Max(0, drawStart + bump);
                    drawEnd   = Mathf.Min(_screenHeight - 1, drawEnd + bump);
                }

                int texX = (int)(wallX * _texWidth);
                if ((side == 0 && rayDirX > 0) ^ hitBackFace) texX = _texWidth - texX - 1;
                if ((side == 1 && rayDirY < 0) ^ hitBackFace) texX = _texWidth - texX - 1;

                int lightScale;
                if (settings.useGridLighting)
                {
                    float dist = Mathf.Max(Mathf.Abs(mapX - player.LogicX), Mathf.Abs(mapY - player.LogicY));
                    lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / (dist + 1.0f), 0f, 1f) * 255);
                }
                else
                {
                    lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / perpWallDist, 0f, 1f) * 255);
                }
                if (side == 1) lightScale = (lightScale * 230) >> 8;

                bool isWire = _isScanning && (perpWallDist < _currentScanRadius);
                
                // 수직선(Y루프)을 그리기 전에 공통으로 쓰이는 스캔 변수를 미리 계산
                bool isPulse = false;
                bool isVEdge = false;
                if (isWire)
                {
                    isPulse = Mathf.Abs(perpWallDist - _currentScanRadius) < pulseWidth;
                    isVEdge = (texX == 0 || texX == _texWidth - 1);
                }

                float stepVal = 1.0f * _texHeight / lineHeight;
                
                for (int y = drawStart; y < drawEnd; y++)
                {
                    Color32 col;
                    if (isWire)
                    {
                        bool isHEdge = (y == drawStart || y == drawEnd - 1);
                        col = isPulse ? pulseColor : ((isVEdge || isHEdge) ? wireframeColor : Color.black);
                    }
                    else
                    {
                        int sTexX = texX;
                        int d = y * 256 - _screenHeight * 128 + lineHeight * 128 - (int)player.Pitch * 256 + (int)player.JumpOffset * 256;
                        int texY = ((d * _texHeight) / lineHeight) / 256;

                        if (settings.useWallDistortion) {
                            sTexX = (int)(texX + Mathf.Sin((y + x) * settings.distortionFreq) * settings.distortionAmp) & (_texWidth - 1);
                            float noiseY = Mathf.PerlinNoise(texX * 0.1f, y * settings.distortionFreq);
                            texY = Mathf.Clamp((int)(texY + (noiseY * 2f - 1f) * settings.distortionAmp), 0, _texHeight - 1);
                        }
                        
                        col = GetPixelFast(hitTexId, sTexX, texY);
                        if (lightScale < 255) ApplyLight(ref col, lightScale);
                    }

                    int bIdx = y * _screenWidth + x;
                    for (int s = 0; s < step; s++)
                    {
                        if (x + s < _screenWidth)
                        {
                            _buffer[bIdx + s] = col;
                            _zBuffer[x + s] = perpWallDist;
                        }
                    }
                }

                // void 내벽. horizon부터 그려서 본 벽 하단을 덮어씀
                if (voidEdgeDist > 0f)
                {
                    if (settings.useCylinderEffect)
                    {
                        float distFactor = cameraX * cameraX;
                        voidEdgeDist *= (1.0f + distFactor * settings.cylinderStrength);
                    }

                    DrawVoidWall(x, voidEdgeSide, stepX, stepY,
                                px, py, rayDirX, rayDirY,
                                voidEdgeDist, clipY, // <--- clipY 매개변수 추가
                                player, settings, step,
                                pulseColor, wireframeColor, pulseWidth);
                }

                // 천장 void 내벽 그리기
                if (ceilEdgeDist > 0f)
                {
                    if (settings.useCylinderEffect)
                    {
                        float distFactor = cameraX * cameraX;
                        ceilEdgeDist *= (1.0f + distFactor * settings.cylinderStrength);
                    }

                    DrawCeilVoidWall(x, ceilEdgeSide, stepX, stepY,
                                    px, py, rayDirX, rayDirY,
                                    ceilEdgeDist, ceilClipY, 
                                    player, settings, step,
                                    pulseColor, wireframeColor, pulseWidth);
                }
            }
        }

        private void DrawVoidWall(
            int x, int side, int stepX, int stepY,
            float px, float py, float rayDirX, float rayDirY,
            float perpVoidDist, int clipY,
            DungeonPlayer player, RenderSettings settings, int step,
            Color32 pulseColor, Color32 wireframeColor, float pulseWidth)
        {
            if (perpVoidDist <= 0.001f) perpVoidDist = 0.001f;

            float voidWallX = (side == 0)
                ? (py + perpVoidDist * rayDirY)
                : (px + perpVoidDist * rayDirX);
            voidWallX -= Mathf.Floor(voidWallX);

            int voidTexX = (int)(voidWallX * _texWidth);
            if ((side == 0 && rayDirX > 0)) voidTexX = _texWidth - voidTexX - 1;
            if ((side == 1 && rayDirY < 0)) voidTexX = _texWidth - voidTexX - 1;

            float hScale     = 0.66f;
            int horizon    = (int)(_screenHeight / 2 - player.JumpOffset + player.Pitch);
            int lineHeight = (int)((_screenHeight / perpVoidDist) * hScale);
            int voidHeight = (int)(lineHeight * settings.voidWallHeightScale);

            int floorEdgeY = horizon - lineHeight / 2;

            // 바닥선(floorEdgeY)에서 구멍 깊이(voidHeight)만큼 아래로 내려간 곳부터 바닥선까지 위로 그려줌
            int rawDrawStart = floorEdgeY - voidHeight; // 원래 그려져야 할 바닥 좌표
            int drawStart = Mathf.Max(0, rawDrawStart);
            drawStart = Mathf.Max(drawStart, clipY);    // 앞쪽 바닥(clipY) 이하로는 그리지 않음
            
            int drawEnd = Mathf.Max(0, floorEdgeY);

            if (drawStart >= drawEnd) return;

            int lightScale = (int)(Mathf.Clamp(
                settings.lightingIntensity / perpVoidDist, 0f, 1f) * 255);
            if (side == 1) lightScale = (lightScale * 180) >> 8;

            float stepVal = 1.0f * _texHeight / voidHeight;
            
            // 텍스처 시작점을 0(밑바닥)으로 잡고, 화면 밑(y < 0)으로 잘려나간 만큼 보정
            float texPos = 0f;
            if (drawStart > rawDrawStart)
            {
                texPos = (drawStart - rawDrawStart) * stepVal;
            }

            bool isWire  = _isScanning && (perpVoidDist < _currentScanRadius);
            bool isPulse = isWire && Mathf.Abs(perpVoidDist - _currentScanRadius) < pulseWidth;
            bool isVEdge = isWire && (voidTexX == 0 || voidTexX == _texWidth - 1);

            for (int y = drawStart; y < drawEnd; y++) 
            {
                int voidTexY = (int)texPos & (_texHeight - 1);
                texPos += stepVal;

                Color32 col;
                if (isWire)
                {
                    bool isHEdge = (y == drawStart || y == drawEnd - 1);
                    col = isPulse ? pulseColor : ((isVEdge || isHEdge) ? wireframeColor : Color.black);
                }
                else
                {
                    col = GetPixelFast(settings.voidWallTexIdx, voidTexX, voidTexY);
                    
                    // y가 drawStart(바닥)일 때 0.2, drawEnd(천장)일 때 1.0이 되는 계수 계산. 0.2는 최소 밝기
                    float verticalFactor = Mathf.Clamp01((float)(y - drawStart) / (drawEnd - drawStart));
                    float depthDimmer = Mathf.Lerp(0.2f, 1.0f, verticalFactor); 
                    
                    int finalLight = (int)(lightScale * depthDimmer);

                    if (finalLight < 255) ApplyLight(ref col, finalLight);
                }

                int bIdx = y * _screenWidth + x;
                for (int s = 0; s < step; s++)
                {
                    if (x + s < _screenWidth)
                        _buffer[bIdx + s] = col;
                }
            }
        }

        private void DrawCeilVoidWall(
            int x, int side, int stepX, int stepY,
            float px, float py, float rayDirX, float rayDirY,
            float perpVoidDist, int ceilClipY,
            DungeonPlayer player, RenderSettings settings, int step,
            Color32 pulseColor, Color32 wireframeColor, float pulseWidth)
        {
            if (perpVoidDist <= 0.001f) perpVoidDist = 0.001f;

            float voidWallX = (side == 0) ? (py + perpVoidDist * rayDirY) : (px + perpVoidDist * rayDirX);
            voidWallX -= Mathf.Floor(voidWallX);

            int voidTexX = (int)(voidWallX * _texWidth);
            if ((side == 0 && rayDirX > 0)) voidTexX = _texWidth - voidTexX - 1;
            if ((side == 1 && rayDirY < 0)) voidTexX = _texWidth - voidTexX - 1;

            float hScale = 0.66f;
            int horizon = (int)(_screenHeight / 2 - player.JumpOffset + player.Pitch);
            int lineHeight = (int)((_screenHeight / perpVoidDist) * hScale);
            int voidHeight = (int)(lineHeight * settings.voidWallHeightScale);

            // 천장의 모서리는 화면 위쪽에 위치 (Y값이 큼)
            int ceilEdgeY = horizon + lineHeight / 2;

            int rawDrawStart = ceilEdgeY; 
            int rawDrawEnd = ceilEdgeY + voidHeight; // 구멍 안쪽(더 높은 곳)으로 올라감

            int drawStart = Mathf.Max(0, rawDrawStart);
            int drawEnd = Mathf.Min(_screenHeight, rawDrawEnd);
            drawEnd = Mathf.Min(drawEnd, ceilClipY); // 앞쪽 천장에 가려지는 부분 자르기

            if (drawStart >= drawEnd) return;

            int lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / perpVoidDist, 0f, 1f) * 255);
            if (side == 1) lightScale = (lightScale * 180) >> 8;

            float stepVal = 1.0f * _texHeight / voidHeight;
            float texPos = 0f;
            
            // 화면 밑으로 내려가서 잘린 경우 텍스처 보정
            if (drawStart > rawDrawStart)
            {
                texPos = (drawStart - rawDrawStart) * stepVal;
            }

            bool isWire = _isScanning && (perpVoidDist < _currentScanRadius);
            bool isPulse = isWire && Mathf.Abs(perpVoidDist - _currentScanRadius) < pulseWidth;
            bool isVEdge = isWire && (voidTexX == 0 || voidTexX == _texWidth - 1);

            for (int y = drawStart; y < drawEnd; y++) 
            {
                int voidTexY = (int)texPos & (_texHeight - 1);
                texPos += stepVal;

                Color32 col;
                if (isWire)
                {
                    bool isHEdge = (y == drawStart || y == drawEnd - 1);
                    col = isPulse ? pulseColor : ((isVEdge || isHEdge) ? wireframeColor : Color.black);
                }
                else
                {
                    // settings.voidWallTexIdx가 아닌 천장 구멍 전용 텍스처 인덱스가 있다면 수정 필요
                    col = GetPixelFast(settings.voidWallTexIdx, voidTexX, voidTexY);
                    
                    // 수직 그라데이션. 위로 올라갈수록(y가 커질수록) 어두워짐
                    float verticalFactor = Mathf.Clamp01((float)(y - rawDrawStart) / voidHeight);
                    float depthDimmer = Mathf.Lerp(1.0f, 0.2f, verticalFactor); // 입구(1.0), 깊은곳(0.2)
                    
                    int finalLight = (int)(lightScale * depthDimmer);
                    if (finalLight < 255) ApplyLight(ref col, finalLight);
                }

                int bIdx = y * _screenWidth + x;
                for (int s = 0; s < step; s++)
                {
                    if (x + s < _screenWidth) _buffer[bIdx + s] = col;
                }
            }
        }

        private void CastFloorCeiling(DungeonPlayer player, RenderSettings settings, int step, float px, float py)
        {
            float horizon = _screenHeight / 2 - player.JumpOffset + player.Pitch;
            float hScale = 0.66f; 

            // 캐싱
            Color32 pulseColor = settings.pulseColor;
            Color32 floorWireColor = settings.floorWireframeColor;
            float pulseWidth = settings.pulseWidth;

            for (int y = 0; y < _screenHeight; y++)
            {
                bool isFloor = y < horizon;
                if (!isFloor && _ceilTexIdx == -1 && !_isScanning) continue;

                float p = isFloor ? (horizon - y) : (y - horizon);
                if (p <= 0.1f) p = 0.1f;
                float rowDist = (0.5f * _screenHeight * hScale) / p;

                float rDX0 = player.DirX - player.PlaneX;
                float rDY0 = player.DirY - player.PlaneY;
                float rDX1 = player.DirX + player.PlaneX;
                float rDY1 = player.DirY + player.PlaneY;

                float stepX = rowDist * (rDX1 - rDX0) / _screenWidth * step;
                float stepY = rowDist * (rDY1 - rDY0) / _screenWidth * step;
                float floorX = px + rowDist * rDX0;
                float floorY = py + rowDist * rDY0;

                int texIdx = isFloor ? _floorTexIdx : _ceilTexIdx;
                
                // 가로선(X루프)을 그리기 전에 해당 줄의 펄스 여부를 한 번만 계산
                bool isRowScanned = _isScanning && (rowDist < _currentScanRadius);
                bool isPulseRow = false;
                if (isRowScanned)
                {
                    isPulseRow = Mathf.Abs(rowDist - _currentScanRadius) < pulseWidth;
                }

                // 조명 역시 가로줄은 rowDist가 동일하므로 밖에서 한 번만 계산
                int lightScale = 255;
                if (!isRowScanned) 
                    lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / rowDist, 0f, 1f) * 255);

                for (int x = 0; x < _screenWidth; x += step)
                {
                    int cellX = (int)floorX;
                    int cellY = (int)floorY;

                    CellData cell = _worldMap.GetCell(cellX, cellY);
                    bool isVoid = isFloor && (cell != null && cell.value == -1);
                    Color32 col;

                    if (isVoid)
                    {
                        // 바닥을 검게
                        float darkness = Mathf.Clamp01(1f - rowDist / settings.voidDepthScale);
                        byte bright = (byte)(darkness * 60f); // 최대 밝기 60
                        col = new Color32(bright, bright, bright, 255);
                        // float tx = floorX - cellX; // 0~1, 셀 내 위치
                        // float ty = floorY - cellY;
                        // float edgeDist = Mathf.Min(tx, 1f - tx, ty, 1f - ty); // 가장 가까운 경계까지 거리
                        // float shadow = Mathf.Clamp01(edgeDist * 6f);           // 경계에 가까울수록 0
                        // ApplyLight(ref col, (int)(shadow * lightScale));
                    }
                    else if (isRowScanned)
                    {
                        int tx = (int)(_texWidth * (floorX - cellX)) & (_texWidth - 1);
                        int ty = (int)(_texHeight * (floorY - cellY)) & (_texHeight - 1);
                        
                        bool edge = (tx == 0 || tx == _texWidth - 1 || ty == 0 || ty == _texHeight - 1);
                        col = isPulseRow ? pulseColor : (edge ? floorWireColor : Color.black);
                    }
                    else
                    {
                        int cx = (int)(_texWidth * (floorX - cellX)) & (_texWidth - 1);
                        int cy = (int)(_texHeight * (floorY - cellY)) & (_texHeight - 1);
                        col = GetPixelFast(texIdx, cx, cy);
                            
                        if (lightScale < 255) ApplyLight(ref col, lightScale);
                    }

                    // Step 처리
                    int baseIdx = y * _screenWidth;
                    for (int s = 0; s < step; s++)
                    {
                        if (x + s < _screenWidth) _buffer[baseIdx + x + s] = col;
                    }

                    floorX += stepX;
                    floorY += stepY;
                }
            }
        }

        // 복원된 스프라이트 렌더링 로직
        private void CastSprites(DungeonPlayer player, RenderSettings settings, int step, float px, float py)
        {
            if (_sprtData == null || _sprtData.Length == 0) return;

            // 거리 계산 및 정렬
            for (int i = 0; i < _sprtData.Length; i++)
            {
                _spriteSortList[i].index = i;
                // 멀리 있는 것부터 그려야 하므로 거리 역순 계산을 위해 (또는 Sort에서 내림차순)
                // 유클리드 거리 제곱 사용
                _spriteSortList[i].distance = ((px - _sprtData[i].x) * (px - _sprtData[i].x) + 
                                               (py - _sprtData[i].y) * (py - _sprtData[i].y));
            }
            // 거리 기준 내림차순 정렬 (먼 것 -> 가까운 것)
            Array.Sort(_spriteSortList, (a, b) => b.distance.CompareTo(a.distance));

            // 투영 및 그리기
            float invDet = 1.0f / (player.PlaneX * player.DirY - player.DirX * player.PlaneY);

            for (int i = 0; i < _sprtData.Length; i++)
            {
                int idx = _spriteSortList[i].index;
                float spriteX = _sprtData[idx].x - px;
                float spriteY = _sprtData[idx].y - py;

                // 카메라 공간으로 변환
                float transformX = invDet * (player.DirY * spriteX - player.DirX * spriteY);
                float transformY = invDet * (-player.PlaneY * spriteX + player.PlaneX * spriteY); // Depth

                if (transformY <= 0) continue; // 카메라 뒤쪽

                bool isScanned = _isScanning && (transformY < _currentScanRadius);
                if (isScanned) continue; // 스캔 중에는 스프라이트 숨김

                // 조명 계산
                int lightScale = 255;
                if (settings.useGridLighting)
                {
                    float dist = Mathf.Max(Mathf.Abs(_sprtData[idx].x - player.LogicX), Mathf.Abs(_sprtData[idx].y - player.LogicY));
                    lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / (dist + 1.0f), 0f, 1f) * 255);
                }
                else
                {
                    lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / transformY, 0f, 1f) * 255);
                }

                // 화면 좌표 계산
                int spriteScreenX = (int)((_screenWidth / 2.0f) * (1 + transformX / transformY));
                int spriteHeight = (int)(Mathf.Abs(_screenHeight / transformY)); // 높이는 거리 반비례
                
                // 점프/피치 반영
                int vOffset = (int)(-player.JumpOffset + player.Pitch);
                int drawStartY = -spriteHeight / 2 + _screenHeight / 2 + vOffset;
                if (drawStartY < 0) drawStartY = 0;
                int drawEndY = spriteHeight / 2 + _screenHeight / 2 + vOffset;
                if (drawEndY >= _screenHeight) drawEndY = _screenHeight - 1;

                int spriteWidth = Mathf.Abs((int)(_screenHeight / transformY)); // 정사각형 비율 가정
                int drawStartX = -spriteWidth / 2 + spriteScreenX;
                if (drawStartX < 0) drawStartX = 0;
                int drawEndX = spriteWidth / 2 + spriteScreenX;
                if (drawEndX >= _screenWidth) drawEndX = _screenWidth;

                // 텍스처 데이터 가져오기
                Texture2D tex = _textures[_sprtData[idx].texIdx];
                int texW = tex.width;
                int texH = tex.height;

                // 스트라이프(세로줄) 그리기
                for (int stripe = drawStartX; stripe < drawEndX; stripe += step)
                {
                    int texX = (int)(256 * (stripe - (-spriteWidth / 2 + spriteScreenX)) * texW / spriteWidth) / 256;
                    
                    // ZBuffer 검사 (벽보다 앞에 있는지)
                    // stripe가 화면 범위를 벗어나지 않는지 확인
                    if (stripe >= 0 && stripe < _screenWidth && transformY < _zBuffer[stripe])
                    {
                        for (int y = drawStartY; y < drawEndY; y++)
                        {
                            int d = (y - vOffset) * 256 - _screenHeight * 128 + spriteHeight * 128;
                            int texY = ((d * texH) / spriteHeight) / 256;

                            Color32 col = GetPixelFast(_sprtData[idx].texIdx, texX, texY);
                            
                            if (col.a > 0) // 투명색이 아니면 그리기
                            {
                                if (lightScale < 255) ApplyLight(ref col, lightScale);

                                int bIdx = y * _screenWidth + stripe;
                                
                                // Step 처리 (가로 픽셀 채우기)
                                for (int s = 0; s < step; s++)
                                {
                                    if (stripe + s < _screenWidth && transformY < _zBuffer[stripe + s])
                                    {
                                        _buffer[bIdx + s] = col;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ApplyLight(ref Color32 c, int scale)
        {
            c.r = (byte)((c.r * scale) >> 8);
            c.g = (byte)((c.g * scale) >> 8);
            c.b = (byte)((c.b * scale) >> 8);
        }

        private int GetTextureIdOnSide(CellData cell, int side, int stepX, int stepY, bool back)
        {
            if (!back)
            {
                if (side == 0) return (stepX > 0) ? cell.wallTextureIDs[0] : cell.wallTextureIDs[2];
                else return (stepY > 0) ? cell.wallTextureIDs[3] : cell.wallTextureIDs[1];
            }
            else
            {
                if (side == 0) return (stepX > 0) ? cell.wallTextureIDs[2] : cell.wallTextureIDs[0];
                else return (stepY > 0) ? cell.wallTextureIDs[1] : cell.wallTextureIDs[3];
            }
        }

        private int GetBoundaryTex(int x, int y, int side, int stepX, int stepY)
        {
             CellData last = _worldMap.GetCell(x, y);
             return (last != null) ? GetTextureIdOnSide(last, side, stepX, stepY, true) : 0;
        }
    }
}