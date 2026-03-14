using System.Collections.Generic;
using UnityEngine;
using Data;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Manager
{
    public class ModuleManager : MonoBehaviour
    {
        public static ModuleManager Instance;

        [Header("Configuration")]
        public int maxBlockSize = 10;

        [Header("Reference Data (Drag All SOs here)")]
        public List<GameModuleData> moduleDatabase; 

        private Dictionary<ModuleFeature, GameModuleData> _moduleLookup;

        [Header("Player State (Save This!)")]
        public List<ModuleFeature> ownedModules = new List<ModuleFeature>();
        public List<PlacedModuleData> mountedModules = new List<PlacedModuleData>();

        public int gridWidth = 5;
        public int gridHeight = 5;
        private ModuleFeature[,] expansionBoard;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                _moduleLookup = new Dictionary<ModuleFeature, GameModuleData>();
                foreach (var module in moduleDatabase)
                {
                    if (!_moduleLookup.ContainsKey(module.feature))
                        _moduleLookup.Add(module.feature, module);
                }

                expansionBoard = new ModuleFeature[gridWidth, gridHeight];

                List<PlacedModuleData> defaultModules = new List<PlacedModuleData>(mountedModules);
                mountedModules.Clear();

                foreach (var module in defaultModules)
                {
                    if (module.feature == ModuleFeature.None) continue;

                    if (CanMountModule(module.feature, module.x, module.y, module.rotation))
                    {
                        MountModule(module.feature, module.x, module.y, module.rotation);
                    }
                    else
                    {
                        if (FindAutoMountCoordinate(module.feature, out Vector2Int newPos, out int newRot))
                        {
                            MountModule(module.feature, newPos.x, newPos.y, newRot);
                        }
                        else
                        {
                            Debug.LogWarning($"[ModuleManager] {module.feature}를 마운트할 메모리 공간이 부족합니다.");
                        }
                    }
                }
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
        }

        private bool FindAutoMountCoordinate(ModuleFeature feature, out Vector2Int pos, out int foundRotation)
        {
            pos = Vector2Int.zero;
            foundRotation = 0;
            GameModuleData data = GetModuleData(feature);
            if (data == null) return false;

            for (int r = 0; r < 4; r++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        if (CanMountModule(feature, x, y, r))
                        {
                            pos = new Vector2Int(x, y);
                            foundRotation = r;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void LoadGame(SaveData data)
        {
            maxBlockSize = data.maxBlockSize;
            ownedModules.Clear();
            if (data.ownedModules != null) ownedModules.AddRange(data.ownedModules);

            mountedModules.Clear();
            expansionBoard = new ModuleFeature[gridWidth, gridHeight];

            if (data.mountedModules != null)
            {
                foreach (var module in data.mountedModules)
                {
                    if (_moduleLookup.ContainsKey(module.feature)) 
                        MountModule(module.feature, module.x, module.y, module.rotation); 
                }
            }
        }

        public bool CanMountModule(ModuleFeature feature, int startX, int startY, int rotation = 0)
        {
            GameModuleData data = GetModuleData(feature);
            if (data == null) return false;

            foreach (Vector2Int blockOffset in data.GetRotatedBlocks(rotation))
            {
                int checkX = startX + blockOffset.x;
                int checkY = startY + blockOffset.y;

                if (checkX < 0 || checkX >= gridWidth || checkY < 0 || checkY >= gridHeight) return false;
                if (expansionBoard[checkX, checkY] != ModuleFeature.None) return false;
            }
            return true;
        }

        public void MountModule(ModuleFeature feature, int startX, int startY, int rotation = 0)
        {
            if (!CanMountModule(feature, startX, startY, rotation)) return;

            GameModuleData data = GetModuleData(feature);
            foreach (Vector2Int blockOffset in data.GetRotatedBlocks(rotation))
            {
                expansionBoard[startX + blockOffset.x, startY + blockOffset.y] = feature;
            }
            mountedModules.Add(new PlacedModuleData { feature = feature, x = startX, y = startY, rotation = rotation });
        }

        public void UnmountModule(ModuleFeature feature)
        {
            var moduleToRemove = mountedModules.FirstOrDefault(m => m.feature == feature);
            if (moduleToRemove != null)
            {
                mountedModules.Remove(moduleToRemove);
                GameModuleData data = GetModuleData(feature);
                foreach (Vector2Int blockOffset in data.GetRotatedBlocks(moduleToRemove.rotation))
                {
                    expansionBoard[moduleToRemove.x + blockOffset.x, moduleToRemove.y + blockOffset.y] = ModuleFeature.None;
                }
            }
        }

        public bool IsMounted(ModuleFeature feature) => mountedModules.Any(m => m.feature == feature);
        public List<PlacedModuleData> GetMountedModules() => mountedModules;
        public GameModuleData GetModuleData(ModuleFeature feature)
        {
            if (_moduleLookup.TryGetValue(feature, out GameModuleData data)) return data;
            return null;
        }

        public void SyncexpansionBoardWithMountedModules()
        {
            for (int y = 0; y < gridHeight; y++)
                for (int x = 0; x < gridWidth; x++)
                    expansionBoard[x, y] = ModuleFeature.None;

            foreach (var mountedModule in mountedModules)
            {
                GameModuleData data = GetModuleData(mountedModule.feature);
                if (data == null) continue;

                foreach (Vector2Int blockOffset in data.GetRotatedBlocks(mountedModule.rotation))
                {
                    int targetX = mountedModule.x + blockOffset.x;
                    int targetY = mountedModule.y + blockOffset.y;
                    if (targetX >= 0 && targetX < gridWidth && targetY >= 0 && targetY < gridHeight)
                    {
                        expansionBoard[targetX, targetY] = mountedModule.feature;
                    }
                }
            }
        }

        // 인스펙터 우클릭 메뉴에 자동 배치 버튼 생성
        [ContextMenu("빈 공간에 자동 배치 (Auto Arrange)")]
        public void AutoArrangeDefaultModules()
        {
            ModuleFeature[,] tempBoard = new ModuleFeature[gridWidth, gridHeight];
            Dictionary<ModuleFeature, GameModuleData> tempLookup = new Dictionary<ModuleFeature, GameModuleData>();
            
            foreach (var appData in moduleDatabase)
            {
                if (appData != null && !tempLookup.ContainsKey(appData.feature))
                {
                    tempLookup.Add(appData.feature, appData);
                }
            }

            // 인스펙터의 installedModules를 순회하며 자리 찾기
            for (int i = 0; i < mountedModules.Count; i++)
            {
                PlacedModuleData currentModule = mountedModules[i];
                
                if (currentModule.feature == ModuleFeature.None) continue;
                
                if (!tempLookup.ContainsKey(currentModule.feature))
                {
                    Debug.LogWarning($"[ModuleManager] '{currentModule.feature}' 데이터를 appDatabase에서 찾을 수 없습니다.");
                    continue;
                }

                GameModuleData data = tempLookup[currentModule.feature];
                bool isPlaced = false;

                // 상단 왼쪽부터 우측 하단 방향으로 스캔
                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        if (CanPlaceModuleInTempBoard(data, x, y, tempBoard))
                        {
                            // 위치 지정
                            currentModule.x = x;
                            currentModule.y = y;
                            mountedModules[i] = currentModule; // 변경된 좌표 적용

                            // 다음 앱이 겹치지 않도록 임시 보드에 블록 채우기
                            foreach (Vector2Int block in data.shapeBlocks)
                            {
                                tempBoard[x + block.x, y + block.y] = currentModule.feature;
                            }
                            
                            isPlaced = true;
                            break;
                        }
                    }
                    if (isPlaced) break;
                }

                if (!isPlaced)
                {
                    Debug.LogError($"[ModuleManager] '{currentModule.feature}'를 배치할 공간이 부족합니다! (Grid 한도 초과)");
                }
            }

            Debug.Log("[ModuleManager] 기본 앱 자동 배치 완료!");

            // 씬을 저장할 때 함께 저장되도록 마킹
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        // 임시 보드용 충돌 검사 함수
        private bool CanPlaceModuleInTempBoard(GameModuleData data, int startX, int startY, ModuleFeature[,] tempBoard)
        {
            foreach (Vector2Int blockOffset in data.shapeBlocks)
            {
                int checkX = startX + blockOffset.x;
                int checkY = startY + blockOffset.y;

                if (checkX < 0 || checkX >= gridWidth || checkY < 0 || checkY >= gridHeight)
                    return false;

                if (tempBoard[checkX, checkY] != ModuleFeature.None)
                    return false;
            }
            return true;
        }
    }
}