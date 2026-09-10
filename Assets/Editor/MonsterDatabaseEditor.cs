using System;
using System.Collections.Generic;
using System.Linq;
using MonsterEditing;
using RPGProject.Feature.Characters;
using UnityEditor;
using UnityEngine;

public class MonsterDatabaseEditor : EditorWindow
{
    [SerializeField] private MonsterDatabase database;
    [SerializeField] private int selectedIndex;
    [SerializeField] private string search = "";
    [SerializeField] private bool bossesOnly, ascending = true, autoPlay = true;
    [SerializeField] private float zoom = 1f;
    [SerializeField] private Color previewBackground = new Color(0.15f, 0.16f, 0.18f);
    private enum SortType { Original, Name, Race, Level, Gender }
    [SerializeField] private SortType sort = SortType.Original;
    private SerializedObject serializedDB;
    private SerializedProperty entriesProp;
    private Vector2 listScroll, detailScroll, frameScroll, previewScroll;
    private readonly List<int> visible = new List<int>();
    private readonly HashSet<string> duplicateIds = new HashSet<string>();
    private MonsterPreviewClock clock;
    private Sprite[] previewFrames = Array.Empty<Sprite>();
    private Vector2 canvasPixels = Vector2.one;
    private float configuredInterval;
    private double nextUpdate, nextLoadingRefresh;
    private int previewSource;
    private bool loadingPreview;
    private string notice = "";
    private static readonly string[] SortLabels = { "등록 순서", "이름", "종족", "레벨", "성별" };
    private static readonly string[] SourceFields = { "image", "fallDownImgs", "downImgs", "leftImgs", "rightImgs", "upImgs" };
    private static readonly string[] SourceLabels = { "기본 / 전투", "넘어짐 (참고)", "아래 이동 (참고)", "왼쪽 이동 (참고)", "오른쪽 이동 (참고)", "위 이동 (참고)" };
    private static readonly HashSet<string> MainFields = new HashSet<string>
    { "id", "name", "isBoss", "race", "align", "gender", "portrait", "image", "animInterval", "fallDownImgs", "downImgs", "leftImgs", "rightImgs", "upImgs" };
    private MonsterDatabase.MonsterEntry Selected => database != null && database.entries != null && selectedIndex >= 0 && selectedIndex < database.entries.Count ? database.entries[selectedIndex] : null;

    [MenuItem("Tools/Monster Database Editor")]
    public static void ShowWindow() => GetWindow<MonsterDatabaseEditor>("몬스터 DB").Show();

