using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Data;
using UI;
using UnityEditor;
using UnityEngine;

public partial class DungeonMapEditor : EditorWindow
{
    [SerializeField] private DungeonMapEditorDocument document;
    [SerializeField] private string currentFilePath = "";
    [SerializeField] private string savedJson = "";
    [SerializeField] private string diskJson = "";
    [SerializeField] private List<Vector2Int> selection = new List<Vector2Int>();
    [SerializeField] private Vector2 gridScroll, inspectorScroll, paletteScroll;
    [SerializeField] private float cellSize = 46;
    [SerializeField] private int inspectorTab;
    [SerializeField] private bool showAdvanced;
    [SerializeField] private string status = "새 맵을 만들거나 프로젝트 맵을 열어주세요.";
    private DungeonMapEditorIndex index;
    private List<DungeonMapIssue> issues = new List<DungeonMapIssue>();
    private double recoveryAt;
    private bool needsValidation;
    private const string RecoveryPath = "Library/DungeonMapEditor/recovery.json";
    private MapData Map => document != null ? document.map : null;
    private DungeonTheme Theme => index.Theme(Map.themeID);
    private IEnumerable<CellData> Selected => selection.Select(p => Map.GetCell(p.x, p.y)).Where(c => c != null);
    private CellData Active => selection.Count == 0 ? null : Map.GetCell(selection[selection.Count - 1].x, selection[selection.Count - 1].y);

