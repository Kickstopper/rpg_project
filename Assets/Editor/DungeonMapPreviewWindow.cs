using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Data;
using UI.DungeonMapScene;
using UnityEditor;
using UnityEngine;
using MapRenderSettings = UI.DungeonMapScene.RenderSettings;

public sealed class DungeonMapPreviewWindow : EditorWindow
{
    [SerializeField] private MapData snapshot;
    [SerializeField] private DungeonTheme theme;
    [SerializeField] private Vector2Int positionInMap;
    [SerializeField] private Direction direction;
    private RaycastRenderEngine renderer;
    private DungeonPlayer player;
    private MapRenderSettings settings;
    private string error;
    private bool animate;
    private double nextFrame;

    public static void Open(MapData source, DungeonTheme theme, Vector2Int start)
    {
        var window = GetWindow<DungeonMapPreviewWindow>();
        window.titleContent = new GUIContent("던전 1인칭 미리보기");
        window.minSize = new Vector2(600, 400);
        window.snapshot = DungeonMapEditing.Clone(source);
        window.theme = theme;
        window.positionInMap = start;
        window.direction = source.startDirection;
        window.Rebuild();
        window.Show();
    }
    private void OnEnable() { EditorApplication.update += UpdatePreview; }
    private void OnDisable() { EditorApplication.update -= UpdatePreview; Release(); }
    private void Release()
    {
        if (renderer != null && renderer.ScreenTexture != null) DestroyImmediate(renderer.ScreenTexture);
        renderer = null; player = null;
    }
    private void Rebuild()
    {
        Release(); error = null;
        try
        {
            if (!DungeonMapEditing.CheckStructure(snapshot, out var structuralError)) throw new InvalidOperationException(structuralError);
            if (snapshot.GetCell(positionInMap.x, positionInMap.y) == null) throw new InvalidOperationException("미리보기 위치가 맵 밖에 있습니다.");
            if (theme == null || theme.texture == null || theme.texture.Length == 0 ||
                theme.texture.Any(t => t == null || !t.isReadable || t.width != 64 || t.height != 64))
                throw new InvalidOperationException("테마의 벽/바닥 텍스처는 Read/Write가 켜진 64×64 이미지여야 합니다.");
            if (theme.objectSprites != null && theme.objectSprites.Any(o => o.texture != null && !o.texture.isReadable))
                throw new InvalidOperationException("오브젝트 이미지의 Read/Write 설정을 확인하세요.");
            settings = new MapRenderSettings();
            // 동일 이름/타입의 테마 효과 설정만 복사합니다. 화면 크기 등은 미리보기 설정을 유지합니다.
            foreach (var field in typeof(MapRenderSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var source = typeof(DungeonTheme).GetField(field.Name);
                if (source != null && source.FieldType == field.FieldType) field.SetValue(settings, source.GetValue(theme));
            }
            renderer = new RaycastRenderEngine();
            renderer.Initialize(512, 256);
            var sprites = new List<SpriteInfo>();
            foreach (var cell in snapshot.cells)
            {
                if (cell.centerObjectID >= 0) sprites.Add(new SpriteInfo { x = cell.x + 0.5f, y = cell.y + 0.5f, texIdx = cell.centerObjectID });
                for (int d = 0; d < 4; d++) if (cell.faceObjectIDs[d] >= 0)
                    sprites.Add(new SpriteInfo { x = cell.x + 0.5f + DungeonMapEditing.Directions[d].x * 0.499f,
                        y = cell.y + 0.5f + DungeonMapEditing.Directions[d].y * 0.499f, texIdx = cell.faceObjectIDs[d] });
            }
            renderer.LoadAssets(theme, Array.Empty<Sprite>(), 64, 64, sprites.ToArray());
            renderer.SetMapData(snapshot, theme, new TileAnimState[snapshot.width, snapshot.height]);
            player = new DungeonPlayer(null, 0.66f, 0, theme.passableWallTexIDs ?? new List<int>());
            player.SetMapData(snapshot, positionInMap.x, positionInMap.y, direction);
        }
        catch (Exception ex) { Release(); error = ex.Message; }
        Repaint();
    }
    private void UpdatePreview()
    {
        if (!animate || EditorApplication.timeSinceStartup < nextFrame) return;
        nextFrame = EditorApplication.timeSinceStartup + 0.05;
        Repaint();
    }
    private void Move(int step)
    {
        var offset = DungeonMapEditing.Directions[(int)direction] * step;
        var target = positionInMap + offset;
        var cell = snapshot.GetCell(target.x, target.y);
        if (cell == null || DungeonMapValidation.IsObstacle(theme, cell.centerObjectID) ||
            cell.faceObjectIDs.Any(id => DungeonMapValidation.IsObstacle(theme, id)) ||
            !player.IsWalkable(target.x + 0.5f, target.y + 0.5f, offset.x, offset.y)) return;
        positionInMap = target;
        player.SetDirectPosition(target.x + 0.5f, target.y + 0.5f, (int)direction);
        Repaint();
    }
    private void Turn(int amount)
    {
        direction = (Direction)(((int)direction + amount + 4) % 4);
        player.SetDirectPosition(positionInMap.x + 0.5f, positionInMap.y + 0.5f, (int)direction);
        Repaint();
    }
    private void OnGUI()
    {
        if (snapshot == null) { EditorGUILayout.HelpBox("맵 편집기에서 미리보기를 열어주세요.", MessageType.Info); return; }
        if (renderer == null && error == null) Rebuild();
        EditorGUILayout.HelpBox("열었을 때의 맵 복사본입니다. 방향키로 이동·회전합니다. 이벤트, 전투, 문 개폐, 벽 애니메이션은 실행하지 않습니다. 편집 후 버튼을 다시 눌러 갱신하세요.", MessageType.Info);
        if (!string.IsNullOrEmpty(error))
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
            if (GUILayout.Button("다시 시도")) Rebuild();
            return;
        }
        var e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.UpArrow) { Move(1); e.Use(); }
            else if (e.keyCode == KeyCode.DownArrow) { Move(-1); e.Use(); }
            else if (e.keyCode == KeyCode.LeftArrow) { Turn(-1); e.Use(); }
            else if (e.keyCode == KeyCode.RightArrow) { Turn(1); e.Use(); }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("왼쪽 회전")) Turn(-1);
            if (GUILayout.Button("앞으로")) Move(1);
            if (GUILayout.Button("뒤로")) Move(-1);
            if (GUILayout.Button("오른쪽 회전")) Turn(1);
            animate = GUILayout.Toggle(animate, "환경 효과 재생");
        }
        GUILayout.Label($"{snapshot.mapID} · ({positionInMap.x}, {positionInMap.y}) · {direction}");
        Rect area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (e.type == EventType.Repaint)
        {
            try
            {
                renderer.RenderFrame(player, settings, false, animate ? (float)EditorApplication.timeSinceStartup : 0);
                float width = Mathf.Min(area.width, area.height * 2);
                var screen = new Rect(area.center.x - width / 2, area.center.y - width / 4, width, width / 2);
                EditorGUI.DrawRect(screen, Color.black);
                if (theme.background != null) GUI.DrawTexture(screen, theme.background, ScaleMode.StretchToFill);
                GUI.DrawTexture(screen, renderer.ScreenTexture, ScaleMode.StretchToFill, true);
            }
            catch (Exception ex) { error = "미리보기 렌더링 실패: " + ex.Message; Repaint(); }
        }
    }
}
