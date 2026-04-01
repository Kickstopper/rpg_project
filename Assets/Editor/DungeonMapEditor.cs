using UnityEngine;
using UnityEditor;
using System.IO;
using Data;
using UI;
using System.Collections.Generic;
public class DungeonMapEditor : EditorWindow
{
    string currentFilePath = null; // 현재 로드된 파일 경로 (null이면 미로드 상태)

    MapData mapData;
    Vector2 scrollPos;
    CellData selectedCell;                          // 단일 선택
    List<CellData> selectedCells = new List<CellData>(); // 다중 선택 목록

    // 맵 크기 입력을 위한 변수
    int inputWidth = 10;
    int inputHeight = 10;

    // ID와 테마 입력을 위한 변수
    
    int inputStartX = 0;
    int inputStartY = 0;
    Direction inputStartDirection = Direction.North; 

    string inputID;
    DungeonTheme inputTheme;

    [MenuItem("Tools/Dungeon Map Editor")]
    public static void ShowWindow()
    {
        GetWindow<DungeonMapEditor>("Dungeon Editor");
    }

    void OnEnable()
    {
        // 초기화 시 기본값 설정
        if (mapData == null) InitializeMap(10, 10, "default", null, 0, 0, 0);
    }

    // 크기를 인자로 받아 초기화
    void InitializeMap(int w, int h, string id, DungeonTheme theme, int startX, int startY, Direction startDir)
    {
        mapData = new MapData();
        mapData.width = w;
        mapData.height = h;
        mapData.mapID = id;
        mapData.themeName = (theme != null) ? theme.name : "";

        // 시작 위치 및 방향 설정
        mapData.startX = startX;
        mapData.startY = startY;
        mapData.startDirection = startDir;

        mapData.cells = new CellData[w * h];
        for (int i = 0; i < mapData.cells.Length; i++)
        {
            mapData.cells[i] = new CellData { x = i % w, y = i / w };
        }
        
        selectedCell = null;
        
        // 에디터 입력값 동기화
        inputWidth = w;
        inputHeight = h;
        inputID = id;
        inputTheme = theme;
        
        // 플레이어 위치 입력값 동기화
        inputStartX = startX;
        inputStartY = startY;
        inputStartDirection = startDir;

        UpdateVisualizer();
        Debug.Log($"New Map Created: ID={id}, Size={w}x{h}, Theme={mapData.themeName}");
    }


    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Map ID 입력
        GUILayout.Label("ID:", GUILayout.Width(20));
        inputID = EditorGUILayout.TextField(inputID, GUILayout.Width(80));

        // 시작 위치 및 방향 입력
        GUILayout.Space(10);
        GUILayout.Label("Player Start:", EditorStyles.boldLabel, GUILayout.Width(80));

        // Start X/Y
        GUILayout.Label("X:", GUILayout.Width(15));
        inputStartX = EditorGUILayout.IntField(inputStartX, GUILayout.Width(30));
        GUILayout.Label("Y:", GUILayout.Width(15));
        inputStartY = EditorGUILayout.IntField(inputStartY, GUILayout.Width(30));

        // Start Direction (Enum 팝업 필드 사용)
        GUILayout.Label("Dir:", GUILayout.Width(25));
        inputStartDirection = (Direction)EditorGUILayout.EnumPopup(inputStartDirection, GUILayout.Width(60));

        // Width / Height 입력
        GUILayout.Label("W:", GUILayout.Width(20));
        inputWidth = EditorGUILayout.IntField(inputWidth, GUILayout.Width(30));
        GUILayout.Label("H:", GUILayout.Width(20));
        inputHeight = EditorGUILayout.IntField(inputHeight, GUILayout.Width(30));

        // DungeonTheme Object Field (드래그 앤 드롭 슬롯)
        GUILayout.Label("Theme:", GUILayout.Width(45));
        // typeof(DungeonTheme)를 사용하여 해당 타입의 에셋만 들어오게 함
        inputTheme = (DungeonTheme)EditorGUILayout.ObjectField(inputTheme, typeof(DungeonTheme), false, GUILayout.Width(150));

