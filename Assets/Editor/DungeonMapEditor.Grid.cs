using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEditor;
using UnityEngine;

public partial class DungeonMapEditor
{
    private enum MapTool { Select, Brush, Rectangle, Fill, Eyedropper, Start }
    [SerializeField] private MapTool tool;
    [SerializeField] private DungeonPaintLayer paintLayer;
    [SerializeField] private int paintValue = -1, paintDirection;
    [SerializeField] private string paletteSearch = "";
    [SerializeField] private bool showImages = true;
    private bool gestureActive, gesturePaint, gestureMoved, panGesture;
    private int gestureGroup = -1, gridControl;
    private Vector2Int dragOrigin, dragLast;
    private List<Vector2Int> selectionBeforeDrag;
    private HashSet<Vector2Int> painted = new HashSet<Vector2Int>();
    private Rect gridViewport;
    private static readonly string[] ToolNames = { "선택", "브러시", "사각형", "채우기", "스포이트", "시작점" };
    private static readonly string[] LayerNames = { "바닥", "천장", "벽", "중앙 오브젝트", "벽면 오브젝트", "타일 종류" };
    internal static readonly string[] DirectionNames = { "북 ↑", "동 →", "남 ↓", "서 ←" };

    private void DrawPalette()
    {
        GUILayout.Label("편집 도구", EditorStyles.boldLabel);
        var nextTool = (MapTool)GUILayout.SelectionGrid((int)tool, ToolNames, 2);
        if (nextTool != tool) { FinishGesture(); tool = nextTool; }
        EditorGUILayout.HelpBox("선택: 클릭 / Ctrl 추가 선택 / 드래그 영역 선택\n브러시·사각형·채우기: 선택한 재료 적용\n바닥·천장 채우기는 벽 경계에서 멈춥니다.", MessageType.None);
        int nextLayer = EditorGUILayout.Popup("편집 대상", (int)paintLayer, LayerNames);
        if (nextLayer != (int)paintLayer) { paintLayer = (DungeonPaintLayer)nextLayer; paintValue = paintLayer == DungeonPaintLayer.CellType ? 0 : -1; }
        if (paintLayer == DungeonPaintLayer.Wall || paintLayer == DungeonPaintLayer.FaceObject)
            paintDirection = GUILayout.Toolbar(paintDirection, DirectionNames);
        paletteSearch = EditorGUILayout.TextField(new GUIContent("검색", "이름 또는 번호로 찾습니다."), paletteSearch);
        paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll);
        if (paintLayer == DungeonPaintLayer.CellType)
        {
            foreach (CellType type in Enum.GetValues(typeof(CellType)))
                PaletteButton((int)type, CellTypeName((int)type), null, false);
        }
        else
        {
            string empty = paintLayer == DungeonPaintLayer.Floor || paintLayer == DungeonPaintLayer.Ceiling ? "테마 기본값" : "없음";
            PaletteButton(-1, empty, null, false);
            var theme = Theme;
            if (theme == null) EditorGUILayout.HelpBox("맵 설정에서 테마를 선택하세요.", MessageType.Info);
            else
            {
                bool objects = paintLayer == DungeonPaintLayer.CenterObject || paintLayer == DungeonPaintLayer.FaceObject;
                var choices = new List<(int id, string label, Texture2D image)>();
                if (objects && theme.objectSprites != null)
                    foreach (var item in theme.objectSprites)
                        choices.Add((item.objectID, item.texture != null ? item.texture.name : "이미지 없음", item.texture));
                else if (!objects && theme.texture != null)
                    for (int i = 0; i < theme.texture.Length; i++)
                        choices.Add((i, theme.texture[i] != null ? theme.texture[i].name : "이미지 없음", theme.texture[i]));
                choices = choices.Where(c => string.IsNullOrEmpty(paletteSearch) ||
                    (c.id + " " + c.label).IndexOf(paletteSearch, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                for (int row = 0; row < choices.Count; row += 2)
                    using (new EditorGUILayout.HorizontalScope())
                        for (int col = row; col < Math.Min(row + 2, choices.Count); col++)
                        { var choice = choices[col]; PaletteButton(choice.id, choice.label, choice.image, true); }
            }
        }
        EditorGUILayout.EndScrollView();
        GUILayout.Label($"선택 재료: {PaintLabel()}", EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUI.DisabledScope(selection.Count == 0 || !CanPaint()))
            if (GUILayout.Button($"선택한 {selection.Count}칸에 적용"))
                EditSelected("선택 영역에 재료 적용", c => DungeonMapEditing.Paint(c, paintLayer, paintDirection, paintValue));
    }
    private void PaletteButton(int value, string label, Texture2D image, bool tile)
    {
        Color previous = GUI.backgroundColor;
        if (paintValue == value) GUI.backgroundColor = new Color(0.35f, 0.85f, 1f);
        var style = new GUIStyle(GUI.skin.button) { imagePosition = image != null ? ImagePosition.ImageAbove : ImagePosition.TextOnly,
            wordWrap = true, fontSize = 10 };
        string text = showAdvanced ? $"{label} [{value}]" : label;
        bool clicked = tile
            ? GUILayout.Button(new GUIContent(text, image, $"{label} (ID {value})"), style, GUILayout.Width(92), GUILayout.Height(84))
            : GUILayout.Button(new GUIContent(text, $"{label} (ID {value})"), style, GUILayout.Height(28));
        if (clicked) paintValue = value;
        GUI.backgroundColor = previous;
    }
    private bool CanPaint()
    {
        if (paintLayer == DungeonPaintLayer.CellType) return Enum.IsDefined(typeof(CellType), paintValue);
        if (paintLayer == DungeonPaintLayer.CenterObject || paintLayer == DungeonPaintLayer.FaceObject)
            return DungeonMapValidation.ObjectValid(Theme, paintValue);
        return paintValue == -1 || DungeonMapValidation.TextureValid(Theme, paintValue);
    }
    private string PaintLabel()
    {
        if (paintLayer == DungeonPaintLayer.CellType) return CellTypeName(paintValue);
        if (paintValue == -1) return paintLayer == DungeonPaintLayer.Floor || paintLayer == DungeonPaintLayer.Ceiling ? "테마 기본값" : "없음";
        return TextureOrObjectLabel(paintValue, paintLayer == DungeonPaintLayer.CenterObject || paintLayer == DungeonPaintLayer.FaceObject);
    }
    private void DrawGrid()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label($"{Map.width} × {Map.height} · 선택 {selection.Count}칸", EditorStyles.miniLabel);
            showImages = GUILayout.Toggle(showImages, "이미지", GUILayout.Width(60));
            cellSize = GUILayout.HorizontalSlider(cellSize, 20, 96, GUILayout.Width(100));
            if (GUILayout.Button("전체 보기", GUILayout.Width(72)))
            { cellSize = Mathf.Clamp(Mathf.Min((gridViewport.width - 18) / Map.width, (gridViewport.height - 18) / Map.height), 8, 96); gridScroll = Vector2.zero; }
        }
        gridViewport = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        var e = Event.current;
        bool inView = gridViewport.Contains(e.mousePosition);
        if (inView && e.type == EventType.ScrollWheel && (e.control || e.command))
        {
            float oldSize = cellSize;
            cellSize = Mathf.Clamp(cellSize * Mathf.Pow(1.1f, -e.delta.y), 8, 96);
            Vector2 offset = e.mousePosition - gridViewport.position;
            gridScroll = (gridScroll + offset) * (cellSize / oldSize) - offset;
            e.Use();
        }
        gridControl = GUIUtility.GetControlID(FocusType.Passive);
        if (inView && e.type == EventType.MouseDown && e.button == 2)
        { panGesture = true; GUIUtility.hotControl = gridControl; e.Use(); }
        if (panGesture && e.type == EventType.MouseDrag)
        { gridScroll -= e.delta; e.Use(); Repaint(); }
        if (panGesture && e.type == EventType.MouseUp)
        { panGesture = false; GUIUtility.hotControl = 0; e.Use(); }
        Rect content = new Rect(0, 0, Map.width * cellSize, Map.height * cellSize);
        gridScroll = GUI.BeginScrollView(gridViewport, gridScroll, content);
        DrawVisibleCells();
        HandleGridInput(inView, content);
        GUI.EndScrollView();
        GUILayout.Label("빨강 벽 · 초록 문 · 청록 점선 통과 벽 · E 이벤트 · P 입구 · 노란 화살표 시작점\n휠 스크롤 / Ctrl+휠 확대 / 가운데 버튼 이동", EditorStyles.wordWrappedMiniLabel);
    }
    private void DrawVisibleCells()
    {
        if (Event.current.type != EventType.Repaint) return;
        var selected = new HashSet<Vector2Int>(selection);
        var portals = new HashSet<Vector2Int>(Map.entrances.Select(p => new Vector2Int(p.sourceX, p.sourceY)));
        int minX = Mathf.Clamp(Mathf.FloorToInt(gridScroll.x / cellSize), 0, Map.width - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt((gridScroll.x + gridViewport.width) / cellSize), 0, Map.width - 1);
        int minRow = Mathf.Clamp(Mathf.FloorToInt(gridScroll.y / cellSize), 0, Map.height - 1);
        int maxRow = Mathf.Clamp(Mathf.CeilToInt((gridScroll.y + gridViewport.height) / cellSize), 0, Map.height - 1);
        var theme = Theme;
        var small = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerCenter, fontSize = 9 };
        var marker = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter, fontSize = 11 };
        for (int row = minRow; row <= maxRow; row++)
            for (int x = minX; x <= maxX; x++)
            {
                int y = Map.height - 1 - row;
                var c = Map.GetCell(x, y);
                Rect r = new Rect(x * cellSize, row * cellSize, cellSize - 1, cellSize - 1);
                EditorGUI.DrawRect(r, c.value == -1 ? new Color(0.06f, 0.06f, 0.08f) : new Color(0.22f, 0.24f, 0.27f));
                int texture = c.floorTexIdx >= 0 ? c.floorTexIdx : theme != null ? theme.floorTexIdx : -1;
                if (showImages && c.value != -1 && DungeonMapValidation.TextureValid(theme, texture))
                    GUI.DrawTexture(r, theme.texture[texture], ScaleMode.ScaleToFit);
                if (selected.Contains(new Vector2Int(x, y))) EditorGUI.DrawRect(r, new Color(0.1f, 0.65f, 1f, 0.35f));
                for (int d = 0; d < 4; d++)
                {
                    int id = c.wallTextureIDs[d];
                    if (id < 0) continue;
                    bool door = theme != null && theme.doorAnimations != null && theme.doorAnimations.Any(a => a != null && a.closedTexId == id);
                    bool pass = theme != null && theme.passableWallTexIDs != null && theme.passableWallTexIDs.Contains(id);
                    Color color = door ? Color.green : pass ? Color.cyan : new Color(1, 0.28f, 0.23f);
                    Rect edge = d == 0 ? new Rect(r.x, r.y, r.width, 3) : d == 1 ? new Rect(r.xMax - 3, r.y, 3, r.height) :
                                d == 2 ? new Rect(r.x, r.yMax - 3, r.width, 3) : new Rect(r.x, r.y, 3, r.height);
                    if (!pass || door) EditorGUI.DrawRect(edge, color);
                    else for (int segment = 0; segment < 3; segment++)
                    {
                        var dash = edge;
                        if (d % 2 == 0) { dash.x += segment * edge.width / 3; dash.width = edge.width / 6; }
                        else { dash.y += segment * edge.height / 3; dash.height = edge.height / 6; }
                        EditorGUI.DrawRect(dash, color);
                    }
                }
                if (cellSize >= 28)
                {
                    string text = (portals.Contains(new Vector2Int(x, y)) ? "P " : "") + (c.events.Count > 0 ? "E " : "") +
                                  (c.centerObjectID >= 0 || c.faceObjectIDs.Any(v => v >= 0) ? "◆" : "");
                    GUI.Label(r, text, marker);
                    GUI.Label(r, $"{x},{y}", small);
                }
                if (x == Map.startX && y == Map.startY)
                {
                    var arrowStyle = new GUIStyle(marker) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Mathf.Clamp(cellSize * 0.55f, 12, 34)) };
                    arrowStyle.normal.textColor = Color.yellow;
                    GUI.Label(r, new[] { "↑", "→", "↓", "←" }[Mathf.Clamp((int)Map.startDirection, 0, 3)], arrowStyle);
                }
            }
        if (gestureActive && gestureMoved && (tool == MapTool.Rectangle || tool == MapTool.Select))
        {
            int left = Math.Min(dragOrigin.x, dragLast.x), top = Map.height - 1 - Math.Max(dragOrigin.y, dragLast.y);
            EditorGUI.DrawRect(new Rect(left * cellSize, top * cellSize, (Math.Abs(dragOrigin.x - dragLast.x) + 1) * cellSize,
                (Math.Abs(dragOrigin.y - dragLast.y) + 1) * cellSize), new Color(0.3f, 0.75f, 1, 0.2f));
        }
    }
    private void HandleGridInput(bool inView, Rect content)
    {
        if (!GUI.enabled) return;
        var e = Event.current;
        var p = new Vector2Int(Mathf.FloorToInt(e.mousePosition.x / cellSize), Map.height - 1 - Mathf.FloorToInt(e.mousePosition.y / cellSize));
        bool valid = inView && content.Contains(e.mousePosition) && Map.GetCell(p.x, p.y) != null;
        if (valid && e.type == EventType.MouseDown && e.button == 0)
        {
            GUI.FocusControl(null);
            if (tool == MapTool.Eyedropper)
            { paintValue = DungeonMapEditing.ReadPaint(Map.GetCell(p.x, p.y), paintLayer, paintDirection); tool = MapTool.Brush; e.Use(); return; }
            if (tool == MapTool.Start)
            { Edit("시작점 배치", map => { map.startX = p.x; map.startY = p.y; }); e.Use(); return; }
            if (tool != MapTool.Select && !CanPaint()) { status = "현재 테마에 있는 재료를 선택하세요."; e.Use(); return; }
            if (tool == MapTool.Fill)
            {
                var cells = DungeonMapEditing.FloodRegion(Map, p, paintLayer, paintDirection);
                Edit("연결 영역 채우기", map => { foreach (var coord in cells) DungeonMapEditing.Paint(map.GetCell(coord.x, coord.y), paintLayer, paintDirection, paintValue); });
                e.Use(); return;
            }
            gestureActive = true; gestureMoved = false; gesturePaint = false;
            dragOrigin = dragLast = p;
            painted.Clear();
            selectionBeforeDrag = e.control || e.command ? new List<Vector2Int>(selection) : new List<Vector2Int>();
            GUIUtility.hotControl = gridControl;
            if (tool == MapTool.Select)
            {
                selection = new List<Vector2Int>(selectionBeforeDrag);
                if (!selection.Remove(p)) selection.Add(p);
                UpdateVisualizer();
            }
            else if (tool == MapTool.Brush) PaintPoint(p);
            e.Use(); Repaint();
        }
        if (gestureActive && e.type == EventType.MouseDrag)
        {
            if (valid)
            {
                if (p != dragOrigin) gestureMoved = true;
                if (tool == MapTool.Brush) foreach (var point in Line(dragLast, p)) PaintPoint(point);
                dragLast = p;
                if (tool == MapTool.Select)
                    selection = selectionBeforeDrag.Concat(Rectangle(dragOrigin, dragLast)).Distinct().ToList();
                UpdateVisualizer();
            }
            e.Use(); Repaint();
        }
        if (gestureActive && e.type == EventType.MouseUp && e.button == 0)
        {
            if (tool == MapTool.Rectangle) foreach (var point in Rectangle(dragOrigin, dragLast)) PaintPoint(point);
            FinishGesture(); e.Use();
        }
    }
    private void PaintPoint(Vector2Int p)
    {
        var cell = Map.GetCell(p.x, p.y);
        if (cell == null || !painted.Add(p) || DungeonMapEditing.ReadPaint(cell, paintLayer, paintDirection) == paintValue) return;
        if (!gesturePaint)
        {
            Undo.IncrementCurrentGroup(); gestureGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("맵에 재료 칠하기");
            Undo.RegisterCompleteObjectUndo(document, "맵에 재료 칠하기");
            gesturePaint = true;
        }
        DungeonMapEditing.Paint(cell, paintLayer, paintDirection, paintValue);
        EditorUtility.SetDirty(document); hasUnsavedChanges = true;
    }
    private void FinishGesture()
    {
        bool changed = gesturePaint;
        if (gesturePaint && gestureGroup >= 0) { Undo.CollapseUndoOperations(gestureGroup); Undo.IncrementCurrentGroup(); }
        CancelGesture();
        if (changed) RefreshState();
    }
    private void CancelGesture()
    {
        if (gestureActive || panGesture) GUIUtility.hotControl = 0;
        gestureActive = gesturePaint = gestureMoved = panGesture = false;
        gestureGroup = -1; painted?.Clear();
    }
    private void OnLostFocus() { FinishGesture(); }
    private static IEnumerable<Vector2Int> Rectangle(Vector2Int a, Vector2Int b)
    {
        for (int y = Math.Min(a.y, b.y); y <= Math.Max(a.y, b.y); y++)
            for (int x = Math.Min(a.x, b.x); x <= Math.Max(a.x, b.x); x++) yield return new Vector2Int(x, y);
    }
    private static IEnumerable<Vector2Int> Line(Vector2Int a, Vector2Int b)
    {
        int dx = Math.Abs(b.x - a.x), dy = Math.Abs(b.y - a.y), sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1, error = dx - dy;
        while (true)
        {
            yield return a;
            if (a == b) yield break;
            int twice = error * 2;
            if (twice > -dy) { error -= dy; a.x += sx; }
            if (twice < dx) { error += dx; a.y += sy; }
        }
    }
    private void FocusCell(Vector2Int p)
    {
        if (Map.GetCell(p.x, p.y) == null) return;
        selection = new List<Vector2Int> { p };
        gridScroll = new Vector2(Mathf.Max(0, (p.x + 0.5f) * cellSize - gridViewport.width / 2),
            Mathf.Max(0, (Map.height - p.y - 0.5f) * cellSize - gridViewport.height / 2));
        UpdateVisualizer(); Repaint();
    }
}
