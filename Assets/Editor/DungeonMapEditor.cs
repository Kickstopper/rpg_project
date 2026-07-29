using UnityEngine;
using UnityEditor;
using System.IO;
using Data;
using UI;
using System.Collections.Generic;
using System;
public class DungeonMapEditor : EditorWindow
{
    string currentFilePath = null; // 현재 로드된 파일 경로 (null이면 미로드 상태)

    string[] availableMapIDs = new string[] { 
        "Outpost", "Bridge_0", 
        "Underground_0", "Underground_0_0", "Underground_1", "Underworld_0", "Underworld_1",
        "Tower_0", "Tower_1", "Tower_2", "Tower_3", "Tower_4", "Tower_5", "Tower_6", "Tower_7", 
        "Tower_8", "Tower_9", "Tower_10", "Tower_11", 
        "Cave_0", "Cave_1", "Cave_2", "Cave_3", "Cave_4", "Cave_5", "Cave_6",
        "Labyrinth_0", "Labyrinth_1", "Labyrinth_2", "Labyrinth_3", "Labyrinth_4", "Labyrinth_5", 
        "VampireCastle_0","VampireCastle_1","VampireCastle_2","VampireCastle_3","VampireCastle_4","VampireCastle_5","VampireCastle_6","VampireCastle_7","VampireCastle_8","VampireCastle_9"
    }; 
    bool isInvalidIDLoaded = false;

    MapData mapData;
    Vector2 scrollPos;
    CellData selectedCell;                          // 단일 선택
    List<CellData> selectedCells = new List<CellData>(); // 다중 선택 목록

    bool _isDragging = false;   // 드래그 확정 상태
    bool _dragPending = false;  // MouseDown 이후 이동 확인 대기
    Vector2 _dragStartPos;      // 드래그 시작 마우스 위치
    CellData _dragStartCell;    // 드래그 시작 셀
    const float DragThreshold = 5f; // 드래그 인정 거리 (px)

    // 맵 크기 입력을 위한 변수
    int inputWidth = 10;
    int inputHeight = 10;

    // ID와 테마 입력을 위한 변수
    
    int inputStartX = 0;
    int inputStartY = 0;
    Direction inputStartDirection = Direction.North; 

    string inputMapID;
    string inputLocationID;
    DungeonTheme inputTheme;
    bool inputHasCeil = true;

    [MenuItem("Tools/Dungeon Map Editor")]
    public static void ShowWindow()
    {
        GetWindow<DungeonMapEditor>("Dungeon Editor");
    }

    void OnEnable()
    {
        // 초기화 시 기본값 설정
        if (mapData == null) InitializeMap(10, 10, "MapId", "None (Location)", null, 0, 0, 0);
    }

    // 크기를 인자로 받아 초기화
    void InitializeMap(int w, int h, string mapId, string locationID, DungeonTheme theme, int startX, int startY, Direction startDir, bool hasCeil = true)
    {
        mapData = new MapData();
        mapData.width = w;
        mapData.height = h;
        mapData.mapID = mapId;
        mapData.locationID = locationID;
        mapData.themeName = (theme != null) ? theme.name : "";
        mapData.hasCeil = hasCeil;

        // 시작 위치 및 방향 설정
        mapData.startX = startX;
        mapData.startY = startY;
        mapData.startDirection = startDir;

        mapData.cells = new CellData[w * h];
        mapData.entrances = new List<EntranceData>();
        
        for (int i = 0; i < mapData.cells.Length; i++)
        {
            mapData.cells[i] = new CellData { x = i % w, y = i / w };
        }
        
        selectedCell = null;
        
        // 에디터 입력값 동기화
        inputWidth = w;
        inputHeight = h;
        inputMapID = mapId;
        inputLocationID = locationID;
        inputTheme = theme;
        
        // 플레이어 위치 입력값 동기화
        inputStartX = startX;
        inputStartY = startY;
        inputStartDirection = startDir;
        inputHasCeil = hasCeil;
        UpdateVisualizer();
        Debug.Log($"New Map Created: ID={mapId}, Size={w}x{h}, Theme={mapData.themeName}");
    }


    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("ID", GUILayout.Width(20));

        // 현재 inputMapID가 배열의 몇 번째 인덱스인지 확인
        int selectedIndex = Array.IndexOf(availableMapIDs, inputMapID);