    [MenuItem("Tools/Dungeon Map Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<DungeonMapEditor>();
        window.titleContent = new GUIContent("던전 맵 편집기");
        window.minSize = new Vector2(1000, 600);
    }

    private void OnEnable()
    {
        minSize = new Vector2(1000, 600);
        index = new DungeonMapEditorIndex();
        index.Refresh();
        selection = selection ?? new List<Vector2Int>();
        if (document == null || document.map == null)
        {
            document = CreateInstance<DungeonMapEditorDocument>();
            document.hideFlags = HideFlags.HideAndDontSave;
            document.map = DungeonMapEditing.Create(10, 10, "NewMap", "", "", true);
            savedJson = JsonUtility.ToJson(document.map);
            currentFilePath = "";
        }
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.update += Tick;
        EditorApplication.projectChanged += ProjectChanged;
        RefreshState();
    }
    private void OnDisable()
    {
        FinishGesture();
        WriteRecovery();
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.update -= Tick;
        EditorApplication.projectChanged -= ProjectChanged;
    }
    private void OnDestroy()
    {
        if (document != null) { Undo.ClearUndo(document); DestroyImmediate(document); }
    }
    private void ProjectChanged() { needsValidation = true; }
    private void Tick()
    {
        if (EditorApplication.timeSinceStartup < recoveryAt) return;
        recoveryAt = EditorApplication.timeSinceStartup + 10;
        WriteRecovery();
    }
    private void WriteRecovery()
    {
        if (!hasUnsavedChanges || Map == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RecoveryPath));
            DungeonMapFileIO.WriteAtomic(RecoveryPath, JsonUtility.ToJson(Map, true));
        }
        catch (Exception ex) { status = "복구본 저장 실패: " + ex.Message; }
    }
    private static void DeleteRecovery()
    {
        try
        {
            if (File.Exists(RecoveryPath)) File.Delete(RecoveryPath);
            if (File.Exists(RecoveryPath + ".bak")) File.Delete(RecoveryPath + ".bak");
        }
        catch (IOException) { /* 문서 저장 성공 여부와 복구본 정리는 별개입니다. */ }
        catch (UnauthorizedAccessException) { /* 읽기 전용인 복구본이 문서 저장을 막지 않게 합니다. */ }
    }
    public override void SaveChanges()
    {
        // 저장 취소/실패 시 hasUnsavedChanges를 유지하여 Unity가 창을 닫지 않게 합니다.
        if (SaveMap(false)) base.SaveChanges();
    }
    public override void DiscardChanges()
    {
        DeleteRecovery();
        base.DiscardChanges();
    }
    private bool ConfirmDocumentChange()
    {
        FinishGesture();
        if (!hasUnsavedChanges) return true;
        int choice = EditorUtility.DisplayDialogComplex("변경 사항 저장", "현재 맵의 변경 사항을 저장하시겠습니까?", "저장", "취소", "저장하지 않음");
        if (choice == 1) return false;
        if (choice == 0) return SaveMap(false);
        return true;
    }
    private void ReplaceDocument(MapData map, string path, string originalJson, bool isSaved)
    {
        FinishGesture();
        Undo.ClearUndo(document);
        document.map = map;
        currentFilePath = path ?? "";
        diskJson = originalJson ?? "";
        savedJson = isSaved ? JsonUtility.ToJson(map) : "";
        selection.Clear();
        gridScroll = Vector2.zero;
        DeleteRecovery();
        RefreshState();
    }
    private void Edit(string label, Action<MapData> edit)
    {
        document.Edit(label, edit);
        RefreshState();
    }
    private void EditSelected(string label, Action<CellData> edit)
    {
        var coordinates = selection.ToArray();
        Edit(label, map => { foreach (var p in coordinates) { var c = map.GetCell(p.x, p.y); if (c != null) edit(c); } });
    }
    private void RefreshState()
    {
        if (Map == null) return;
        selection.RemoveAll(p => Map.GetCell(p.x, p.y) == null);
        hasUnsavedChanges = JsonUtility.ToJson(Map) != savedJson;
        saveChangesMessage = $"'{Map.mapID}'의 변경 사항을 저장하시겠습니까?";
        titleContent = new GUIContent("던전 맵 — " + Map.mapID);
        needsValidation = true;
        UpdateVisualizer();
        Repaint();
    }
    private void OnUndoRedo() { CancelGesture(); RefreshState(); }
    private void ValidateMap(bool reloadIndex = false)
    {
        if (reloadIndex) index.Refresh();
        issues = DungeonMapValidation.Check(Map, index, currentFilePath);
        needsValidation = false;
    }
    private void UpdateVisualizer()
    {
        if (EditorApplication.isPlaying) return;
        var visualizer = FindFirstObjectByType<EditorGridVisualizer>();
        if (visualizer == null) return;
        visualizer.mapData = Map;
        visualizer.selectedCoords = new List<Vector2Int>(selection);
        SceneView.RepaintAll();
    }
    private void Later(Action action)
    {
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception ex) { status = "작업 실패: " + ex.Message; Debug.LogException(ex); }
            Repaint();
        };
    }
    private void OnGUI()
    {
        if (document == null || index == null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) FinishGesture();
        
        if (Event.current.type == EventType.Layout)
        {
            if (needsValidation && !gestureActive) ValidateMap();
            CaptureIssueLayout();
        }
        HandleShortcuts();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(208))) DrawPalette();
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true))) DrawGrid();
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(330)))
                {
                    inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
                    DrawInspector();
                    EditorGUILayout.EndScrollView();
                }
            }
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("맵 편집은 Play 모드 종료 후 사용할 수 있습니다.", MessageType.Info);
        EditorGUILayout.LabelField(status, EditorStyles.helpBox, GUILayout.Height(36));
    }
    private void HandleShortcuts()
    {
        var e = Event.current;
        if (EditorApplication.isPlayingOrWillChangePlaymode || e.type != EventType.KeyDown) return;
        bool command = e.control || e.command;
        if (command && e.keyCode == KeyCode.S) { bool saveAs = e.shift; Later(() => SaveMap(saveAs)); e.Use(); }
        if (command && !EditorGUIUtility.editingTextField && e.keyCode == KeyCode.Z)
        { FinishGesture(); if (e.shift) Undo.PerformRedo(); else Undo.PerformUndo(); e.Use(); }
        if (command && !EditorGUIUtility.editingTextField && e.keyCode == KeyCode.Y)
        { FinishGesture(); Undo.PerformRedo(); e.Use(); }
        if (command && !EditorGUIUtility.editingTextField && e.keyCode == KeyCode.A)
        { selection = Map.cells.Select(c => new Vector2Int(c.x, c.y)).ToList(); UpdateVisualizer(); e.Use(); }
        if (e.keyCode == KeyCode.Escape && !EditorGUIUtility.editingTextField)
        { FinishGesture(); selection.Clear(); UpdateVisualizer(); e.Use(); }
    }
    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("새 맵", EditorStyles.toolbarButton)) Later(OpenNewMap);
            if (GUILayout.Button("맵 목록", EditorStyles.toolbarButton)) OpenMapPicker();
            if (GUILayout.Button("JSON 열기", EditorStyles.toolbarButton)) Later(() =>
            { string p = EditorUtility.OpenFilePanel("맵 JSON 열기", "Assets/Database/Dungeons/Levels", "json"); if (!string.IsNullOrEmpty(p)) LoadMap(p); });
            if (GUILayout.Button("저장", EditorStyles.toolbarButton)) Later(() => SaveMap(false));
            if (GUILayout.Button("다른 이름으로 저장", EditorStyles.toolbarButton)) Later(() => SaveMap(true));
            GUILayout.Space(10);
            if (GUILayout.Button("실행 취소", EditorStyles.toolbarButton)) { FinishGesture(); Undo.PerformUndo(); }
            if (GUILayout.Button("다시 실행", EditorStyles.toolbarButton)) { FinishGesture(); Undo.PerformRedo(); }
            if (GUILayout.Button("검사 / 목록 갱신", EditorStyles.toolbarButton)) Later(() => { ValidateMap(true); status = $"검사 완료: 오류 {issues.Count(i => i.severity == MessageType.Error)}개"; });
            if (GUILayout.Button("1인칭 미리보기", EditorStyles.toolbarButton)) Later(OpenPreview);
            GUILayout.FlexibleSpace();
            GUILayout.Label(hasUnsavedChanges ? "● 미저장" : "저장됨", EditorStyles.miniLabel);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(string.IsNullOrEmpty(currentFilePath) ? "새 문서 · 첫 저장 시 파일을 지정합니다." : currentFilePath, EditorStyles.miniLabel);
            if (File.Exists(RecoveryPath) && GUILayout.Button("복구본 열기", GUILayout.Width(100))) Later(() => LoadMap(RecoveryPath, true));
        }
    }
    private void OpenMapPicker()
    {
        var entries = index.maps.ToArray();
        DungeonMapChoiceWindow.Show("맵 열기", entries.Select(m => m.Label).ToArray(), i => Later(() => LoadMap(entries[i].path)));
    }
    private void LoadMap(string path, bool recovery = false)
    {
        // 파일 읽기/구조 검사가 끝난 뒤에만 현재 문서의 저장 여부를 물어봅니다.
        string json = File.ReadAllText(path);
        if (!DungeonMapEditing.TryRead(json, out var candidate, out var error)) { status = error; return; }
        if (!ConfirmDocumentChange()) return;
        string assetPath = recovery ? null : DungeonMapFileIO.AssetPath(path);
        ReplaceDocument(candidate, assetPath, json, !string.IsNullOrEmpty(assetPath));
        status = recovery ? "복구본을 열었습니다. 다른 이름으로 저장하세요." : "맵을 열었습니다.";
    }
    private void OpenNewMap()
    {
        DungeonMapCreateWindow.Show(index.themes.ToArray(), (w, h, id, location, theme, ceil) =>
        {
            if (this == null) return false;
            if (index.HasIdentityConflict(id, null)) { status = "이미 사용 중인 맵 ID입니다."; return false; }
            if (!ConfirmDocumentChange()) return false;
            ReplaceDocument(DungeonMapEditing.Create(w, h, id, location, theme != null ? theme.themeID : "", ceil), "", "", false);
            status = "새 맵을 만들었습니다. 왼쪽 팔레트에서 재료를 선택하세요.";
            return true;
        });
    }
    private bool SaveMap(bool saveAs)
    {
        try { return SaveMapCore(saveAs); }
        catch (Exception ex) { status = "저장 실패 — 현재 문서는 유지됩니다: " + ex.Message; return false; }
    }
    private bool SaveMapCore(bool saveAs)
    {
        FinishGesture();
        if (!DungeonMapEditing.CheckStructure(Map, out var error)) { status = error; return false; }
        var candidate = DungeonMapEditing.Clone(Map);
        string path = currentFilePath;
        if (saveAs || string.IsNullOrEmpty(path) || Path.GetFileNameWithoutExtension(path) != candidate.mapID)
        {
            string id = DungeonMapFileIO.ValidID(candidate.mapID) ? candidate.mapID : "NewMap";
            path = EditorUtility.SaveFilePanelInProject("맵 저장", id, "json", "파일명이 맵 ID가 됩니다.", "Assets/Database/Dungeons/Levels");
            if (string.IsNullOrEmpty(path)) return false;
            string oldID = candidate.mapID;
            candidate.mapID = Path.GetFileNameWithoutExtension(path);
            if (oldID != candidate.mapID)
                foreach (var e in candidate.entrances)
                    if (e.type == EntranceType.Map && !e.isWorldMap && e.destinationID == oldID) e.destinationID = candidate.mapID;
        }
        if (!DungeonMapFileIO.ValidID(candidate.mapID)) { status = "파일명으로 사용할 수 없는 맵 ID입니다."; return false; }
        index.Refresh();
        string assetPath = DungeonMapFileIO.AssetPath(path);
        if (index.HasIdentityConflict(candidate.mapID, assetPath))
        { status = "다른 맵과 ID가 중복됩니다. 복사본은 새로운 파일명을 사용하세요."; return false; }
        if (path == currentFilePath && File.Exists(path) && File.ReadAllText(path) != diskJson)
        { status = "디스크의 파일이 외부에서 변경되었습니다. 다른 이름으로 저장하거나 다시 열어 비교하세요."; return false; }
        try
        {
            string json = JsonUtility.ToJson(candidate, true);
            DungeonMapFileIO.WriteAtomic(path, json);
            if (JsonUtility.ToJson(Map) != JsonUtility.ToJson(candidate)) document.Edit("저장할 맵 ID 변경", _ => document.map = candidate);
            currentFilePath = assetPath;
            diskJson = json;
            savedJson = JsonUtility.ToJson(Map);
            DeleteRecovery();
            RefreshState();
            status = "맵 저장 완료: " + currentFilePath;
            try
            {
                AssetDatabase.ImportAsset(currentFilePath, ImportAssetOptions.ForceUpdate);
                index.Refresh();
                ValidateMap();
                if (!issues.Any(i => i.severity == MessageType.Error))
                {
                    DungeonMapEditorIndex.Register(currentFilePath, Theme);
                    status += " · 카탈로그 등록 완료";
                }
                else status += " · 초안으로 저장됨 (검사 오류를 수정한 뒤 게임 등록)";
            }
            catch (Exception ex) { status = "JSON 저장 완료 / 게임 등록 실패: " + ex.Message; }
            return true;
        }
        catch (Exception ex) { status = "저장 실패 — 변경 사항은 유지됩니다: " + ex.Message; return false; }
    }
    private void RegisterMap()
    {
        if (hasUnsavedChanges || string.IsNullOrEmpty(currentFilePath))
        { status = "먼저 맵을 저장하세요."; return; }
        ValidateMap(true);
        if (issues.Any(i => i.severity == MessageType.Error)) { status = "검사 오류를 수정한 뒤 등록하세요."; return; }
        var catalog = DungeonMapEditorIndex.Register(currentFilePath, Theme);
        Selection.activeObject = catalog;
        status = "카탈로그 등록 완료. 아래 매니저 프리팹 연결 상태를 확인하세요.";
    }
    private void ResizeMap(int width, int height, int offsetX, int offsetY)
    {
        if (!DungeonMapEditing.ValidSize(width, height)) { status = "맵 크기는 1~256입니다."; return; }
        int removed = DungeonMapEditing.CountRemovedEntrances(Map, width, height, offsetX, offsetY);
        int keptW = Mathf.Max(0, Mathf.Min(Map.width, width - offsetX) - Mathf.Max(0, -offsetX));
        int keptH = Mathf.Max(0, Mathf.Min(Map.height, height - offsetY) - Mathf.Max(0, -offsetY));
        int removedCells = Map.cells.Length - keptW * keptH;
        if (removedCells > 0 && !EditorUtility.DisplayDialog("맵 크기 변경",
            $"타일 {removedCells}개와 입구 {removed}개가 삭제됩니다. 시작점이 잘리면 맵 안으로 이동합니다.\n실행 취소로 복원할 수 있습니다.", "변경", "취소")) return;
        if (offsetX != 0 || offsetY != 0)
        {
            var inbound = index.maps.Where(m => m.path != currentFilePath && m.map.entrances.Any(e =>
                e.type == EntranceType.Map && !e.isWorldMap && e.destinationID == Map.mapID && (e.targetX >= 0 || e.targetY >= 0))).ToArray();
            if (inbound.Length > 0 && !EditorUtility.DisplayDialog("다른 맵의 도착 좌표 확인",
                "좌표 이동 후 아래 맵의 입구 도착점을 다시 확인해야 합니다:\n" + string.Join("\n", inbound.Select(m => m.Key)), "계속", "취소")) return;
        }
        Edit("맵 크기 변경", _ => document.map = DungeonMapEditing.Resize(Map, width, height, offsetX, offsetY));
        selection.Clear(); RefreshState();
    }
    private void GenerateMaze()
    {
        if (Map.width < 3 || Map.height < 3 || Map.width > 255 || Map.height > 255 || !DungeonMapValidation.TextureValid(Theme, 0))
        { status = "미로는 크기 3~255, 기본 벽 텍스처 0이 있는 테마가 필요합니다."; return; }
        if (!EditorUtility.DisplayDialog("무작위 미로", "타일·입구·이벤트를 새 미로로 바꿉니다. 짝수 크기는 다음 홀수가 됩니다. 실행 취소로 복원할 수 있습니다.", "생성", "취소")) return;
        var maze = Generator.DungeonGenerator.GenerateRandomMaze(Map.width, Map.height, Map.mapID, Map.themeID);
        maze.locationID = Map.locationID; maze.hasCeil = Map.hasCeil;
        Edit("무작위 미로 생성", _ => document.map = maze);
        selection.Clear(); RefreshState();
    }
    private void OpenPreview()
    {
        ValidateMap();
        if (Theme == null) { status = "테마를 먼저 선택하세요."; return; }
        var p = Active != null ? new Vector2Int(Active.x, Active.y) : new Vector2Int(Map.startX, Map.startY);
        DungeonMapPreviewWindow.Open(Map, Theme, p);
    }
}
