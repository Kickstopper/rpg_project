using System;
using UnityEditor;
using UnityEngine;

public sealed class DungeonMapChoiceWindow : EditorWindow
{
    private string[] labels;
    private Texture[] images;
    private Action<int> onChoose;
    private string search = "";
    private Vector2 scroll;

    public static void Show(string title, string[] labels, Action<int> onChoose, Texture[] images = null)
    {
        var window = CreateInstance<DungeonMapChoiceWindow>();
        window.titleContent = new GUIContent(title);
        window.labels = labels; window.images = images; window.onChoose = onChoose;
        window.position = new Rect(180, 120, 560, 480);
        window.ShowUtility();
    }
    private void OnGUI()
    {
        if (labels == null) { EditorGUILayout.HelpBox("목록을 다시 열어주세요.", MessageType.Info); return; }
        GUI.SetNextControlName("Search");
        search = EditorGUILayout.TextField("검색", search);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        int matches = 0;
        for (int i = 0; i < labels.Length; i++)
        {
            if (!string.IsNullOrEmpty(search) && labels[i].IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            matches++;
            Texture image = images != null && i < images.Length ? images[i] : null;
            var content = new GUIContent(labels[i], image, labels[i]);
            if (GUILayout.Button(content, GUILayout.Height(image != null ? 56 : 34)))
            { var callback = onChoose; int choice = i; Close(); callback?.Invoke(choice); GUIUtility.ExitGUI(); }
        }
        if (matches == 0) EditorGUILayout.HelpBox("항목이 없습니다. 검색어 또는 프로젝트 데이터를 확인하세요.", MessageType.Info);
        EditorGUILayout.EndScrollView();
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) Close();
    }
    private void OnLostFocus() { Close(); }
}
