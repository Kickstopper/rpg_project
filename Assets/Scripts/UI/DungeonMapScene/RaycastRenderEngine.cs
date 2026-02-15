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
            // 1. 왼쪽 눈 (Red Channel)
            PerformPass(player, settings, 2, true, -settings.stereoSeparation);
            
            Array.Copy(_buffer, _leftEyeBuffer, _buffer.Length);
            
            Array.Clear(_buffer, 0, _buffer.Length);

            // 2. 오른쪽 눈 (Cyan: Green + Blue Channel)
            PerformPass(player, settings, 2, true, settings.stereoSeparation);

            // 3. 병합 (Merge)
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
            for (int x = 0; x < _screenWidth; x += step)
            {
                float cameraX = 2 * x / (float)_screenWidth - 1;
                float rayDirX = player.DirX + player.PlaneX * cameraX;
                float rayDirY = player.DirY + player.PlaneY * cameraX;

                int mapX = Mathf.FloorToInt(px);
                int mapY = Mathf.FloorToInt(py);

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
                while (hit == 0)
                {
                    int prevX = mapX, prevY = mapY;
                    if (sideDistX < sideDistY) { sideDistX += deltaDistX; mapX += stepX; side = 0; }
                    else { sideDistY += deltaDistY; mapY += stepY; side = 1; }

                    if (mapX < 0 || mapX >= _worldMap.width || mapY < 0 || mapY >= _worldMap.height)
                    {
                        hit = 1; hitBackFace = true;
                        hitTexId = GetBoundaryTex(prevX, prevY, side, stepX, stepY);
                    }
                    else
                    {
                        CellData c = _worldMap.GetCell(mapX, mapY);
                        if (c != null && c.HasWall())
                        {
                            int fId = GetTextureIdOnSide(c, side, stepX, stepY, false);
                            if (fId != -1) { hit = 1; hitTexId = fId; hitBackFace = false; }
                        }
                        if (hit == 0)
                        {
                            c = _worldMap.GetCell(prevX, prevY);
                            if (c != null && c.HasWall())
                            {
                                int bId = GetTextureIdOnSide(c, side, stepX, stepY, true);
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

                // 화면 높이 계산
                float hScale = 0.66f; 
                int horizon = (int)(_screenHeight / 2 - player.JumpOffset + player.Pitch);
                int lineHeight = (int)((_screenHeight / perpWallDist) * hScale);
                int drawStart = Mathf.Max(0, -lineHeight / 2 + horizon);
                int drawEnd = Mathf.Min(_screenHeight - 1, lineHeight / 2 + horizon);

                float wallX;
                if (side == 0) wallX = py + perpWallDist * rayDirY;
                else wallX = px + perpWallDist * rayDirX;
                wallX -= Mathf.Floor(wallX);

                int texX = (int)(wallX * _texWidth);
                if ((side == 0 && rayDirX > 0) ^ hitBackFace) texX = _texWidth - texX - 1;
                if ((side == 1 && rayDirY < 0) ^ hitBackFace) texX = _texWidth - texX - 1;

                // 조명 계산
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

                // 벽 그리기 루프
                float stepVal = 1.0f * _texHeight / lineHeight;
                
                for (int y = drawStart; y < drawEnd; y++)
                {
                    Color32 col;
                    if (isWire)
                    {
                        bool pulse = Mathf.Abs(perpWallDist - _currentScanRadius) < settings.pulseWidth;
                        bool edge = (texX == 0 || texX == _texWidth - 1 || y == drawStart || y == drawEnd - 1);
                        col = pulse ? settings.pulseColor : (edge ? settings.wireframeColor : Color.black);
                    }
                    else
                    {
                        int sTexX = texX;
                        if (settings.useWallDistortion)
                            sTexX = (int)(texX + Mathf.Sin((y + x) * settings.distortionFreq) * settings.distortionAmp) & (_texWidth - 1);

                        int d = y * 256 - _screenHeight * 128 + lineHeight * 128 - (int)player.Pitch * 256 + (int)player.JumpOffset * 256;
                        int texY = ((d * _texHeight) / lineHeight) / 256;
                        
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
            }
        }

        private void CastFloorCeiling(DungeonPlayer player, RenderSettings settings, int step, float px, float py)
        {
            float horizon = _screenHeight / 2 - player.JumpOffset + player.Pitch;
            float hScale = 0.66f; 

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

                for (int x = 0; x < _screenWidth; x += step)
                {
                    Color32 col;
                    if (_isScanning && rowDist < _currentScanRadius)
                    {
                        int tx = (int)(_texWidth * (floorX - Mathf.Floor(floorX))) & (_texWidth - 1);
                        int ty = (int)(_texHeight * (floorY - Mathf.Floor(floorY))) & (_texHeight - 1);
                        bool pulse = Mathf.Abs(rowDist - _currentScanRadius) < settings.pulseWidth;
                        bool edge = (tx == 0 || ty == 0);
                        col = pulse ? settings.pulseColor : (edge ? settings.floorWireframeColor : Color.black);
                    }
                    else
                    {
                        int cx = (int)(_texWidth * (floorX - Mathf.Floor(floorX))) & (_texWidth - 1);
                        int cy = (int)(_texHeight * (floorY - Mathf.Floor(floorY))) & (_texHeight - 1);
                        col = GetPixelFast(texIdx, cx, cy);
                        
                        int lightScale = 255;
                        if (settings.useGridLighting)
                            lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / rowDist, 0f, 1f) * 255);
                        else
                            lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / rowDist, 0f, 1f) * 255);
                            
                        if (lightScale < 255) ApplyLight(ref col, lightScale);
                    }

                    for (int s = 0; s < step; s++)
                        if (x + s < _screenWidth) _buffer[y * _screenWidth + (x + s)] = col;

                    floorX += stepX;
                    floorY += stepY;
                }
            }
        }

        // 복원된 스프라이트 렌더링 로직
        private void CastSprites(DungeonPlayer player, RenderSettings settings, int step, float px, float py)
        {
            if (_sprtData == null || _sprtData.Length == 0) return;

            // 1. 거리 계산 및 정렬
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

            // 2. 투영 및 그리기
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