        // 생성 버튼
        if (GUILayout.Button("Create", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            if (EditorUtility.DisplayDialog("Create New Map", 
                "Current map data will be lost. Create new?", "Yes", "No"))
            {
                // InitializeMap 호출 시 플레이어 위치/방향 인자 전달
                InitializeMap(inputWidth, inputHeight, inputID, inputTheme, inputStartX, inputStartY, inputStartDirection);
            }
        }

        GUILayout.FlexibleSpace();

        // Refresh 버튼
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ApplyUIToData();    // 입력값 데이터에 반영
            UpdateVisualizer(); // 화면 갱신
            Debug.Log("Map Refreshed");
        }

        if (GUILayout.Button("Load",   EditorStyles.toolbarButton)) LoadMap();

        // Save 버튼. 로드된 파일이 없으면 비활성화
        GUI.enabled = !string.IsNullOrEmpty(currentFilePath);
        if (GUILayout.Button("Save",   EditorStyles.toolbarButton)) SaveMap();
        GUI.enabled = true;

        if (GUILayout.Button("Export", EditorStyles.toolbarButton)) ExportMap();

        EditorGUILayout.EndHorizontal();

        // 현재 파일 경로 표시
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            EditorGUILayout.HelpBox(
                $"Current Map: ID [{mapData.mapID}] / Theme [{mapData.themeName}]\n" +
                $"File: {currentFilePath}",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Current Map: ID [{mapData.mapID}] / Theme [{mapData.themeName}]\n" +
                "File: (unsaved — use Export to save)",
                MessageType.Info);
        }
    }

    void OnGUI()
    {
        // 상단 툴바 (크기 설정 및 파일 저장/로드)
        DrawToolbar();

        if (mapData == null) return;

        EditorGUILayout.BeginHorizontal();
        
        // 좌측: 그리드 맵 뷰
        DrawGridView();

        // 우측: 선택된 셀 속성 편집
        DrawInspectorView();

        EditorGUILayout.EndHorizontal();

        if (GUI.changed)
        {
            UpdateVisualizer();
        }
    }

    void UpdateVisualizer()
    {
        EditorGridVisualizer visualizer = FindFirstObjectByType<EditorGridVisualizer>();
        if (visualizer != null)
        {
            visualizer.mapData = this.mapData; // 데이터 동기화
            
            // 씬 뷰 강제 갱신 (즉시 반영되도록)
            SceneView.RepaintAll(); 
        }
    }

    void LoadMap()
    {
        string path = EditorUtility.OpenFilePanel("Load Map JSON", "", "json");
        if (path.Length != 0)
        {
            string json = File.ReadAllText(path);
            mapData = JsonUtility.FromJson<MapData>(json);
            currentFilePath = path;

            // UI 값 동기화
            inputWidth = mapData.width;
            inputHeight = mapData.height;
            inputID = mapData.mapID;

            // 플레이어 위치 동기화
            inputStartX = mapData.startX;
            inputStartY = mapData.startY;
            inputStartDirection = mapData.startDirection;

            // 저장된 테마 이름으로 프로젝트에서 Theme 파일을 찾아 연결 시도
            if (!string.IsNullOrEmpty(mapData.themeName))
            {
                // Resources 폴더를 사용한다면
                // inputTheme = Resources.Load<DungeonTheme>(mapData.themeName);

                // 에디터 전용. AssetDatabase 검색
                string[] guids = AssetDatabase.FindAssets($"t:DungeonTheme {mapData.themeName}");
                if (guids.Length > 0)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    inputTheme = AssetDatabase.LoadAssetAtPath<DungeonTheme>(assetPath);
                }
                else
                {
                    inputTheme = null;
                    Debug.LogWarning($"Theme '{mapData.themeName}' not found in project.");
                }
            }
            else
            {
                inputTheme = null;
            }

            selectedCell = null;
            UpdateVisualizer();
        }
    }

    void SaveMap()
    {
        if (string.IsNullOrEmpty(currentFilePath))
        {
            // 로드된 파일이 없으면 다른 이름으로 저장(Export)로 대체
            ExportMap();
            return;
        }

        ApplyUIToData();
        string json = JsonUtility.ToJson(mapData, true);
        File.WriteAllText(currentFilePath, json);
        Debug.Log($"Map Saved: {currentFilePath}");
        UpdateVisualizer();
    }

    void ExportMap()
    {
        ApplyUIToData();

        string json = JsonUtility.ToJson(mapData, true);
        string path = EditorUtility.SaveFilePanel("Save Map", "", "dungeon_map", "json");
        if (path.Length != 0)
        {
            File.WriteAllText(path, json);
            currentFilePath = path;
            Debug.Log("Map Saved to: " + path);
            
            // 시각화 갱신
            UpdateVisualizer(); 
        }
    }

    void DrawGridView()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(position.width * 0.7f));

        for (int y = mapData.height - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < mapData.width; x++)
            {
                int index = y * mapData.width + x;
                CellData cell = mapData.cells[index];

                // 다중 선택 포함 여부로 배경색 결정
                bool isSelected = selectedCells.Contains(cell);
                if (isSelected)        GUI.backgroundColor = Color.cyan;
                else if (cell.value == -1) GUI.backgroundColor = Color.coral;
                else if (cell.value == 1) GUI.backgroundColor = Color.brown;
                else                       GUI.backgroundColor = Color.gray;

                if (GUILayout.Button($"{x},{y}", GUILayout.Width(45), GUILayout.Height(45)))
                {
                    bool isCtrl = Event.current.control || Event.current.command; // Mac: Cmd키

                    if (isCtrl)
                    {
                        // Ctrl: 토글 방식으로 추가/제거
                        if (selectedCells.Contains(cell))
                            selectedCells.Remove(cell);
                        else
                            selectedCells.Add(cell);

                        // 마지막 선택된 셀을 Inspector 기준으로 사용
                        selectedCell = selectedCells.Count > 0
                            ? selectedCells[selectedCells.Count - 1]
                            : null;
                    }
                    else
                    {
                        // 일반 클릭: 단일 선택으로 초기화
                        selectedCells.Clear();
                        selectedCells.Add(cell);
                        selectedCell = cell;
                    }

                    UpdateVisualizerSelection(selectedCells);
                    GUI.FocusControl(null);
                }

                // 벽 시각화 (기존 코드 그대로)
                Rect rect = GUILayoutUtility.GetLastRect();
                float wallThickness = 4f;
                Color wallColor = new Color(1f, 0.3f, 0.3f);

                if (cell.wallTextureIDs[0] != -1)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, wallThickness, rect.height), wallColor);
                if (cell.wallTextureIDs[1] != -1)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, wallThickness), wallColor);
                if (cell.wallTextureIDs[2] != -1)
                    EditorGUI.DrawRect(new Rect(rect.xMax - wallThickness, rect.y, wallThickness, rect.height), wallColor);
                if (cell.wallTextureIDs[3] != -1)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - wallThickness, rect.width, wallThickness), wallColor);
            }
            EditorGUILayout.EndHorizontal();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndScrollView();
    }

    void UpdateVisualizerSelection(List<CellData> cells)
    {
        EditorGridVisualizer visualizer = FindFirstObjectByType<EditorGridVisualizer>();
        if (visualizer == null) return;

        visualizer.mapData = this.mapData;
        visualizer.selectedCoords = cells.ConvertAll(c => new Vector2Int(c.x, c.y));
        SceneView.RepaintAll();
    }

    void DrawInspectorView()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.3f));
        GUILayout.Label("Cell Inspector", EditorStyles.boldLabel);

        if (selectedCells.Count > 1)
        {
            // ── 다중 선택 일괄 편집 UI ──
            GUILayout.Label($"{selectedCells.Count} cells selected", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Ctrl+Click to add/remove cells.\nChanges apply to ALL selected cells.", MessageType.Info);

            GUILayout.Space(8);
            GUILayout.Label("Batch Wall Textures", EditorStyles.miniBoldLabel);

            // 방향별 일괄 설정 — "mixed" 상태 표시
            DrawBatchWallField("← Tex", 0);
            DrawBatchWallField("↑ Tex", 1);
            DrawBatchWallField("→ Tex", 2);
            DrawBatchWallField("↓ Tex", 3);

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open All (-1)", EditorStyles.miniButtonLeft))
            {
                foreach (var c in selectedCells)
                    for (int i = 0; i < 4; i++) c.wallTextureIDs[i] = -1;
                GUI.changed = true; GUI.FocusControl(null);
            }
            if (GUILayout.Button("Wall All (0)", EditorStyles.miniButtonRight))
            {
                foreach (var c in selectedCells)
                    for (int i = 0; i < 4; i++) c.wallTextureIDs[i] = 0;
                GUI.changed = true; GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("Batch Value", EditorStyles.miniBoldLabel);
            DrawBatchValueField();

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Value = 0", EditorStyles.miniButtonLeft))
            {
                foreach (var c in selectedCells) c.value = 0;
                GUI.changed = true; GUI.FocusControl(null);
            }
            if (GUILayout.Button("Value = -1", EditorStyles.miniButtonRight))
            {
                foreach (var c in selectedCells) c.value = -1;
                GUI.changed = true; GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
        }
        else if (selectedCells.Count == 1)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.3f));
            GUILayout.Label("Cell Inspector", EditorStyles.boldLabel);

            if (selectedCell != null)
            {
                GUILayout.Label($"Selected: ({selectedCell.x}, {selectedCell.y})");
                
                GUILayout.Space(10);
                GUILayout.Label("Wall Textures (ID)", EditorStyles.miniBoldLabel);
                
                // 4방향 텍스처 ID 입력 (기존 코드)
                selectedCell.wallTextureIDs[0] = EditorGUILayout.IntField("← Tex", selectedCell.wallTextureIDs[0]);
                selectedCell.wallTextureIDs[1] = EditorGUILayout.IntField("↑ Tex", selectedCell.wallTextureIDs[1]);
                selectedCell.wallTextureIDs[2] = EditorGUILayout.IntField("→ Tex", selectedCell.wallTextureIDs[2]);
                selectedCell.wallTextureIDs[3] = EditorGUILayout.IntField("↓ Tex", selectedCell.wallTextureIDs[3]);

                
                // 텍스처 일괄 설정 버튼 (Quick Actions)
                
                GUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                
                // 버튼 1: 모두 비우기 (-1)
                if (GUILayout.Button("Open All (-1)", EditorStyles.miniButtonLeft))
                {
                    for (int i = 0; i < 4; i++) selectedCell.wallTextureIDs[i] = -1;
                    
                    // 벽이 없어졌으므로 셀의 타입도 복도(0)로 바꿀지 결정
                    selectedCell.value = -1; 
                    
                    GUI.changed = true; // 화면 갱신 트리거
                    GUI.FocusControl(null); // 입력 필드 포커스 해제 (값 즉시 반영)
                }

                // 버튼 2: 모두 기본 벽 (0)
                if (GUILayout.Button("Wall All (0)", EditorStyles.miniButtonRight))
                {
                    for (int i = 0; i < 4; i++) selectedCell.wallTextureIDs[i] = 0;
                    
                    // 벽이 생겼으므로 셀의 타입도 벽(1)으로 바꿀지 결정
                    selectedCell.value = 0;

                    GUI.changed = true;
                    GUI.FocusControl(null);
                }
                
                EditorGUILayout.EndHorizontal();
                

                GUILayout.Space(10);
                selectedCell.value = EditorGUILayout.IntField("Value", selectedCell.value);

                GUILayout.Space(20);
                
                
                // 입구 포털 설정 UI
                
                GUILayout.Label("Door / Entrance / Portal Settings", EditorStyles.boldLabel);

                // 현재 셀(x, y)에 존재하는 입구 데이터를 찾음
                // (MapData에 Entrances 리스트가 초기화되어 있어야 함)
                if (mapData.entrances == null) mapData.entrances = new System.Collections.Generic.List<EntranceData>();
                
                EntranceData existingEntrance = mapData.entrances.Find(w => w.sourceX == selectedCell.x && w.sourceY == selectedCell.y);

                if (existingEntrance != null)
                {
                    // 입구 데이터 편집
                    GUI.backgroundColor = new Color(0.8f, 0.8f, 1f);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    GUILayout.Label($"Entrance at ({existingEntrance.sourceX}, {existingEntrance.sourceY})", EditorStyles.miniBoldLabel);
                    // 입구의 타입 (다른 던전맵으로의 입구인가 상점으로의 입구인가)
                    existingEntrance.type = (EntranceType)EditorGUILayout.EnumPopup("Entrance Type", existingEntrance.type);

                    existingEntrance.isWallEntrance = EditorGUILayout.Toggle("Is Wall Entrance", existingEntrance.isWallEntrance);
                    
                    // 트리거 방향 (플레이어가 어느 방향으로 진입해야 하는가)
                    existingEntrance.triggerDirection = (Direction)EditorGUILayout.EnumPopup("Trigger Dir", existingEntrance.triggerDirection);

                    GUILayout.Space(5);
                    GUILayout.Label("Target Destination", EditorStyles.miniBoldLabel);
                    
                    existingEntrance.destinationID = EditorGUILayout.TextField("Destination ID", existingEntrance.destinationID);
                    
                    EditorGUILayout.BeginHorizontal();
                    existingEntrance.targetX = EditorGUILayout.IntField("X", existingEntrance.targetX);
                    existingEntrance.targetY = EditorGUILayout.IntField("Y", existingEntrance.targetY);
                    EditorGUILayout.EndHorizontal();

                    existingEntrance.targetDirection = (Direction)EditorGUILayout.EnumPopup("Face Dir", existingEntrance.targetDirection);

                    GUILayout.Space(10);
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("Remove Entrance"))
                    {
                        mapData.entrances.Remove(existingEntrance);
                        GUI.FocusControl(null); // 포커스 해제
                        GUI.changed = true;
                    }
                    EditorGUILayout.EndVertical();
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    // 입구 추가 버튼
                    if (GUILayout.Button("Add Entrance Portal"))
                    {
                        EntranceData newEntrance = new EntranceData
                        {
                            type = EntranceType.Map,
                            sourceX = selectedCell.x,
                            sourceY = selectedCell.y,
                            isWallEntrance = true,
                            triggerDirection = Direction.North,
                            destinationID = "NewDestination",
                            targetX = 1,
                            targetY = 1,
                            targetDirection = Direction.North
                        };
                        mapData.entrances.Add(newEntrance);
                        GUI.changed = true;
                    }
                }
            }
            else
            {
                GUILayout.Label("Select a cell to edit.");
            }
            EditorGUILayout.EndVertical();
        }
        else
        {
            GUILayout.Label("Select a cell to edit.");
        }

        EditorGUILayout.EndVertical();
    }

    // 방향별 "mixed" 상태를 표시하고 일괄 적용하는 헬퍼
    void DrawBatchWallField(string label, int dirIdx)
    {
        // 선택된 셀들의 값이 모두 같은지 확인
        int firstVal = selectedCells[0].wallTextureIDs[dirIdx];
        bool isMixed = false;
        foreach (var c in selectedCells)
            if (c.wallTextureIDs[dirIdx] != firstVal) { isMixed = true; break; }

        EditorGUI.showMixedValue = isMixed;
        EditorGUI.BeginChangeCheck();
        int newVal = EditorGUILayout.IntField(label, isMixed ? 0 : firstVal);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var c in selectedCells)
                c.wallTextureIDs[dirIdx] = newVal;
            GUI.changed = true;
        }
        EditorGUI.showMixedValue = false;
    }

    void DrawBatchValueField()
    {
        int firstVal = selectedCells[0].value;
        bool isMixed = false;
        foreach (var c in selectedCells)
            if (c.value != firstVal) { isMixed = true; break; }

        EditorGUI.showMixedValue = isMixed;
        EditorGUI.BeginChangeCheck();
        int newVal = EditorGUILayout.IntField("Value", isMixed ? 0 : firstVal);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var c in selectedCells) c.value = newVal;
            GUI.changed = true;
        }
        EditorGUI.showMixedValue = false;
    }

    // UI 입력값을 실제 데이터에 적용하는 헬퍼 함수
    void ApplyUIToData()
    {
        if (mapData == null) return;

        mapData.mapID = inputID;
        mapData.themeName = (inputTheme != null) ? inputTheme.name : "";
        
        mapData.startX = inputStartX;
        mapData.startY = inputStartY;
        mapData.startDirection = inputStartDirection;
    }
}