        // Popup에 -1이 들어가면 자동으로 빈 칸을 표시함
        int newIndex = EditorGUILayout.Popup(selectedIndex, availableMapIDs, GUILayout.Width(100));

        if (newIndex >= 0 && newIndex != selectedIndex)
        {
            inputMapID = availableMapIDs[newIndex];
            isInvalidIDLoaded = false;
        }

        // 로케이션 ID 입력
        GUILayout.Space(10);
        GUILayout.Label("LocationID", GUILayout.Width(65));
        inputLocationID = EditorGUILayout.TextField(inputLocationID, GUILayout.Width(100));

        // 시작 위치 및 방향 입력
        GUILayout.Space(10);
        GUILayout.Label("Player Start", EditorStyles.boldLabel, GUILayout.Width(80));

        // Start X/Y
        GUILayout.Label("X", GUILayout.Width(15));
        inputStartX = EditorGUILayout.IntField(inputStartX, GUILayout.Width(30));
        GUILayout.Label("Y", GUILayout.Width(15));
        inputStartY = EditorGUILayout.IntField(inputStartY, GUILayout.Width(30));

        // Start Direction (Enum 팝업 필드 사용)
        GUILayout.Label("Dir", GUILayout.Width(25));
        inputStartDirection = (Direction)EditorGUILayout.EnumPopup(inputStartDirection, GUILayout.Width(60));

        // Width / Height 입력
        GUILayout.Label("W", GUILayout.Width(20));
        inputWidth = EditorGUILayout.IntField(inputWidth, GUILayout.Width(30));
        GUILayout.Label("H", GUILayout.Width(20));
        inputHeight = EditorGUILayout.IntField(inputHeight, GUILayout.Width(30));

        // 천장 토글 UI
        GUILayout.Space(5);
        GUILayout.Label("Ceil", GUILayout.Width(25));
        inputHasCeil = EditorGUILayout.Toggle(inputHasCeil, GUILayout.Width(15));

        // DungeonTheme Object Field (드래그 앤 드롭 슬롯)
        GUILayout.Label("Theme", GUILayout.Width(45));
        // typeof(DungeonTheme)를 사용하여 해당 타입의 에셋만 들어오게 함
        inputTheme = (DungeonTheme)EditorGUILayout.ObjectField(inputTheme, typeof(DungeonTheme), false, GUILayout.Width(150));