    private void OnEnable()
    {
        minSize = new Vector2(980, 650);
        clock = new MonsterPreviewClock();
        if (database == null)
        {
            database = AssetDatabase.LoadAssetAtPath<MonsterDatabase>("Assets/Database/MonsterDatabase.asset");
            if (database == null)
            {
                string guid = AssetDatabase.FindAssets("t:MonsterDatabase").OrderBy(g => g).FirstOrDefault();
                if (guid != null) database = AssetDatabase.LoadAssetAtPath<MonsterDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }
        BindDatabase(database);
        EditorApplication.update += EditorTick;
        EditorApplication.projectChanged += OnProjectChanged;
        Undo.undoRedoPerformed += OnUndo;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorTick;
        EditorApplication.projectChanged -= OnProjectChanged;
        Undo.undoRedoPerformed -= OnUndo;
        serializedDB?.Dispose(); serializedDB = null; entriesProp = null;
        // AssetPreview textures and Sprite textures are owned by Unity; never destroy them here.
    }

    private void BindDatabase(MonsterDatabase next)
    {
        serializedDB?.Dispose(); serializedDB = null; entriesProp = null;
        database = next;
        if (database != null)
        {
            serializedDB = new SerializedObject(database);
            entriesProp = serializedDB.FindProperty("entries");
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, entriesProp.arraySize - 1));
        }
        RebuildList(); ConfigurePreview(true, true); Repaint();
    }

    private void OnProjectChanged()
    {
        // Preserve the selected database and pause state after unrelated asset imports.
        if (database == null) { BindDatabase(null); return; }
        OnUndo();
    }
    private void OnUndo()
    {
        if (serializedDB != null) serializedDB.Update();
        if (database != null && database.entries != null) selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, database.entries.Count - 1));
        RebuildList(); ConfigurePreview(true, false); Repaint();
    }

    private void RebuildList()
    {
        visible.Clear(); duplicateIds.Clear();
        if (database == null || database.entries == null) return;
        foreach (var group in database.entries.Where(e => e != null && !string.IsNullOrEmpty(e.id)).GroupBy(e => e.id))
            if (group.Count() > 1) duplicateIds.Add(group.Key);
        for (int i = 0; i < database.entries.Count; i++)
        {
            var e = database.entries[i];
            if (e == null) { if (!bossesOnly && string.IsNullOrEmpty(search)) visible.Add(i); continue; }
            if (bossesOnly && !e.isBoss) continue;
            string text = (e.name ?? "") + " " + e.id + " " + e.race;
            if (!string.IsNullOrEmpty(search) && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            visible.Add(i);
        }
        visible.Sort((a, b) =>
        {
            var left = database.entries[a]; var right = database.entries[b];
            int compare = 0;
            if (left == null || right == null) compare = left == right ? 0 : left == null ? 1 : -1;
            else switch (sort)
            {
                case SortType.Name: compare = string.Compare(left.name, right.name, StringComparison.Ordinal); break;
                case SortType.Race: compare = left.race.CompareTo(right.race); break;
                case SortType.Level: compare = left.stats.level.CompareTo(right.stats.level); break;
                case SortType.Gender: compare = left.gender.CompareTo(right.gender); break;
            }
            if (compare != 0) return ascending ? compare : -compare;
            return a.CompareTo(b); // Stable ties; database storage order never changes.
        });
    }

    private Sprite[] FramesFor(MonsterDatabase.MonsterEntry entry)
    {
        if (entry == null) return Array.Empty<Sprite>();
        switch (previewSource)
        {
            case 1: return entry.fallDownImgs ?? Array.Empty<Sprite>();
            case 2: return entry.downImgs ?? Array.Empty<Sprite>();
            case 3: return entry.leftImgs ?? Array.Empty<Sprite>();
            case 4: return entry.rightImgs ?? Array.Empty<Sprite>();
            case 5: return entry.upImgs ?? Array.Empty<Sprite>();
            default: return entry.image ?? Array.Empty<Sprite>();
        }
    }

    private void ConfigurePreview(bool resetFrame, bool useAutoPlay)
    {
        if (clock == null) clock = new MonsterPreviewClock();
        var entry = Selected;
        Sprite[] frames = FramesFor(entry);
        previewFrames = (Sprite[])frames.Clone();
        canvasPixels = Vector2.one;
        foreach (Sprite sprite in previewFrames)
            if (sprite != null) canvasPixels = Vector2.Max(canvasPixels, sprite.rect.size);
        configuredInterval = entry == null ? 0 : entry.animInterval;
        clock.Configure(previewFrames.Length, configuredInterval, EditorApplication.timeSinceStartup, resetFrame);
        if (useAutoPlay) clock.SetPlaying(autoPlay, EditorApplication.timeSinceStartup);
        loadingPreview = false;
    }

    private void EditorTick()
    {
        if (database == null || clock == null || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        if (now < nextUpdate) return;
        nextUpdate = now + 1.0 / 60.0;
        Sprite[] current = FramesFor(Selected);
        bool changed = current.Length != previewFrames.Length;
        for (int i = 0; !changed && i < current.Length; i++) changed = current[i] != previewFrames[i];
        float interval = Selected == null ? 0 : Selected.animInterval;
        if (changed || !interval.Equals(configuredInterval))
        {
            ConfigurePreview(changed, false); Repaint();
        }
        bool loadingRefresh = loadingPreview && now >= nextLoadingRefresh;
        if (loadingRefresh) nextLoadingRefresh = now + 0.1;
        if (clock.Tick(now) || loadingRefresh) Repaint();
    }

    private void Mutate(string label, Action action)
    {
        if (database == null || EditorApplication.isPlaying) return;
        serializedDB.ApplyModifiedProperties();
        Undo.RegisterCompleteObjectUndo(database, label);
        action(); EditorUtility.SetDirty(database);
        serializedDB.Update(); RebuildList(); ConfigurePreview(true, true);
        int selectedRow = visible.IndexOf(selectedIndex);
        if (selectedRow >= 0) listScroll.y = selectedRow * 49;
        Repaint();
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            var chosen = (MonsterDatabase)EditorGUILayout.ObjectField(database, typeof(MonsterDatabase), false, GUILayout.Width(245));
            if (EditorGUI.EndChangeCheck()) { selectedIndex = 0; BindDatabase(chosen); }
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(75))) BindDatabase(database);
            using (new EditorGUI.DisabledScope(database == null))
                if (GUILayout.Button("에셋 위치", EditorStyles.toolbarButton, GUILayout.Width(75))) EditorGUIUtility.PingObject(database);
            using (new EditorGUI.DisabledScope(database == null || EditorApplication.isPlaying))
            {
                if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(55)))
                { serializedDB.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(database); notice = "선택한 몬스터 DB를 저장했습니다."; }
                if (GUILayout.Button("빈 ID 채우기", EditorStyles.toolbarButton, GUILayout.Width(95)))
                { Mutate("빈 몬스터 ID 채우기", () => notice = MonsterEditorOperations.FillMissingIds(database.entries) + "개의 빈 ID를 채웠습니다. 기존 ID는 유지됩니다."); GUIUtility.ExitGUI(); }
            }
            GUILayout.FlexibleSpace();
            if (database != null) GUILayout.Label(EditorUtility.IsDirty(database) ? "저장하지 않은 변경 있음" : "저장됨", EditorStyles.miniLabel);
        }
        if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, MessageType.Info);
        if (database == null || serializedDB == null || entriesProp == null)
        { EditorGUILayout.HelpBox("편집할 MonsterDatabase 에셋을 선택하세요.", MessageType.Info); return; }
        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("Play Mode에서는 미리보기만 사용할 수 있습니다. 데이터 편집은 Play Mode를 종료한 뒤 진행하세요.", MessageType.Info);
        serializedDB.Update();
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawList();
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(340), GUILayout.ExpandWidth(true)))
            {
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) DrawDetails();
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(330)))
            {
                previewScroll = EditorGUILayout.BeginScrollView(previewScroll);
                DrawAnimationPreview(); EditorGUILayout.EndScrollView();
            }
        }
        if (serializedDB.ApplyModifiedProperties())
        {
            RebuildList();
            // EditorTick detects changes to frames/interval without marking the asset dirty itself.
            Repaint();
        }
    }

    private void DrawList()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(260)))
        {
            GUILayout.Label("몬스터 선택", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("정렬", GUILayout.Width(30));
                sort = (SortType)EditorGUILayout.Popup((int)sort, SortLabels);
                ascending = GUILayout.Toggle(ascending, "오름차순", "Button", GUILayout.Width(70));
            }
            bossesOnly = EditorGUILayout.ToggleLeft("보스만 보기", bossesOnly);
            if (EditorGUI.EndChangeCheck()) { RebuildList(); listScroll = Vector2.zero; }
            GUILayout.Label($"표시 {visible.Count} / 전체 {entriesProp.arraySize}", EditorStyles.miniLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            const float rowHeight = 49;
            Rect all = GUILayoutUtility.GetRect(0, Mathf.Max(1, visible.Count * rowHeight), GUILayout.ExpandWidth(true));
            int first = Mathf.Max(0, Mathf.FloorToInt(listScroll.y / rowHeight) - 1);
            int last = Mathf.Min(visible.Count, first + Mathf.CeilToInt(position.height / rowHeight) + 2);
            for (int row = first; row < last; row++)
            {
                int index = visible[row]; var entry = database.entries[index];
                string label = entry == null ? "비어 있는 항목" : $"{(entry.isBoss ? "[보스] " : "")}{entry.name}\n{entry.id} · Lv {entry.stats.level} · {entry.race}";
                var rect = new Rect(all.x, all.y + row * rowHeight, all.width, rowHeight - 3);
                if (GUI.Toggle(rect, selectedIndex == index, label, "Button") && selectedIndex != index)
                { selectedIndex = index; detailScroll = frameScroll = Vector2.zero; ConfigurePreview(true, true); GUI.FocusControl(null); }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("＋ 새 몬스터"))
                {
                    Mutate("몬스터 추가", () =>
                    {
                        if (database.entries == null) database.entries = new List<MonsterDatabase.MonsterEntry>();
                        database.entries.Add(MonsterEditorOperations.NewEntry(database.entries));
                        selectedIndex = database.entries.Count - 1; search = ""; bossesOnly = false;
                    });
                    GUIUtility.ExitGUI();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(Selected == null))
                        if (GUILayout.Button("선택 항목 복제"))
                        {
                            Mutate("몬스터 복제", () => { database.entries.Add(MonsterEditorOperations.Duplicate(Selected, database.entries)); selectedIndex = database.entries.Count - 1; search = ""; bossesOnly = false; });
                            GUIUtility.ExitGUI();
                        }
                    using (new EditorGUI.DisabledScope(entriesProp.arraySize == 0))
                        if (GUILayout.Button("삭제") && EditorUtility.DisplayDialog("몬스터 삭제", "던전·대화·아이템 등에서 이 ID를 참조할 수 있습니다. 선택한 항목을 삭제할까요? 되돌리기로 복구할 수 있습니다.", "삭제", "취소"))
                        {
                            Mutate("몬스터 삭제", () => { database.entries.RemoveAt(selectedIndex); selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, database.entries.Count - 1)); });
                            GUIUtility.ExitGUI();
                        }
                }
            }
            EditorGUILayout.HelpBox("정렬은 목록 표시만 바꿉니다. 실제 DB의 순서와 기존 ID는 유지됩니다.", MessageType.None);
        }
    }

    private void Property(SerializedProperty entry, string field, string label)
    {
        var property = entry.FindPropertyRelative(field);
        if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }

    private void DrawDetails()
    {
        if (Selected == null) { EditorGUILayout.HelpBox("목록에서 몬스터를 선택하거나 새 항목을 추가하세요.", MessageType.Info); return; }
        SerializedProperty entry = entriesProp.GetArrayElementAtIndex(selectedIndex);
        GUILayout.Label("기본 정보", EditorStyles.boldLabel);
        if (!visible.Contains(selectedIndex)) EditorGUILayout.HelpBox("현재 편집 중인 몬스터가 검색 필터 밖에 있습니다.", MessageType.Info);
        Property(entry, "name", "이름"); Property(entry, "id", "고유 ID");
        if (string.IsNullOrWhiteSpace(Selected.id)) EditorGUILayout.HelpBox("ID가 비어 있습니다. '빈 ID 채우기'를 사용할 수 있습니다.", MessageType.Warning);
        else if (duplicateIds.Contains(Selected.id)) EditorGUILayout.HelpBox("같은 ID가 중복됩니다. 참조가 모호해지므로 고유 ID로 수정하세요.", MessageType.Error);
        EditorGUILayout.HelpBox("기존 ID를 직접 수정하면 그 ID를 사용하는 던전·대화 등의 참조도 별도로 갱신해야 합니다.", MessageType.None);
        Property(entry, "isBoss", "보스"); Property(entry, "race", "종족"); Property(entry, "align", "성향"); Property(entry, "gender", "성별");
        Property(entry, "portrait", "초상화");
        GUILayout.Space(8);
        GUILayout.Label("애니메이션 이미지", EditorStyles.boldLabel);
        int source = EditorGUILayout.Popup("편집할 프레임", previewSource, SourceLabels);
        if (source != previewSource) { previewSource = source; frameScroll = Vector2.zero; ConfigurePreview(true, true); }
        Property(entry, "animInterval", "프레임 간격 (초)");
        if (previewSource != 0) EditorGUILayout.HelpBox("이동·넘어짐 배열은 편집 참고용으로 같은 animInterval을 적용해 보여줍니다. 실제 이동 연출의 재생 속도와는 다를 수 있습니다.", MessageType.Info);
        var frames = entry.FindPropertyRelative(SourceFields[previewSource]);
        EditorGUILayout.PropertyField(frames, new GUIContent("프레임 배열 — 위에서부터 재생"), true);
        EditorGUILayout.HelpBox("프레임 배열에서 개수·이미지·순서를 편집하면 오른쪽 미리보기에 반영됩니다. 프레임을 클릭해 해당 이미지를 정지 상태로 확인할 수도 있습니다.", MessageType.None);
        GUILayout.Space(8);
        GUILayout.Label("전투·능력치·교섭·보상", EditorStyles.boldLabel);
        SerializedProperty iterator = entry.Copy(), end = iterator.GetEndProperty();
        if (iterator.NextVisible(true))
            do
            {
                if (SerializedProperty.EqualContents(iterator, end)) break;
                if (!MainFields.Contains(iterator.name)) EditorGUILayout.PropertyField(iterator, true);
            } while (iterator.NextVisible(false));
    }

    private void DrawAnimationPreview()
    {
        GUILayout.Label("애니메이션 미리보기", EditorStyles.boldLabel);
        if (Selected == null) { GUILayout.Label("몬스터를 선택하세요."); return; }
        GUILayout.Label((Selected.name ?? "") + " · " + SourceLabels[previewSource], EditorStyles.wordWrappedLabel);
        autoPlay = EditorGUILayout.ToggleLeft("몬스터 선택 시 자동 재생", autoPlay);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            var interval = entriesProp.GetArrayElementAtIndex(selectedIndex).FindPropertyRelative("animInterval");
            EditorGUILayout.PropertyField(interval, new GUIContent("프레임 간격 (초)"));
        }
        double seconds = configuredInterval;
        if (seconds > 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds))
            GUILayout.Label($"초당 {1.0 / seconds:0.##} 프레임 · 한 바퀴 {seconds * previewFrames.Length:0.###}초", EditorStyles.miniLabel);
        previewBackground = EditorGUILayout.ColorField("배경색", previewBackground);
        zoom = EditorGUILayout.Slider("확대", zoom, 0.25f, 3f);
        Rect canvas = GUILayoutUtility.GetRect(300, 265, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(canvas, previewBackground);
        Sprite current = previewFrames.Length > 0 ? previewFrames[Mathf.Clamp(clock.Frame, 0, previewFrames.Length - 1)] : null;
        bool waiting = MonsterSpritePreview.Draw(canvas, current, canvasPixels, zoom);
        if (Event.current.type == EventType.Repaint) loadingPreview = waiting;
        GUILayout.Label(previewFrames.Length == 0 ? "프레임 0 / 0" : $"프레임 {clock.Frame + 1} / {previewFrames.Length} · {(current != null ? current.name : "빈 프레임")}", EditorStyles.centeredGreyMiniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!clock.CanPlay))
                if (GUILayout.Button(clock.Playing ? "일시정지" : "재생")) { clock.SetPlaying(!clock.Playing, EditorApplication.timeSinceStartup); Repaint(); }
            using (new EditorGUI.DisabledScope(previewFrames.Length == 0))
            {
                if (GUILayout.Button("처음")) { clock.Seek(0, EditorApplication.timeSinceStartup); Repaint(); }
                if (GUILayout.Button("이전")) { clock.Seek((clock.Frame + previewFrames.Length - 1) % previewFrames.Length, EditorApplication.timeSinceStartup); Repaint(); }
                if (GUILayout.Button("다음")) { clock.Seek((clock.Frame + 1) % previewFrames.Length, EditorApplication.timeSinceStartup); Repaint(); }
            }
        }
        if (previewFrames.Length > 1)
        {
            EditorGUI.BeginChangeCheck();
            int frame = EditorGUILayout.IntSlider("직접 선택", clock.Frame + 1, 1, previewFrames.Length);
            if (EditorGUI.EndChangeCheck()) { clock.Seek(frame - 1, EditorApplication.timeSinceStartup); Repaint(); }
        }
        if (previewFrames.Length == 0) EditorGUILayout.HelpBox("프레임 배열에 Sprite를 추가하세요.", MessageType.Info);
        else if (previewFrames.Length == 1) EditorGUILayout.HelpBox("프레임이 한 장이므로 정지 이미지로 표시합니다.", MessageType.Info);
        if (float.IsNaN(configuredInterval) || float.IsInfinity(configuredInterval) || configuredInterval <= 0)
            EditorGUILayout.HelpBox("재생 간격이 0 이하이거나 유효하지 않아 자동 재생하지 않습니다. 양수로 설정한 뒤 재생을 누르세요.", MessageType.Warning);
        if (previewFrames.Any(s => s == null)) EditorGUILayout.HelpBox("비어 있는 프레임도 순서와 시간을 차지합니다. 누락된 이미지를 확인하세요.", MessageType.Warning);
        frameScroll = EditorGUILayout.BeginScrollView(frameScroll, GUILayout.Height(76));
        using (new EditorGUILayout.HorizontalScope())
            for (int i = 0; i < previewFrames.Length; i++)
            {
                Sprite sprite = previewFrames[i];
                Rect thumb = GUILayoutUtility.GetRect(58, 56, GUILayout.Width(58), GUILayout.Height(56));
                Color previousColor = GUI.backgroundColor;
                if (i == clock.Frame) GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
                bool clicked = GUI.Button(thumb, new GUIContent("", sprite != null ? sprite.name : "빈 프레임"));
                GUI.backgroundColor = previousColor;
                if (clicked) { clock.Seek(i, EditorApplication.timeSinceStartup); Repaint(); }
                Rect picture = new Rect(thumb.x + 3, thumb.y + 2, thumb.width - 6, 36);
                bool pending = MonsterSpritePreview.Draw(picture, sprite, sprite != null ? sprite.rect.size : Vector2.one, 1f);
                if (Event.current.type == EventType.Repaint) loadingPreview |= pending;
                GUI.Label(new Rect(thumb.x, thumb.y + 37, thumb.width, 17), (i + 1).ToString(), EditorStyles.centeredGreyMiniLabel);
            }
        EditorGUILayout.EndScrollView();
        GUILayout.Label("에디터 실제 시간을 사용합니다. Play Mode·Time.timeScale에 영향을 받지 않습니다. 화면 갱신은 최대 60회/초이며 그보다 빠른 프레임은 건너뛰어 보일 수 있습니다.", EditorStyles.wordWrappedMiniLabel);
        GUILayout.FlexibleSpace();
    }
}
