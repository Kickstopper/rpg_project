using System;
using RPGProject.Feature.Exploration;
using UnityEditor;
using UnityEngine;

public sealed class DungeonMapTargetWindow : EditorWindow
{
    private MapData map;
    private Vector2Int selected;
    private Direction direction;
    private Vector2 scroll;
    private Action<Vector2Int, Direction> choose;
    private float size = 40;

    public static void Show(MapData target, Direction direction, Action<Vector2Int, Direction> choose)
    {
        var window = CreateInstance<DungeonMapTargetWindow>();
        window.titleContent = new GUIContent("도착 위치 — " + target.mapID);
        window.map = DungeonMapEditing.Clone(target);
        window.selected = new Vector2Int(target.startX, target.startY);
        window.direction = direction; window.choose = choose;
        window.position = new Rect(160, 100, 660, 580);
        window.ShowUtility();
    }
    private void OnGUI()
    {
        if (map == null) { EditorGUILayout.HelpBox("도착점 선택 창을 다시 열어주세요.", MessageType.Info); return; }
        GUILayout.Label("도착 타일을 클릭하세요. 어두운 타일은 바닥 구멍, 빨간 선은 벽입니다.", EditorStyles.wordWrappedLabel);
        size = EditorGUILayout.Slider("확대", size, 16, 72);
        Rect view = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, map.width * size, map.height * size));
        int x0 = Mathf.Max(0, Mathf.FloorToInt(scroll.x / size)), x1 = Mathf.Min(map.width - 1, Mathf.CeilToInt((scroll.x + view.width) / size));
        int row0 = Mathf.Max(0, Mathf.FloorToInt(scroll.y / size)), row1 = Mathf.Min(map.height - 1, Mathf.CeilToInt((scroll.y + view.height) / size));
        for (int row = row0; row <= row1; row++)
            for (int x = x0; x <= x1; x++)
            {
                int y = map.height - 1 - row;
                var cell = map.GetCell(x, y);
                Rect r = new Rect(x * size, row * size, size - 1, size - 1);
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = selected == new Vector2Int(x, y) ? Color.cyan : cell.value == -1 ? Color.black : Color.gray;
                string label = size >= 30 ? $"{x},{y}" : "";
                if (GUI.Button(r, label)) selected = new Vector2Int(x, y);
                GUI.backgroundColor = previous;
                if (cell.wallTextureIDs[0] >= 0) EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), Color.red);
                if (cell.wallTextureIDs[1] >= 0) EditorGUI.DrawRect(new Rect(r.xMax - 2, r.y, 2, r.height), Color.red);
                if (cell.wallTextureIDs[2] >= 0) EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), Color.red);
                if (cell.wallTextureIDs[3] >= 0) EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), Color.red);
            }
        GUI.EndScrollView();
        GUILayout.Label($"선택한 도착점: ({selected.x}, {selected.y})");
        direction = (Direction)EditorGUILayout.Popup("도착 방향", (int)direction, DungeonMapEditor.DirectionNames);
        var target = map.GetCell(selected.x, selected.y);
        using (new EditorGUI.DisabledScope(target == null || target.value == -1))
            if (GUILayout.Button("이 위치로 연결", GUILayout.Height(30)))
            { var callback = choose; Close(); callback?.Invoke(selected, direction); }
    }
}