        // 생성 버튼
        if (GUILayout.Button("Create", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            if (EditorUtility.DisplayDialog("Create New Map", 
                "Current map data will be lost. Create new?", "Yes", "No"))
            {
                // InitializeMap 호출 시 플레이어 위치/방향 인자 전달
                InitializeMap(inputWidth, inputHeight, inputMapID, inputLocationID, inputTheme, 
                              inputStartX, inputStartY, inputStartDirection, inputHasCeil);
            }
        }
        // 절차적 미로 생성 버튼
        if (GUILayout.Button("Gen Maze", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            if (EditorUtility.DisplayDialog("Generate Maze", 
                "현재 맵 데이터가 무작위 미로로 덮어씌워집니다. 진행하시겠습니까?", "Yes", "No"))
            {
                string themeName = (inputTheme != null) ? inputTheme.name : "";
                
                // Generator 폴더(네임스페이스)의 스크립트 호출하여 맵 데이터 생성
                mapData = Generator.DungeonGenerator.GenerateWizardryMaze(
                    inputWidth, inputHeight, inputMapID, themeName, 0, 0.05f); // 0.05f는 루프 생성 확률
                
                // 생성된 데이터를 에디터 입력 필드 UI와 동기화
                mapData.locationID = inputLocationID;
                inputStartX = mapData.startX;
                inputStartY = mapData.startY;
                inputStartDirection = mapData.startDirection;
                inputWidth = mapData.width;
                inputHeight = mapData.height;

                selectedCells.Clear();
                selectedCell = null;

                UpdateVisualizer();
                GUI.FocusControl(null); // 포커스 해제하여 UI 즉시 갱신
                
                Debug.Log($"[DungeonEditor] {inputWidth}x{inputHeight} 크기의 무작위 미로가 생성되었습니다!");
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
        
        // 잘못된 ID가 로드되었을 때 경고
        if (isInvalidIDLoaded)
        {
            EditorGUILayout.HelpBox(
                "로드된 파일의 Map ID가 유효한 목록에 없어 삭제되었습니다. 반드시 올바른 Map ID를 다시 선택한 후 저장하세요.", 
                MessageType.Warning);
        }
    }

    void OnGUI()
    {
        Event e = Event.current;

        // Ctrl+S 단축키
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.S && (e.control || e.command))
        { SaveMap(); e.Use(); }

        // 마우스 버튼을 놓으면 드래그 종료
        if (e.type == EventType.MouseUp)
        {
            _isDragging = false;
            _dragPending = false;
        }

        DrawToolbar();
        if (mapData == null) return;

        EditorGUILayout.BeginHorizontal();
        DrawGridView();
        DrawInspectorView();
        EditorGUILayout.EndHorizontal();

        if (GUI.changed) UpdateVisualizer();
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

            // 로드된 ID가 availableMapIDs 배열에 있는지 확인
            if (System.Array.IndexOf(availableMapIDs, mapData.mapID) == -1)
            {
                Debug.LogWarning($"[DungeonEditor] 로드된 맵 ID '{mapData.mapID}'는 유효한 목록에 없습니다. 빈 칸으로 초기화됩니다.");
                inputMapID = ""; // 배열에 없으면 빈 칸으로 덮어씀
                isInvalidIDLoaded = true;
            }
            else
            {
                inputMapID = mapData.mapID; // 배열에 있으면 정상 동기화
                isInvalidIDLoaded = false;
            }

            // UI 값 동기화
            inputWidth = mapData.width;
            inputHeight = mapData.height;
            inputMapID = mapData.mapID;
            inputLocationID = mapData.locationID;

            // 플레이어 위치 동기화
            inputStartX = mapData.startX;
            inputStartY = mapData.startY;
            inputStartDirection = mapData.startDirection;
            inputHasCeil = mapData.hasCeil;

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

        // 파일명으로 사용할 변수 설정 (ID가 없으면 기본값 사용)
        string defaultFileName = string.IsNullOrEmpty(inputMapID) ? "dungeon_map" : inputMapID;

        // SaveFilePanel의 세 번째 인자에 변수 전달
        string path = EditorUtility.SaveFilePanel("Save Map", "", defaultFileName, "json");

        if (path.Length != 0)
        {
            File.WriteAllText(path, json);
            currentFilePath = path;
            Debug.Log($"Map Exported: {path}");
            
            UpdateVisualizer(); 
        }
    }

    void DrawGridView()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(position.width * 0.7f));

        Event e = Event.current;
        bool isCtrl = e.control || e.command;

        for (int y = mapData.height - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < mapData.width; x++)
            {
                int index = y * mapData.width + x;
                CellData cell = mapData.cells[index];
                bool isSelected = selectedCells.Contains(cell);
                bool hasEntrance = mapData.entrances != null && 
                                   mapData.entrances.Exists(e => e.sourceX == cell.x && e.sourceY == cell.y);

                GUI.backgroundColor = isSelected         ? Color.cyan
                                    : hasEntrance        ? Color.red     // 입구가 있으면 빨간색
                                    : cell.value == -1   ? Color.white
                                    : cell.value == 1    ? Color.brown
                                                         : Color.gray;

                // GUILayout.Button 대신 Rect를 먼저 예약
                float cellSize = 38f;
                Rect rect = GUILayoutUtility.GetRect(
                    new GUIContent($"{x},{y}"),
                    GUI.skin.button,
                    GUILayout.Width(cellSize), GUILayout.Height(cellSize));

                bool mouseOverCell = rect.Contains(e.mousePosition);

                // 드래그 대기 시작 (Ctrl + MouseDown)
                if (isCtrl && e.type == EventType.MouseDown && mouseOverCell)
                {
                    _dragPending  = true;
                    _dragStartPos  = e.mousePosition;
                    _dragStartCell = cell;
                }

                // 드래그 대기 중 마우스가 충분히 이동하면 드래그 확정
                if (_dragPending && e.type == EventType.MouseDrag)
                {
                    if (Vector2.Distance(e.mousePosition, _dragStartPos) > DragThreshold)
                    {
                        _isDragging   = true;
                        _dragPending  = false;

                        // 드래그 시작 셀을 선택 목록에 추가
                        if (!selectedCells.Contains(_dragStartCell))
                        {
                            selectedCells.Add(_dragStartCell);
                            selectedCell = _dragStartCell;
                        }
                        UpdateVisualizerSelection(selectedCells);
                        Repaint();
                    }
                }

                // 드래그 확정 상태에서 마우스가 올라온 셀을 선택 목록에 추가
                if (_isDragging && mouseOverCell &&
                    (e.type == EventType.MouseDrag || e.type == EventType.Repaint))
                {
                    if (!selectedCells.Contains(cell))
                    {
                        selectedCells.Add(cell);
                        selectedCell = cell;
                        UpdateVisualizerSelection(selectedCells);
                    }
                }

                // 드래그 중이 아닐 때만 버튼 클릭 처리
                if (GUI.Button(rect, $"{x},{y}") && !_isDragging)
                {
                    if (isCtrl)
                    {
                        // 토글
                        if (selectedCells.Contains(cell)) selectedCells.Remove(cell);
                        else selectedCells.Add(cell);
                        selectedCell = selectedCells.Count > 0
                            ? selectedCells[selectedCells.Count - 1] : null;
                    }
                    else
                    {
                        selectedCells.Clear();
                        selectedCells.Add(cell);
                        selectedCell = cell;
                    }
                    UpdateVisualizerSelection(selectedCells);
                    GUI.FocusControl(null);
                }

                // 벽 시각화
                float defaultWt = 3f; // 꽉 막힌 벽 및 문의 두께
                float passableWt = 1.5f; // 통과 가능한 환영의 벽 두께 (얇게)

                for (int i = 0; i < 4; i++)
                {
                    int texID = cell.wallTextureIDs[i];
                    if (texID != -1)
                    {
                        // 통과 가능한 환영의 벽인지 검사
                        bool isPassable = (inputTheme != null && 
                                           inputTheme.passableWallTexIDs != null && 
                                           inputTheme.passableWallTexIDs.Contains(texID));

                        // 텍스처 ID가 테마에 등록된 문(Door)인지 검사
                        bool isDoor = false;
                        if (inputTheme != null && inputTheme.doorAnimations != null)
                        {
                            foreach (var door in inputTheme.doorAnimations)
                            {
                                if (door.closedTexId == texID)
                                {
                                    isDoor = true;
                                    break;
                                }
                            }
                        }

                        // 상태에 따른 색상 및 두께 할당
                        Color wc;
                        float wt = defaultWt;

                        if (isPassable)
                        {
                            wc = new Color(0f, 1f, 1f, 0.8f); // 환영의 벽: 시안색(Cyan)
                            wt = passableWt;
                        }
                        else if (isDoor)
                        {
                            wc = new Color(0.2f, 0.9f, 0.2f, 1f); // 문(Door): 밝은 녹색(Green)
                        }
                        else
                        {
                            wc = new Color(1f, 0.3f, 0.3f, 1f); // 꽉 막힌 벽: 빨간색(Red)
                        }

                        // 렌더링
                        if (isPassable)
                        {
                            // 통과 가능한 벽은 점선으로 렌더링
                            int dashCount = 4;
                            float dashStepX = rect.width / (dashCount * 2 - 1);
                            float dashStepY = rect.height / (dashCount * 2 - 1);

                            for (int d = 0; d < dashCount; d++)
                            {
                                if (i == 0) EditorGUI.DrawRect(new Rect(rect.x + d * dashStepX * 2, rect.y, dashStepX, wt), wc); // 북쪽 (Top)
                                else if (i == 1) EditorGUI.DrawRect(new Rect(rect.xMax - wt, rect.y + d * dashStepY * 2, wt, dashStepY), wc); // 동쪽 (Right)
                                else if (i == 2) EditorGUI.DrawRect(new Rect(rect.x + d * dashStepX * 2, rect.yMax - wt, dashStepX, wt), wc); // 남쪽 (Bottom)
                                else if (i == 3) EditorGUI.DrawRect(new Rect(rect.x, rect.y + d * dashStepY * 2, wt, dashStepY), wc); // 서쪽 (Left)
                            }
                        }
                        else
                        {
                            // 일반 벽과 문은 굵은 실선으로 렌더링
                            if (i == 0) EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, wt), wc); // 북쪽 (Top)
                            else if (i == 1) EditorGUI.DrawRect(new Rect(rect.xMax - wt, rect.y, wt, rect.height), wc); // 동쪽 (Right)
                            else if (i == 2) EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - wt, rect.width, wt), wc); // 남쪽 (Bottom)
                            else if (i == 3) EditorGUI.DrawRect(new Rect(rect.x, rect.y, wt, rect.height), wc); // 서쪽 (Left)
                        }
                    }
                }
                
                // 중앙 고정 오브젝트 시각화
                if (cell.centerObjectID != -1)
                {
                    Rect centerObjRect = new Rect(rect.x + rect.width * 0.35f, rect.y + rect.height * 0.35f, rect.width * 0.3f, rect.height * 0.3f);
                    EditorGUI.DrawRect(centerObjRect, Color.yellow);
                    
                    GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
                    style.alignment = TextAnchor.MiddleCenter;
                    style.normal.textColor = Color.black;
                    GUI.Label(centerObjRect, cell.centerObjectID.ToString(), style);
                }

                // 이벤트(보스, NPC 등) 배치 시각화
                if (!string.IsNullOrEmpty(cell.eventID))
                {
                    // 1회성은 보라색 'E', 반복성은 시안색 'R'로 마크를 다르게 표시합니다.
                    Rect eventRect = new Rect(rect.xMax - 12f, rect.y + 2f, 10f, 10f);
                    Color markColor = cell.isEventRepeatable ? Color.cyan : Color.magenta;
                    string markText = cell.isEventRepeatable ? "R" : "E";

                    EditorGUI.DrawRect(eventRect, markColor);
                    
                    GUIStyle evStyle = new GUIStyle(EditorStyles.miniLabel);
                    evStyle.alignment = TextAnchor.MiddleCenter;
                    evStyle.normal.textColor = cell.isEventRepeatable ? Color.black : Color.white; // 시안색 배경엔 검은 글씨
                    evStyle.fontSize = 8;
                    evStyle.padding = new RectOffset(0,0,0,0);
                    GUI.Label(eventRect, markText, evStyle);
                }

                // 벽면 오브젝트 시각화
                float dotSize = 6f;
                Color dotColor = new Color(1f, 0.9f, 0f); // 진노랑

                // 북쪽 (Top) - Index 0
                if (cell.faceObjectIDs[0] != -1) 
                    EditorGUI.DrawRect(new Rect(rect.center.x - dotSize/2, rect.y + 2, dotSize, dotSize), dotColor);
                // 동쪽 (Right) - Index 1
                if (cell.faceObjectIDs[1] != -1) 
                    EditorGUI.DrawRect(new Rect(rect.xMax - dotSize - 2, rect.center.y - dotSize/2, dotSize, dotSize), dotColor);
                // 남쪽 (Bottom) - Index 2
                if (cell.faceObjectIDs[2] != -1) 
                    EditorGUI.DrawRect(new Rect(rect.center.x - dotSize/2, rect.yMax - dotSize - 2, dotSize, dotSize), dotColor);
                // 서쪽 (Left) - Index 3
                if (cell.faceObjectIDs[3] != -1) 
                    EditorGUI.DrawRect(new Rect(rect.x + 2, rect.center.y - dotSize/2, dotSize, dotSize), dotColor);
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

            // 방향별 일괄 설정.
            DrawBatchWallField("↑ Tex (N)", 0);
            DrawBatchWallField("→ Tex (E)", 1);
            DrawBatchWallField("↓ Tex (S)", 2);
            DrawBatchWallField("← Tex (W)", 3);

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

            GUILayout.Space(10);
            GUILayout.Label("Batch Static Objects", EditorStyles.miniBoldLabel);
            DrawBatchCenterObjectField();

            // 다중 셀에 동일한 이벤트를 일괄 적용할 때 사용
            GUILayout.Space(5);
            GUILayout.Label("Batch Events", EditorStyles.miniBoldLabel);
            DrawBatchEventField();
            DrawBatchEventRepeatableField(); // 다중 셀 반복 여부 일괄 적용

            // 다중 셀 시점 강제 전환 일괄 적용 UI
            DrawBatchUseForceDirField();
            DrawBatchEvForceDirField();

            GUILayout.Space(5);
            GUILayout.Label("Batch Face Objects", EditorStyles.miniBoldLabel);
            
            // 4면 고정 오브젝트
            DrawBatchFaceObjectField("↑ Face Obj (N)", 0);
            DrawBatchFaceObjectField("→ Face Obj (E)", 1);
            DrawBatchFaceObjectField("↓ Face Obj (S)", 2);
            DrawBatchFaceObjectField("← Face Obj (W)", 3);

            GUILayout.Space(5);
            if (GUILayout.Button("Remove All Objects (-1)", EditorStyles.miniButton))
            {
                foreach (var c in selectedCells)
                {
                    c.centerObjectID = -1;
                    for (int i = 0; i < 4; i++) c.faceObjectIDs[i] = -1;
                }
                GUI.changed = true; GUI.FocusControl(null);
            }

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
                selectedCell.wallTextureIDs[0] = EditorGUILayout.IntField("↑ Tex (N)", selectedCell.wallTextureIDs[0]);
                selectedCell.wallTextureIDs[1] = EditorGUILayout.IntField("→ Tex (E)", selectedCell.wallTextureIDs[1]);
                selectedCell.wallTextureIDs[2] = EditorGUILayout.IntField("↓ Tex (S)", selectedCell.wallTextureIDs[2]);
                selectedCell.wallTextureIDs[3] = EditorGUILayout.IntField("← Tex (W)", selectedCell.wallTextureIDs[3]);

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

                // 고정 오브젝트 설정
                GUILayout.Space(15);
                GUILayout.Label("Static Object Settings", EditorStyles.boldLabel);

                // 중앙 오브젝트
                selectedCell.centerObjectID = EditorGUILayout.IntField("Center Obj ID", selectedCell.centerObjectID);

                // 이벤트 ID와 반복 여부 토글을 함께 표시
                GUILayout.Space(10);
                GUILayout.Label("Event Settings", EditorStyles.boldLabel);
                selectedCell.eventID = EditorGUILayout.TextField("Event ID", selectedCell.eventID);
                
                // 이벤트 ID가 있을 때만 반복 여부 토글을 보여주어 깔끔하게 유지
                if (!string.IsNullOrEmpty(selectedCell.eventID))
                {
                    selectedCell.isEventRepeatable = EditorGUILayout.Toggle("Is Repeatable", selectedCell.isEventRepeatable);
                    // 시점 강제 전환 UI
                    selectedCell.useForceDir = EditorGUILayout.Toggle("Use Force Dir", selectedCell.useForceDir);
                    if (selectedCell.useForceDir)
                    {
                        selectedCell.evForceDir = (Direction)EditorGUILayout.EnumPopup("Force Direction", selectedCell.evForceDir);
                    }
                }

                GUILayout.Space(5);
                GUILayout.Label("Face Objects (Wall Decor)", EditorStyles.miniBoldLabel);

                // 4방향 면 오브젝트 입력 필드
                selectedCell.faceObjectIDs[0] = EditorGUILayout.IntField("↑ Face Obj (N)", selectedCell.faceObjectIDs[0]);
                selectedCell.faceObjectIDs[1] = EditorGUILayout.IntField("→ Face Obj (E)", selectedCell.faceObjectIDs[1]);
                selectedCell.faceObjectIDs[2] = EditorGUILayout.IntField("↓ Face Obj (S)", selectedCell.faceObjectIDs[2]);
                selectedCell.faceObjectIDs[3] = EditorGUILayout.IntField("← Face Obj (W)", selectedCell.faceObjectIDs[3]);

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
                    
                    // 계단 연출 타입 입력란
                    existingEntrance.stairType = (StairType)EditorGUILayout.EnumPopup("Stair Transition", existingEntrance.stairType);
                    
                    GUILayout.Space(5);
                    GUILayout.Label("Target Destination", EditorStyles.miniBoldLabel);
                    
                    existingEntrance.isWorldMap = EditorGUILayout.Toggle("Is World Map", existingEntrance.isWorldMap);
                    
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
                            isWorldMap = false,
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

    // 이벤트 일괄 적용 헬퍼 함수
    void DrawBatchEventField()
    {
        string firstVal = selectedCells[0].eventID;
        bool isMixed = false;
        foreach (var c in selectedCells)
            if (c.eventID != firstVal) { isMixed = true; break; }

        EditorGUI.showMixedValue = isMixed;
        EditorGUI.BeginChangeCheck();
        string newVal = EditorGUILayout.TextField("Event ID", isMixed ? "" : firstVal);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var c in selectedCells) c.eventID = newVal;
            GUI.changed = true;
        }
        EditorGUI.showMixedValue = false;
    }

    // 이벤트 반복 여부 플래그 일괄 적용 헬퍼 함수
    void DrawBatchEventRepeatableField()
    {
        bool firstVal = selectedCells[0].isEventRepeatable;
        bool isMixed = false;
        foreach (var c in selectedCells)
            if (c.isEventRepeatable != firstVal) { isMixed = true; break; }

        EditorGUI.showMixedValue = isMixed;
        EditorGUI.BeginChangeCheck();
        bool newVal = EditorGUILayout.Toggle("Is Repeatable", isMixed ? false : firstVal);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var c in selectedCells) c.isEventRepeatable = newVal;
            GUI.changed = true;
        }
        EditorGUI.showMixedValue = false;
    }

    // 방향 강제 여부 일괄 적용 헬퍼
    void DrawBatchUseForceDirField()
    {
        bool firstVal = selectedCells[0].useForceDir;
        bool isMixed = false;
        foreach (var c in selectedCells)
            if (c.useForceDir != firstVal) { isMixed = true; break; }

        EditorGUI.showMixedValue = isMixed;
        EditorGUI.BeginChangeCheck();
        bool newVal = EditorGUILayout.Toggle("Use Force Dir", isMixed ? false : firstVal);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var c in selectedCells) c.useForceDir = newVal;
            GUI.changed = true;
        }
        EditorGUI.showMixedValue = false;
    }

    // 강제할 방향 일괄 적용 헬퍼
    void DrawBatchEvForceDirField()
    {
        Direction firstVal = selectedCells[0].evForceDir;
        bool isMixed = false;
        foreach (var c in selectedCells)
            if (c.evForceDir != firstVal) { isMixed = true; break; }

        EditorGUI.showMixedValue = isMixed;
        EditorGUI.BeginChangeCheck();
        Direction newVal = (Direction)EditorGUILayout.EnumPopup("Force Direction", isMixed ? Direction.North : firstVal);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var c in selectedCells) c.evForceDir = newVal;
            GUI.changed = true;
        }
        EditorGUI.showMixedValue = false;
    }

    // 중앙 고정 오브젝트 일괄 적용 헬퍼
    void DrawBatchCenterObjectField()
    {
        int firstVal = selectedCells[0].centerObjectID;
        bool isMixed = false;
        foreach (var c in selectedCells)
            if (c.centerObjectID != firstVal) { isMixed = true; break; }

        EditorGUI.showMixedValue = isMixed;
        EditorGUI.BeginChangeCheck();
        int newVal = EditorGUILayout.IntField("Center Obj ID", isMixed ? -1 : firstVal);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var c in selectedCells) c.centerObjectID = newVal;
            GUI.changed = true;
        }
        EditorGUI.showMixedValue = false;
    }

    // 벽면 고정 오브젝트 4방향 일괄 적용 헬퍼
    void DrawBatchFaceObjectField(string label, int faceIdx)
    {
        int firstVal = selectedCells[0].faceObjectIDs[faceIdx];
        bool isMixed = false;
        foreach (var c in selectedCells)
            if (c.faceObjectIDs[faceIdx] != firstVal) { isMixed = true; break; }

        EditorGUI.showMixedValue = isMixed;
        EditorGUI.BeginChangeCheck();
        int newVal = EditorGUILayout.IntField(label, isMixed ? -1 : firstVal);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var c in selectedCells) c.faceObjectIDs[faceIdx] = newVal;
            GUI.changed = true;
        }
        EditorGUI.showMixedValue = false;
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

        mapData.mapID = inputMapID;
        mapData.locationID = inputLocationID;
        mapData.themeName = (inputTheme != null) ? inputTheme.name : "";
        mapData.hasCeil = inputHasCeil;
        
        mapData.startX = inputStartX;
        mapData.startY = inputStartY;
        mapData.startDirection = inputStartDirection;
    }
}