using System;
using UnityEngine;
using Data;
using Manager;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UI.DungeonMapScene
{
    public struct SpriteInfo
    {
        public float x;
        public float y;
        public int texIdx;
        public bool isEnemy;
        public bool isFallen; // 몬스터가 넘어진 상태인지 렌더러에 알려주는 변수
    }

    public class RaycastRenderEngine
    {
        private struct IllusionHit
        {
            public float perpWallDist;
            public int texX;
            public int hitTexId;
            public int side;
            public int mapX;
            public int mapY;
        }
        private IllusionHit[] _illusionHits = new IllusionHit[32]; 
        private HashSet<int> _passableTexIDs = new HashSet<int>();

        private Color32[] _buffer;
        private Color32[] _leftEyeBuffer;
        
        // 1D 버퍼(솔리드 벽 방어)와 2D 버퍼(환영의 벽 투과)를 동시에 사용
        private float[] _depthBuffer; // 2D 픽셀 뎁스
        private float[] _zBuffer1D;   // 1D 라인 뎁스

        private Color32[] _flatWallPixels;   
        private Color32[] _flatSpritePixels; 
        private Dictionary<int, Color32[]> _flatObjectPixels;  
        private Dictionary<int, Vector2Int> _objectDimensions; 

        private MapData _worldMap;
        private Texture2D[] _wallTextures;
        private Sprite[] _enemySprite;
        private Dictionary<int, Texture2D> _objectSpriteDict;
        
        private SpriteInfo[] _sprtData;
        private SpriteSortInfo[] _spriteSortList;
        private TileAnimState[,] _tileAnimStates;
        
        private int _texWidth, _texHeight;
        private int _screenWidth, _screenHeight;
        private int _ceilTexIdx, _floorTexIdx;
        
        private bool _isScanning;
        private float _currentScanRadius;

        private struct DustParticle
        {
            public float x, y, z;
            public float speed, phase;
        }
        private DustParticle[] _dustArray;

        public Texture2D ScreenTexture { get; private set; }

        public void Initialize(int width, int height)
        {
            _screenWidth = width;
            _screenHeight = height;
            _buffer = new Color32[width * height];
            _leftEyeBuffer = new Color32[width * height];
            
            _depthBuffer = new float[width * height]; 
            _zBuffer1D = new float[width]; // 1D 버퍼 추가 할당
            
            ScreenTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            ScreenTexture.filterMode = FilterMode.Point;
        }

        public void LoadAssets(DungeonTheme theme, Sprite[] dynamicEnemySprites, int texW, int texH, SpriteInfo[] spriteData)
        {
            _wallTextures = theme.texture;
            _enemySprite = dynamicEnemySprites;

            _objectSpriteDict = new Dictionary<int, Texture2D>();
            _flatObjectPixels = new Dictionary<int, Color32[]>();
            _objectDimensions = new Dictionary<int, Vector2Int>();

            if (theme.objectSprites != null)
            {
                foreach (var obj in theme.objectSprites)
                {
                    if (obj.texture != null)
                    {
                        _objectSpriteDict[obj.objectID] = obj.texture;
                        _flatObjectPixels[obj.objectID] = obj.texture.GetPixels32();
                        _objectDimensions[obj.objectID] = new Vector2Int(obj.texture.width, obj.texture.height);
                    }
                }
            }
            
            _texWidth = texW;
            _texHeight = texH;
            _sprtData = spriteData;
            
            if (_sprtData != null) _spriteSortList = new SpriteSortInfo[_sprtData.Length];

            PrecomputeTextures();
        }

        private Color32 GetObjectSpritePixelFast(int objId, int x, int y)
        {
            if (_flatObjectPixels != null && _flatObjectPixels.TryGetValue(objId, out Color32[] pixels))
            {
                Vector2Int dim = _objectDimensions[objId];
                x = Mathf.Clamp(x, 0, dim.x - 1);
                y = Mathf.Clamp(y, 0, dim.y - 1);
                
                return pixels[y * dim.x + x];
            }
            return new Color32(0, 0, 0, 0);
        }

        private void PrecomputeTextures()
        {
            int pxPerTex = _texWidth * _texHeight;

            if (_wallTextures != null && _wallTextures.Length > 0)
            {
                _flatWallPixels = new Color32[_wallTextures.Length * pxPerTex];
                for (int i = 0; i < _wallTextures.Length; i++)
                {
                    Color[] src = _wallTextures[i].GetPixels();
                    int offset = i * pxPerTex;
                    for (int p = 0; p < src.Length; p++)
                        _flatWallPixels[offset + p] = (Color32)src[p];
                }
            }

            if (_enemySprite != null && _enemySprite.Length > 0)
            {
                _flatSpritePixels = new Color32[_enemySprite.Length * pxPerTex];
                for (int i = 0; i < _enemySprite.Length; i++)
                {
                    Sprite spr = _enemySprite[i];
                    if (spr == null) continue;

                    Color[] src = spr.texture.GetPixels(
                        (int)spr.rect.x, 
                        (int)spr.rect.y, 
                        (int)spr.rect.width, 
                        (int)spr.rect.height
                    );

                    int offset = i * pxPerTex;
                    for (int p = 0; p < src.Length; p++)
                    {
                        _flatSpritePixels[offset + p] = (Color32)src[p];
                    }
                }
            }
        }

        public void UpdateFloorCeilingTex(int floorTexIdx, int ceilTexIdx)
        {
            _floorTexIdx = floorTexIdx;
            _ceilTexIdx = ceilTexIdx;
        }

        public void UpdateSprites(SpriteInfo[] sprites)
        {
            _sprtData = sprites;
            if (_sprtData != null)
            {
                if (_spriteSortList == null || _spriteSortList.Length < _sprtData.Length)
                {
                    _spriteSortList = new SpriteSortInfo[_sprtData.Length];
                }
            }
        }

        public void SetMapData(MapData map, DungeonTheme theme, TileAnimState[,] animStates)
        {
            _worldMap = map;
            _ceilTexIdx = theme.ceilingTexIdx;
            _floorTexIdx = theme.floorTexIdx;
            _tileAnimStates = animStates;

            if (theme.passableWallTexIDs != null)
                _passableTexIDs = new HashSet<int>(theme.passableWallTexIDs);
            else
                _passableTexIDs.Clear();
        }

        public void SetScanState(bool scanning, float radius)
        {
            _isScanning = scanning;
            _currentScanRadius = radius;
        }

        private Color32 GetWallPixelFast(int texIdx, int x, int y)
        {
            if (_wallTextures == null || texIdx < 0 || texIdx >= _wallTextures.Length) 
                return new Color32(255, 0, 255, 255); 
            
            x &= (_texWidth - 1);
            y &= (_texHeight - 1);
            return _flatWallPixels[(texIdx * _texWidth * _texHeight) + (y * _texWidth) + x];
        }

        private Color32 GetEnemySpritePixelFast(int texIdx, int x, int y)
        {
            if (_enemySprite == null || texIdx < 0 || texIdx >= _enemySprite.Length) 
                return new Color32(0, 0, 0, 0); 
            
            x &= (_texWidth - 1);
            y &= (_texHeight - 1);
            return _flatSpritePixels[(texIdx * _texWidth * _texHeight) + (y * _texWidth) + x];
        }

        // ================= 메인 렌더링 루프 =================
        public void RenderFrame(DungeonPlayer player, RenderSettings settings)
        {
            settings.animTime = Time.time;

            Array.Clear(_buffer, 0, _buffer.Length);
            
            // 두 개의 뎁스 버퍼 모두 무한대로 초기화
            for (int i = 0; i < _depthBuffer.Length; i++) _depthBuffer[i] = 10000f;
            for (int i = 0; i < _zBuffer1D.Length; i++) _zBuffer1D[i] = 10000f;

            if (ManagerRoot.GameSetting.useAnaglyph)
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

        private void RenderStereo(DungeonPlayer player, RenderSettings settings)
        {
            PerformPass(player, settings, 2, true, -settings.stereoSeparation);
            Array.Copy(_buffer, _leftEyeBuffer, _buffer.Length);
            
            Array.Clear(_buffer, 0, _buffer.Length);
            
            // 오른쪽 눈 그리기 전 뎁스 버퍼 재초기화
            for (int i = 0; i < _depthBuffer.Length; i++) _depthBuffer[i] = 10000f;
            for (int i = 0; i < _zBuffer1D.Length; i++) _zBuffer1D[i] = 10000f;

            PerformPass(player, settings, 2, true, settings.stereoSeparation);

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
            
            if (isStereo)
            {
                rPosX += player.PlaneX * stereoOffset;
                rPosY += player.PlaneY * stereoOffset;
            }

            CastFloorCeiling(player, settings, step, rPosX, rPosY);
            CastWalls(player, settings, step, rPosX, rPosY);
            if (_sprtData != null && _sprtData.Length > 0)
                CastSprites(player, settings, step, rPosX, rPosY);

            if (settings.useDustEffect)
                CastDust(player, settings, step);
        }

        // ================= 세부 렌더링 로직 =================
        private void CastWalls(DungeonPlayer player, RenderSettings settings, int step, float px, float py)
        {
            Color32 pulseColor = settings.pulseColor;
            Color32 wireframeColor = settings.wireframeColor;
            float pulseWidth = settings.pulseWidth;

            for (int x = 0; x < _screenWidth; x += step)
            {
                int illusionHitCount = 0;

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

                float voidEdgeDist = -1f;
                int   voidEdgeSide = 0;
                int   clipY = 0; 
                float ceilEdgeDist = -1f;
                int   ceilEdgeSide = 0;
                int   ceilClipY = _screenHeight; 
                
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

                        if (isPrevSolid && !isCurrSolid && voidEdgeDist < 0f)
                        {
                            float nearDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                            if (settings.useCylinderEffect) nearDist *= (1.0f + cameraX * cameraX * settings.cylinderStrength);
                            if (nearDist <= 0.001f) nearDist = 0.001f;
                            int nearLineHeight = (int)((_screenHeight / nearDist) * 0.66f);
                            horizon = (int)(_screenHeight / 2 - player.JumpOffset + player.Pitch);
                            clipY = Mathf.Max(clipY, horizon - nearLineHeight / 2); 
                        }

                        if (!isPrevSolid && isCurrSolid)
                        {
                            if (voidEdgeDist < 0f) 
                            {
                                voidEdgeDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                                voidEdgeSide = side;
                            }
                        }

                        bool isPrevCeilSolid = (prevCell != null && prevCell.value != 1);
                        bool isCurrCeilSolid = (currCell != null && currCell.value != 1);

                        if (isPrevCeilSolid && !isCurrCeilSolid && ceilEdgeDist < 0f)
                        {
                            float nearDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                            if (settings.useCylinderEffect) nearDist *= (1.0f + cameraX * cameraX * settings.cylinderStrength);
                            if (nearDist <= 0.001f) nearDist = 0.001f;
                            int nearLineHeight = (int)((_screenHeight / nearDist) * 0.66f);
                            horizon = (int)(_screenHeight / 2 - player.JumpOffset + player.Pitch);
                            ceilClipY = Mathf.Min(ceilClipY, horizon + nearLineHeight / 2); 
                        }

                        if (!isPrevCeilSolid && isCurrCeilSolid)
                        {
                            if (ceilEdgeDist < 0f)
                            {
                                ceilEdgeDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                                ceilEdgeSide = side;
                            }
                        }

                        if (currCell != null && currCell.HasWall())
                        {
                            int fId = GetTextureIdOnSide(currCell, side, stepX, stepY, false);
                            if (fId != -1) 
                            { 
                                if (!_passableTexIDs.Contains(fId)) 
                                {
                                    hit = 1; hitTexId = fId; hitBackFace = false; 
                                }
                                else 
                                {
                                    if (illusionHitCount < 32)
                                    {
                                        float perpDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                                        if (settings.useCylinderEffect) perpDist *= (1.0f + cameraX * cameraX * settings.cylinderStrength);
                                        if (perpDist <= 0.001f) perpDist = 0.001f;

                                        float wX = (side == 0) ? py + perpDist * rayDirY : px + perpDist * rayDirX;
                                        wX -= Mathf.Floor(wX);
                                        int tX = (int)(wX * _texWidth);
                                        if ((side == 0 && rayDirX > 0)) tX = _texWidth - tX - 1;
                                        if ((side == 1 && rayDirY < 0)) tX = _texWidth - tX - 1;

                                        _illusionHits[illusionHitCount++] = new IllusionHit { perpWallDist = perpDist, texX = tX, hitTexId = fId, side = side, mapX = mapX, mapY = mapY };
                                    }
                                }
                            }
                        }
                        if (hit == 0)
                        {
                            if (prevCell != null && prevCell.HasWall())
                            {
                                int bId = GetTextureIdOnSide(prevCell, side, stepX, stepY, true);
                                if (bId != -1) 
                                { 
                                    if (!_passableTexIDs.Contains(bId))
                                    {
                                        hit = 1; hitTexId = bId; hitBackFace = true; 
                                    }
                                    else 
                                    {
                                        if (illusionHitCount < 32)
                                        {
                                            float perpDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);
                                            if (settings.useCylinderEffect) perpDist *= (1.0f + cameraX * cameraX * settings.cylinderStrength);
                                            if (perpDist <= 0.001f) perpDist = 0.001f;

                                            float wX = (side == 0) ? py + perpDist * rayDirY : px + perpDist * rayDirX;
                                            wX -= Mathf.Floor(wX);
                                            int tX = (int)(wX * _texWidth);
                                            if ((side == 0 && rayDirX > 0) ^ true) tX = _texWidth - tX - 1;
                                            if ((side == 1 && rayDirY < 0) ^ true) tX = _texWidth - tX - 1;

                                            _illusionHits[illusionHitCount++] = new IllusionHit { perpWallDist = perpDist, texX = tX, hitTexId = bId, side = side, mapX = prevX, mapY = prevY };
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (hitBackFace) { if (side == 0) mapX -= stepX; else mapY -= stepY; }

                if (mapX >= 0 && mapX < _tileAnimStates.GetLength(0) && mapY >= 0 && mapY < _tileAnimStates.GetLength(1))
                {
                    var st = _tileAnimStates[mapX, mapY];
                    if (st != null && st.isAnimating && st.config.frameTexIDs != null && st.config.frameTexIDs.Length > 0)
                    {
                        if (hitTexId == st.config.frameTexIDs[0])
                        {
                            hitTexId = st.config.frameTexIDs[st.currentFrame];
                        }
                    }
                }

                if (side == 0) perpWallDist = (sideDistX - deltaDistX);
                else perpWallDist = (sideDistY - deltaDistY);

                if (settings.useCylinderEffect)
                {
                    float distFactor = cameraX * cameraX;
                    perpWallDist *= (1.0f + distFactor * settings.cylinderStrength);
                }
                if (perpWallDist <= 0.001f) perpWallDist = 0.001f;
                
                // 1D 뎁스 버퍼 기록 (전체 세로줄에 대한 진짜 솔리드 벽 깊이 등록)
                for (int s = 0; s < step; s++)
                {
                    if (x + s < _screenWidth) _zBuffer1D[x + s] = perpWallDist;
                }

                float wallX;
                if (side == 0) wallX = py + perpWallDist * rayDirY;
                else wallX = px + perpWallDist * rayDirX;
                wallX -= Mathf.Floor(wallX);

                if (settings.useOrganicEffect)
                {
                    float t = settings.animTime * settings.organicSpeed;
                    float spatialSeed = wallX * settings.organicFreqX;
                    float noise = Mathf.PerlinNoise(spatialSeed, t) * 2f - 1f;                                  
                    float breathNoise = Mathf.PerlinNoise(spatialSeed * 0.3f, t * 0.4f);
                    float liveAmplitude = settings.organicAmplitude * Mathf.Lerp(1f - settings.organicBreath, 1f, breathNoise);
                    perpWallDist += noise * liveAmplitude;
                    if (perpWallDist <= 0.001f) perpWallDist = 0.001f;
                }
                
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
                bool isPulse = false;
                bool isVEdge = false;
                if (isWire)
                {
                    isPulse = Mathf.Abs(perpWallDist - _currentScanRadius) < pulseWidth;
                    isVEdge = (texX == 0 || texX == _texWidth - 1);
                }

                if (lightScale <= 0 && !isWire)
                {
                    for (int y = drawStart; y < drawEnd; y++)
                    {
                        int bIdx = y * _screenWidth + x;
                        for (int s = 0; s < step; s++)
                        {
                            if (x + s < _screenWidth)
                            {
                                _buffer[bIdx + s] = settings.fogColor;
                                _depthBuffer[bIdx + s] = perpWallDist; 
                            }
                        }
                    }
                }
                else
                {
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
                            
                            col = GetWallPixelFast(hitTexId, sTexX, texY);
                            if (lightScale < 255) ApplyLight(ref col, lightScale, settings.fogColor);
                        }

                        int bIdx = y * _screenWidth + x;
                        for (int s = 0; s < step; s++)
                        {
                            if (x + s < _screenWidth)
                            {
                                _buffer[bIdx + s] = col;
                                _depthBuffer[bIdx + s] = perpWallDist;
                            }
                        }
                    }
                }

                if (voidEdgeDist > 0f)
                {
                    if (settings.useCylinderEffect)
                    {
                        float distFactor = cameraX * cameraX;
                        voidEdgeDist *= (1.0f + distFactor * settings.cylinderStrength);
                    }

                    DrawVoidWall(x, voidEdgeSide, stepX, stepY,
                                px, py, rayDirX, rayDirY,
                                voidEdgeDist, clipY,
                                player, settings, step,
                                pulseColor, wireframeColor, pulseWidth);
                }

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

                for (int i = illusionHitCount - 1; i >= 0; i--)
                {
                    IllusionHit illusion = _illusionHits[i];
                    
                    int aLineHeight = (int)((_screenHeight / illusion.perpWallDist) * hScale);
                    int aDrawStart = Mathf.Max(0, -aLineHeight / 2 + horizon);
                    int aDrawEnd = Mathf.Min(_screenHeight - 1, aLineHeight / 2 + horizon);
                    
                    int aLightScale;
                    if (settings.useGridLighting)
                    {
                        float dist = Mathf.Max(Mathf.Abs(illusion.mapX - player.LogicX), Mathf.Abs(illusion.mapY - player.LogicY));
                        aLightScale = (int)(Mathf.Clamp(settings.lightingIntensity / (dist + 1.0f), 0f, 1f) * 255);
                    }
                    else
                    {
                        aLightScale = (int)(Mathf.Clamp(settings.lightingIntensity / illusion.perpWallDist, 0f, 1f) * 255);
                    }
                    if (illusion.side == 1) aLightScale = (aLightScale * 230) >> 8;

                    for (int y = aDrawStart; y < aDrawEnd; y++)
                    {
                        int d = y * 256 - _screenHeight * 128 + aLineHeight * 128 - (int)player.Pitch * 256 + (int)player.JumpOffset * 256;
                        int texY = ((d * _texHeight) / aLineHeight) / 256;

                        Color32 col = GetWallPixelFast(illusion.hitTexId, illusion.texX, texY);
                        
                        if (col.a > 0)
                        {
                            if (aLightScale <= 0) col = settings.fogColor; 
                            else if (aLightScale < 255) ApplyLight(ref col, aLightScale, settings.fogColor);

                            int bIdx = y * _screenWidth + x;
                            for (int s = 0; s < step; s++)
                            {
                                if (x + s < _screenWidth)
                                {
                                    if (illusion.perpWallDist < _depthBuffer[bIdx + s])
                                    {
                                        _buffer[bIdx + s] = col;
                                        _depthBuffer[bIdx + s] = illusion.perpWallDist; 
                                    }
                                }
                            }
                        }
                    }
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

            float voidWallX = (side == 0) ? (py + perpVoidDist * rayDirY) : (px + perpVoidDist * rayDirX);
            voidWallX -= Mathf.Floor(voidWallX);

            int voidTexX = (int)(voidWallX * _texWidth);
            if ((side == 0 && rayDirX > 0)) voidTexX = _texWidth - voidTexX - 1;
            if ((side == 1 && rayDirY < 0)) voidTexX = _texWidth - voidTexX - 1;

            float hScale     = 0.66f;
            int horizon    = (int)(_screenHeight / 2 - player.JumpOffset + player.Pitch);
            int lineHeight = (int)((_screenHeight / perpVoidDist) * hScale);
            int voidHeight = (int)(lineHeight * settings.voidWallHeightScale);

            int floorEdgeY = horizon - lineHeight / 2;
            int rawDrawStart = floorEdgeY - voidHeight; 
            int drawStart = Mathf.Max(0, rawDrawStart);
            drawStart = Mathf.Max(drawStart, clipY);    
            int drawEnd = Mathf.Max(0, floorEdgeY);
            drawEnd = Mathf.Min(drawEnd, _screenHeight);

            if (drawStart >= drawEnd) return;

            int lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / perpVoidDist, 0f, 1f) * 255);
            if (side == 1) lightScale = (lightScale * 180) >> 8;

            float stepVal = 1.0f * _texHeight / voidHeight;
            float texPos = 0f;
            if (drawStart > rawDrawStart) texPos = (drawStart - rawDrawStart) * stepVal;

            bool isWire = _isScanning && (perpVoidDist < _currentScanRadius);
            bool isPulse = false;
            bool isVEdge = false;
            if (isWire)
            {
                isPulse = Mathf.Abs(perpVoidDist - _currentScanRadius) < pulseWidth;
                isVEdge = (voidTexX == 0 || voidTexX == _texWidth - 1);
            }

            if (lightScale <= 0 && !isWire)
            {
                for (int y = drawStart; y < drawEnd; y++)
                {
                    int bIdx = y * _screenWidth + x;
                    for (int s = 0; s < step; s++)
                    {
                        if (x + s < _screenWidth)
                        {
                            _buffer[bIdx + s] = settings.fogColor;
                            _depthBuffer[bIdx + s] = perpVoidDist; 
                        }
                    }
                }
            }
            else 
            {
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
                        col = GetWallPixelFast(settings.voidWallTexIdx, voidTexX, voidTexY);
                        float verticalFactor = Mathf.Clamp01((float)(y - drawStart) / (drawEnd - drawStart));
                        float depthDimmer = Mathf.Lerp(0.2f, 1.0f, verticalFactor); 
                        
                        int finalLight = (int)(lightScale * depthDimmer);
                        if (finalLight < 255) ApplyLight(ref col, finalLight, settings.fogColor);
                    }

                    int bIdx = y * _screenWidth + x;
                    for (int s = 0; s < step; s++)
                    {
                        if (x + s < _screenWidth)
                        {
                            _buffer[bIdx + s] = col;
                            _depthBuffer[bIdx + s] = perpVoidDist; 
                        }
                    }
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

            int ceilEdgeY = horizon + lineHeight / 2;
            int rawDrawStart = ceilEdgeY; 
            int rawDrawEnd = ceilEdgeY + voidHeight; 

            int drawStart = Mathf.Max(0, rawDrawStart);
            int drawEnd = Mathf.Min(_screenHeight, rawDrawEnd);
            drawEnd = Mathf.Min(drawEnd, ceilClipY); 

            if (drawStart >= drawEnd) return;

            int lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / perpVoidDist, 0f, 1f) * 255);
            if (side == 1) lightScale = (lightScale * 180) >> 8;

            float stepVal = 1.0f * _texHeight / voidHeight;
            float texPos = 0f;
            if (drawStart > rawDrawStart) texPos = (drawStart - rawDrawStart) * stepVal;

            bool isWire = _isScanning && (perpVoidDist < _currentScanRadius);
            bool isPulse = false;
            bool isVEdge = false;
            if (isWire)
            {
                isPulse = Mathf.Abs(perpVoidDist - _currentScanRadius) < pulseWidth;
                isVEdge = (voidTexX == 0 || voidTexX == _texWidth - 1);
            }

            if (lightScale <= 0 && !isWire)
            {
                for (int y = drawStart; y < drawEnd; y++)
                {
                    int bIdx = y * _screenWidth + x;
                    for (int s = 0; s < step; s++)
                    {
                        if (x + s < _screenWidth)
                        {
                            _buffer[bIdx + s] = settings.fogColor;
                            _depthBuffer[bIdx + s] = perpVoidDist; 
                        }
                    }
                }
            }
            else
            {
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
                        col = GetWallPixelFast(settings.voidWallTexIdx, voidTexX, voidTexY);
                        float verticalFactor = Mathf.Clamp01((float)(y - rawDrawStart) / voidHeight);
                        float depthDimmer = Mathf.Lerp(1.0f, 0.2f, verticalFactor); 
                        
                        int finalLight = (int)(lightScale * depthDimmer);
                        if (finalLight < 255) ApplyLight(ref col, finalLight, settings.fogColor);
                    }

                    int bIdx = y * _screenWidth + x;
                    for (int s = 0; s < step; s++)
                    {
                        if (x + s < _screenWidth)
                        {
                            _buffer[bIdx + s] = col;
                            _depthBuffer[bIdx + s] = perpVoidDist; 
                        }
                    }
                }
            }
        }

        private void CastFloorCeiling(DungeonPlayer player, RenderSettings settings, int step, float px, float py)
        {
            float horizon = _screenHeight / 2 - player.JumpOffset + player.Pitch;
            float hScale = 0.66f; 

            Color32 pulseColor = settings.pulseColor;
            Color32 floorWireColor = settings.floorWireframeColor;
            float pulseWidth = settings.pulseWidth;

            for (int y = 0; y < _screenHeight; y++)
            {
                bool isFloor = y < horizon;
                // 맵 데이터에 천장이 없으면 천장(isFloor == false) 부분은 픽셀 렌더링을 생략
                if (!isFloor && !_worldMap.hasCeil) continue;

                // _ceilTexIdx == -1 이더라도 개별 셀에 천장이 있을 수 있으므로 가로줄 전체 스킵 최적화 코드를 비활성화
                // if (!isFloor && _ceilTexIdx == -1 && !_isScanning) continue;

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
                
                bool isRowScanned = _isScanning && (rowDist < _currentScanRadius);
                bool isPulseRow = false;
                if (isRowScanned)
                {
                    isPulseRow = Mathf.Abs(rowDist - _currentScanRadius) < pulseWidth;
                }

                int lightScale = 255;
                if (!isRowScanned) 
                    lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / rowDist, 0f, 1f) * 255);

                if (lightScale <= 0 && !isRowScanned)
                {
                    int baseIdx = y * _screenWidth;
                    for (int x = 0; x < _screenWidth; x += step)
                    {
                        for (int s = 0; s < step; s++)
                        {
                            if (x + s < _screenWidth) _buffer[baseIdx + x + s] = settings.fogColor;
                        }
                    }
                    continue;
                }

                for (int x = 0; x < _screenWidth; x += step)
                {
                    int cellX = (int)floorX;
                    int cellY = (int)floorY;

                    CellData cell = _worldMap.GetCell(cellX, cellY);
                    bool isVoid = isFloor && (cell != null && cell.value == -1);
                    Color32 col;

                    if (isVoid)
                    {
                        float darkness = Mathf.Clamp01(1f - rowDist / settings.voidDepthScale);
                        byte bright = (byte)(darkness * 60f); 
                        col = new Color32(bright, bright, bright, 255);
                        
                        if (lightScale < 255) ApplyLight(ref col, lightScale, settings.fogColor);
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
                        // 현재 셀의 바닥/천장 텍스처를 우선하고 -1이면 fallback 텍스처 사용
                        int activeTexIdx = isFloor ? _floorTexIdx : _ceilTexIdx;
                        
                        if (cell != null)
                        {
                            if (isFloor && cell.floorTexIdx != -1) activeTexIdx = cell.floorTexIdx;
                            if (!isFloor && cell.ceilTexIdx != -1) activeTexIdx = cell.ceilTexIdx;
                        }

                        // Fallback까지 거쳤는데도 최종 인덱스가 -1이라면, 픽셀 렌더링을 스킵
                        if (activeTexIdx == -1)
                        {
                            floorX += stepX;
                            floorY += stepY;
                            continue;
                        }

                        int cx = (int)(_texWidth * (floorX - cellX)) & (_texWidth - 1);
                        int cy = (int)(_texHeight * (floorY - cellY)) & (_texHeight - 1);
                        col = GetWallPixelFast(activeTexIdx, cx, cy);
                            
                        if (lightScale < 255) ApplyLight(ref col, lightScale, settings.fogColor);
                    }

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

        private void CastDust(DungeonPlayer player, RenderSettings settings, int step)
        {
            if (_dustArray == null || _dustArray.Length != settings.dustParticleCount)
            {
                if (settings.dustParticleCount <= 0) return; 

                _dustArray = new DustParticle[settings.dustParticleCount];
                for (int i = 0; i < _dustArray.Length; i++)
                {
                    _dustArray[i] = new DustParticle
                    {
                        x = UnityEngine.Random.Range(0f, 10f),
                        y = UnityEngine.Random.Range(0f, 10f),
                        z = UnityEngine.Random.Range(0f, 2f),
                        speed = UnityEngine.Random.Range(0.02f, 0.08f),
                        phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
                    };
                }
            }

            if (_dustArray.Length == 0) return;

            float invDet = 1.0f / (player.PlaneX * player.DirY - player.DirX * player.PlaneY);
            float horizon = _screenHeight / 2 - player.JumpOffset + player.Pitch;

            Color32 baseDustColor = settings.dustColor;
            float t = settings.animTime;
            float verticalDir = settings.dustMovesUp ? -1f : 1f;

            for (int i = 0; i < _dustArray.Length; i++)
            {
                var p = _dustArray[i];

                float currentZ = (p.z + t * p.speed * verticalDir) % 2.0f;
                if (currentZ < 0) currentZ += 2.0f;
                currentZ -= 0.5f;

                float currentX = p.x + Mathf.Sin(t + p.phase) * settings.dustSwayAmplitude;
                float currentY = p.y + Mathf.Cos(t + p.phase * 0.8f) * settings.dustSwayAmplitude;

                float dx = currentX - player.PosX;
                float dy = currentY - player.PosY;

                dx = (dx % 10f + 10f) % 10f - 5f;
                dy = (dy % 10f + 10f) % 10f - 5f;

                float transformX = invDet * (player.DirY * dx - player.DirX * dy);
                float transformY = invDet * (-player.PlaneY * dx + player.PlaneX * dy);

                if (transformY > 0.1f)
                {
                    int screenX = (int)((_screenWidth / 2.0f) * (1.0f + transformX / transformY));
                    float eyeDiff = currentZ - 0.5f; 
                    int screenY = (int)(horizon - (eyeDiff * _screenHeight / transformY));

                    if (screenX >= 0 && screenX < _screenWidth && screenY >= 0 && screenY < _screenHeight)
                    {
                        Color32 finalColor = baseDustColor;

                        if (settings.useDustTwinkle)
                        {
                            float twinkleFactor = (Mathf.Sin(t * settings.dustTwinkleSpeed + p.phase * 5f) + 1f) * 0.5f;
                            float brightness = Mathf.Lerp(0.2f, 1.0f, twinkleFactor);

                            finalColor.r = (byte)(finalColor.r * brightness);
                            finalColor.g = (byte)(finalColor.g * brightness);
                            finalColor.b = (byte)(finalColor.b * brightness);
                        }

                        int lightScale = (int)(Mathf.Clamp(settings.lightingIntensity / transformY, 0f, 1f) * 255);
                        ApplyLight(ref finalColor, lightScale, settings.fogColor);

                        int particleSize = (transformY < 1.5f) ? 3 : 2; 

                        for (int pY = 0; pY < particleSize; pY++)
                        {
                            int drawY = screenY + pY;
                            if (drawY >= _screenHeight || drawY < 0) continue; 
                            int rowIdx = drawY * _screenWidth;

                            for (int pX = 0; pX < particleSize * step; pX++)
                            {
                                int drawX = screenX + pX;
                                if (drawX < 0 || drawX >= _screenWidth) continue; 

                                // 먼지도 하이브리드 버퍼(Mathf.Min)를 사용하여 깊이를 판정
                                float pixelDepth = Mathf.Min(_zBuffer1D[drawX], _depthBuffer[rowIdx + drawX]);
                                if (transformY < pixelDepth)
                                {
                                    _buffer[rowIdx + drawX] = finalColor;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CastSprites(DungeonPlayer player, RenderSettings settings, int step, float px, float py)
        {
            if (_sprtData == null || _sprtData.Length == 0) return;

            for (int i = 0; i < _sprtData.Length; i++)
            {
                _spriteSortList[i].index = i;
                _spriteSortList[i].distance = ((px - _sprtData[i].x) * (px - _sprtData[i].x) + 
                                               (py - _sprtData[i].y) * (py - _sprtData[i].y));
            }

            for (int i = _sprtData.Length; i < _spriteSortList.Length; i++)
            {
                _spriteSortList[i].index = 0;      
                _spriteSortList[i].distance = -1f; 
            }

            Array.Sort(_spriteSortList, (a, b) => b.distance.CompareTo(a.distance));

            float invDet = 1.0f / (player.PlaneX * player.DirY - player.DirX * player.PlaneY);

            for (int i = 0; i < _sprtData.Length; i++)
            {
                int idx = _spriteSortList[i].index;
                float spriteX = _sprtData[idx].x - px;
                float spriteY = _sprtData[idx].y - py;

                float transformX = invDet * (player.DirY * spriteX - player.DirX * spriteY);
                float transformY = invDet * (-player.PlaneY * spriteX + player.PlaneX * spriteY); 

                if (transformY <= 0) continue; 

                bool isScanned = _isScanning && (transformY < _currentScanRadius);
                if (isScanned) continue; 

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

                int spriteScreenX = (int)((_screenWidth / 2.0f) * (1 + transformX / transformY));
                int spriteHeight = (int)(Mathf.Abs(_screenHeight / transformY)); 
                
                int vOffset = (int)(-player.JumpOffset + player.Pitch);
                int drawStartY = -spriteHeight / 2 + _screenHeight / 2 + vOffset;
                if (drawStartY < 0) drawStartY = 0;
                int drawEndY = spriteHeight / 2 + _screenHeight / 2 + vOffset;
                if (drawEndY >= _screenHeight) drawEndY = _screenHeight - 1;

                int spriteWidth = Mathf.Abs((int)(_screenHeight / transformY)); 
                int drawStartX = -spriteWidth / 2 + spriteScreenX;
                if (drawStartX < 0) drawStartX = 0;
                int drawEndX = spriteWidth / 2 + spriteScreenX;
                if (drawEndX >= _screenWidth) drawEndX = _screenWidth;

                int idOrIdx = _sprtData[idx].texIdx;
                bool isEnemy = _sprtData[idx].isEnemy;

                // 현재 그릴 스프라이트가 넘어진 몬스터인지 확인
                bool isFallen = _sprtData[idx].isFallen; 

                // 픽셀 루프를 돌기 전에, 깜빡임 비율을 미리 계산하여 CPU 부하를 없앰
                float blend = 0f;
                float invBlend = 1f;
                byte blinkR = 255; byte blinkG = 255; byte blinkB = 255; // 깜빡일 색상

                if (isFallen)
                {
                    // 시간(animTime)에 따라 0.0 ~ 1.0 사이를 오가는 사인파 생성 (16f는 깜빡임 속도)
                    float blinkFactor = (Mathf.Sin(settings.animTime * 16f) + 1f) * 0.5f;
                    blend = blinkFactor * 0.8f; // 최대 80% 까지만 색 혼합
                    invBlend = 1f - blend;
                }
                
                int texW = _texWidth; 
                int texH = _texHeight;

                if (isEnemy)
                {
                    if (_enemySprite != null && idOrIdx >= 0 && idOrIdx < _enemySprite.Length)
                    {
                        Sprite spr = _enemySprite[idOrIdx];
                        if (spr != null)
                        {
                            texW = (int)spr.rect.width; 
                            texH = (int)spr.rect.height;
                        }
                    }
                }
                else
                {
                    if (_objectDimensions != null && _objectDimensions.TryGetValue(idOrIdx, out Vector2Int dim))
                    {
                        texW = dim.x;
                        texH = dim.y;
                    }
                }

                for (int stripe = drawStartX; stripe < drawEndX; stripe += step)
                {
                    int texX = (int)(256 * (stripe - (-spriteWidth / 2 + spriteScreenX)) * texW / spriteWidth) / 256;
                    
                    if (stripe >= 0 && stripe < _screenWidth)
                    {
                        for (int y = drawStartY; y < drawEndY; y++)
                        {
                            int d = (y - vOffset) * 256 - _screenHeight * 128 + spriteHeight * 128;
                            int texY = ((d * texH) / spriteHeight) / 256;

                            Color32 col;
                            
                            if (isEnemy) col = GetEnemySpritePixelFast(idOrIdx, texX, texY);
                            else         col = GetObjectSpritePixelFast(idOrIdx, texX, texY);
                            
                            if (col.a == 255) 
                            {
                                // 넘어진 상태라면 미리 계산한 비율(blend)대로 색상을 고속 블렌딩
                                if (isFallen)
                                {
                                    col.r = (byte)((col.r * invBlend) + (blinkR * blend));
                                    col.g = (byte)((col.g * invBlend) + (blinkG * blend));
                                    col.b = (byte)((col.b * invBlend) + (blinkB * blend));
                                }

                                if (lightScale <= 0) col = settings.fogColor; 
                                else if (lightScale < 255) ApplyLight(ref col, lightScale, settings.fogColor);
                                
                                int bIdx = y * _screenWidth + stripe;
                                
                                for (int s = 0; s < step; s++)
                                {
                                    // 몬스터도 하이브리드 버퍼(Mathf.Min)를 통과한 경우에만 렌더링하고 깊이를 기록
                                    float pixelDepth = Mathf.Min(_zBuffer1D[stripe + s], _depthBuffer[bIdx + s]);
                                    
                                    if (stripe + s < _screenWidth && transformY < pixelDepth)
                                    {
                                        _buffer[bIdx + s] = col;
                                        _depthBuffer[bIdx + s] = transformY;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyLight(ref Color32 c, int scale, Color32 fog)
        {
            int invScale = 255 - scale; 
            
            c.r = (byte)(((c.r * scale) + (fog.r * invScale)) >> 8);
            c.g = (byte)(((c.g * scale) + (fog.g * invScale)) >> 8);
            c.b = (byte)(((c.b * scale) + (fog.b * invScale)) >> 8);
        }

        private int GetTextureIdOnSide(CellData cell, int side, int stepX, int stepY, bool back)
        {
            if (!back)
            {
                if (side == 0) return (stepX > 0) ? cell.wallTextureIDs[3] : cell.wallTextureIDs[1]; 
                else           return (stepY > 0) ? cell.wallTextureIDs[2] : cell.wallTextureIDs[0]; 
            }
            else
            {
                if (side == 0) return (stepX > 0) ? cell.wallTextureIDs[1] : cell.wallTextureIDs[3];
                else           return (stepY > 0) ? cell.wallTextureIDs[0] : cell.wallTextureIDs[2];
            }
        }

        private int GetBoundaryTex(int x, int y, int side, int stepX, int stepY)
        {
             CellData last = _worldMap.GetCell(x, y);
             return (last != null) ? GetTextureIdOnSide(last, side, stepX, stepY, true) : 0;
        }
    }
}