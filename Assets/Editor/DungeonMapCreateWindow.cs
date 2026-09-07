using System;
using Data;
using UnityEditor;
using UnityEngine;

public sealed class DungeonMapCreateWindow : EditorWindow
{
    private DungeonTheme[] themes;
    private Func<int, int, string, string, DungeonTheme, bool, bool> create;
    private string mapID = "NewMap", location = "";
    private int width = 10, height = 10, themeIndex;
    private bool ceiling = true;
    private string error = "";

    public static void Show(DungeonTheme[] themes, Func<int, int, string, string, DungeonTheme, bool, bool> create)
    {
        var window = CreateInstance<DungeonMapCreateWindow>();
        window.titleContent = new GUIContent("새 던전 맵");
        window.themes = themes; window.create = create;
        window.position = new Rect(180, 140, 440, 330);
        window.ShowUtility();
    }
    private void OnGUI()
    {
        if (create == null || themes == null) { EditorGUILayout.HelpBox("새 맵 창을 다시 열어주세요.", MessageType.Info); return; }
        EditorGUILayout.HelpBox("이름, 크기, 테마를 정한 뒤 맵을 만듭니다. 맵 ID는 저장할 파일명으로도 사용됩니다.", MessageType.Info);
        mapID = EditorGUILayout.TextField("맵 ID / 파일명", mapID);
        location = EditorGUILayout.TextField("상위 지역 ID", location);
        width = EditorGUILayout.IntField("가로 타일 수", width);
        height = EditorGUILayout.IntField("세로 타일 수", height);
        string[] names = Array.ConvertAll(themes, t => t.name);
        if (names.Length > 0) themeIndex = EditorGUILayout.Popup("테마", themeIndex, names);
        else EditorGUILayout.HelpBox("DungeonTheme 에셋을 먼저 만들어주세요.", MessageType.Warning);
        ceiling = EditorGUILayout.Toggle("천장 사용", ceiling);
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
        using (new EditorGUI.DisabledScope(!DungeonMapEditing.ValidSize(width, height) || !DungeonMapFileIO.ValidID(mapID) || names.Length == 0))
            if (GUILayout.Button("맵 만들기", GUILayout.Height(32)))
            {
                if (create(width, height, mapID, location, themes[themeIndex], ceiling)) Close();
                else error = "맵 ID가 중복되었거나 현재 문서 전환이 취소되었습니다.";
            }
    }
}
