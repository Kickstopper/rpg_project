using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DialogueEditing;
using UnityEditor;
using UnityEngine;

public class DialogueEditorWindow : EditorWindow
{
    [SerializeField] private DialogueDocument document;
    [SerializeField] private string selectedEvent = "", selectedKey = "", newEvent = "NEW_EVENT";
    [SerializeField] private string eventSearch = "", assetSearch = "";
    [SerializeField] private bool advanced;
    [SerializeField] private string baselineHash = "", baselineCsv = "";
    private Vector2 eventScroll, stepScroll, detailScroll, previewScroll;
    private DialogueAssetCatalog catalog;
    private List<DialogueIssue> issues = new List<DialogueIssue>();
    private List<string> events = new List<string>();
    private List<DialogueRow> lines = new List<DialogueRow>();
    private int previewIndex = -1;
    private string notice = "", previewNotice = "", previewBackground = "";
    private static readonly string[] Types = { "TALK", "JOIN", "LEAVE" };
    private static readonly string[] TypeLabels = { "대사", "동료 합류", "동료 이탈" };

    [MenuItem("Tools/Dialogue Editor")]
    public static void ShowWindow() => GetWindow<DialogueEditorWindow>("대화 이벤트");

    private void OnEnable()
    {
        minSize = new Vector2(1050, 650);
        saveChangesMessage = "대화 이벤트에 저장하지 않은 변경이 있습니다.";
        catalog = new DialogueAssetCatalog(); catalog.Reload();
        if (document == null)
        {
            document = CreateInstance<DialogueDocument>();
            document.hideFlags = HideFlags.HideAndDontSave;
            if (File.Exists(DialogueFileStore.DraftPath))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(DialogueFileStore.DraftPath), document);
                    if (!document.loaded) throw new FormatException("초안 형식이 올바르지 않습니다.");
                    notice = "이전에 저장하지 않은 초안을 복구했습니다. 저장 전에 내용을 확인하세요.";
                }
                catch (Exception ex)
                {
                    string recovery = DialogueFileStore.DraftPath + ".unreadable." + Guid.NewGuid().ToString("N");
                    File.Copy(DialogueFileStore.DraftPath, recovery);
                    notice = "초안을 복구하지 못했습니다. 원본 보관 위치: " + recovery + " / " + ex.Message;
                    document.rows.Clear(); document.loaded = false;
                }
            }
            if (!document.loaded) LoadFromDisk(false);
        }
        baselineHash = document.sourceHash; baselineCsv = document.savedCsv;
        Undo.undoRedoPerformed += OnUndo;
        EditorApplication.projectChanged += OnProjectChanged;
        Refresh();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndo;
        EditorApplication.projectChanged -= OnProjectChanged;
    }

    private void OnUndo()
    {
        document.sourceHash = baselineHash; document.savedCsv = baselineCsv;
        Refresh(); PersistDraft();
    }
    private void OnDestroy()
    {
        if (document != null) { Undo.ClearUndo(document); DestroyImmediate(document); }
    }
    private void OnProjectChanged() { catalog.Reload(); Repaint(); }

    private void Refresh()
    {
        events = document.rows.Select(r => r.EventID).Distinct().ToList();
        if (!events.Contains(selectedEvent)) selectedEvent = events.FirstOrDefault() ?? "";
        lines = document.Event(selectedEvent);
        if (!lines.Any(r => r.key == selectedKey)) selectedKey = lines.FirstOrDefault()?.key ?? "";
        issues = DialogueValidation.Check(document);
        hasUnsavedChanges = document.loaded && document.Encode() != document.savedCsv;
        previewIndex = -1; previewBackground = "";
        Repaint();
    }

    private void Change(string label, Action edit)
    {
        Undo.RegisterCompleteObjectUndo(document, label);
        edit(); EditorUtility.SetDirty(document);
        Refresh(); PersistDraft();
    }

    private void PersistDraft()
    {
        try
        {
            if (hasUnsavedChanges)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DialogueFileStore.DraftPath));
                string temp = DialogueFileStore.DraftPath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(document), Encoding.UTF8);
                if (File.Exists(DialogueFileStore.DraftPath)) File.Replace(temp, DialogueFileStore.DraftPath, null);
                else File.Move(temp, DialogueFileStore.DraftPath);
            }
            else if (File.Exists(DialogueFileStore.DraftPath)) File.Delete(DialogueFileStore.DraftPath);
        }
        catch (Exception ex) { notice = "복구용 초안을 저장하지 못했습니다: " + ex.Message; }
    }

    public override void SaveChanges()
    {
        if (SaveToDisk()) base.SaveChanges();
        else PersistDraft();
    }

    public override void DiscardChanges()
    {
        if (File.Exists(DialogueFileStore.DraftPath)) File.Delete(DialogueFileStore.DraftPath);
        base.DiscardChanges();
    }

    private bool SaveToDisk()
    {
        issues = DialogueValidation.Check(document);
        if (!document.loaded || issues.Any(i => i.error))
        {
            notice = "저장하지 않았습니다. 빨간 오류를 수정하세요. '초안 내보내기'로 작업 내용을 보관할 수 있습니다.";
            return false;
        }
        int warnings = issues.Count(i => !i.error);
        if (warnings > 0 && !EditorUtility.DisplayDialog("확인할 항목이 있습니다", $"경고 {warnings}개가 있습니다. 현재 런타임에서 적용되지 않는 설정 등을 확인한 뒤 저장하세요.", "내용을 확인했으며 저장", "돌아가기")) return false;
        try
        {
            string csv = document.Encode();
            string backup = DialogueFileStore.Save(DialogueFileStore.CsvPath, csv, document.sourceHash, "UserSettings/DialogueEditor/Backups");
            document.sourceHash = DialogueFileStore.Hash(DialogueFileStore.CsvPath);
            document.savedCsv = csv;
            baselineHash = document.sourceHash; baselineCsv = csv;
            hasUnsavedChanges = false; PersistDraft();
            AssetDatabase.ImportAsset(DialogueFileStore.CsvPath, ImportAssetOptions.ForceUpdate);
            notice = "저장했습니다. 이전 CSV 백업: " + backup;
            return true;
        }
        catch (Exception ex) { notice = "저장하지 못했습니다. " + ex.Message; return false; }
    }

    private void LoadFromDisk(bool confirm)
    {
        if (confirm && hasUnsavedChanges)
        {
            int result = EditorUtility.DisplayDialogComplex("편집 중인 내용이 있습니다", "CSV를 다시 불러오기 전에 현재 변경을 어떻게 할까요?", "저장 후 불러오기", "취소", "변경 버리기");
            if (result == 1 || (result == 0 && !SaveToDisk())) return;
        }
        try
        {
            string text = DialogueFileStore.ReadSnapshot(DialogueFileStore.CsvPath, out string hash);
            List<DialogueRow> decoded = DialogueDocument.Decode(text);
            // Parsing must succeed before replacing the current draft.
            Undo.ClearUndo(document);
            document.rows = decoded; document.sourceHash = hash; document.loaded = true;
            document.savedCsv = document.Encode();
            baselineHash = hash; baselineCsv = document.savedCsv;
            Refresh(); PersistDraft();
            notice = "CSV를 불러왔습니다. 이벤트를 고른 뒤 대사나 선택지를 편집하세요.";
        }
        catch (Exception ex) { notice = "불러오지 못했습니다. 현재 편집 내용은 유지됩니다. " + ex.Message; }
    }

    private void OnGUI()
    {
        if (document == null) return;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("CSV 다시 불러오기", EditorStyles.toolbarButton, GUILayout.Width(135))) LoadFromDisk(true);
            using (new EditorGUI.DisabledScope(!document.loaded))
            {
                if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(55))) SaveToDisk();
                if (GUILayout.Button("초안 내보내기", EditorStyles.toolbarButton, GUILayout.Width(100))) ExportDraft();
            }
            if (GUILayout.Button("되돌리기", EditorStyles.toolbarButton, GUILayout.Width(75))) { Undo.PerformUndo(); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("다시 실행", EditorStyles.toolbarButton, GUILayout.Width(75))) { Undo.PerformRedo(); GUIUtility.ExitGUI(); }
            GUILayout.FlexibleSpace();
            GUILayout.Label(hasUnsavedChanges ? "저장하지 않은 변경 있음" : "저장된 내용", EditorStyles.miniLabel);
            GUILayout.Label($"오류 {issues.Count(i => i.error)} · 경고 {issues.Count(i => !i.error)}", EditorStyles.miniLabel);
        }
        if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, MessageType.Info);
        if (!document.loaded) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawEvents(); DrawSteps();
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(330), GUILayout.ExpandWidth(true)))
            {
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                DrawDetail(); EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(270)))
            {
                previewScroll = EditorGUILayout.BeginScrollView(previewScroll);
                DrawPreview(); EditorGUILayout.EndScrollView();
            }
        }
    }

    private void DrawEvents()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(190)))
        {
            GUILayout.Label("1. 이벤트", EditorStyles.boldLabel);
            eventSearch = EditorGUILayout.TextField(eventSearch, EditorStyles.toolbarSearchField);
            eventScroll = EditorGUILayout.BeginScrollView(eventScroll);
            foreach (string id in events)
            {
                if (!id.Contains(eventSearch, StringComparison.OrdinalIgnoreCase)) continue;
                int errors = issues.Count(i => i.error && i.row.EventID == id);
                string label = (id == selectedEvent ? "● " : "") + (string.IsNullOrEmpty(id) ? "(ID 없음)" : id) + (errors > 0 ? $"  !{errors}" : "");
                if (GUILayout.Button(label, EditorStyles.miniButton)) { selectedEvent = id; selectedKey = ""; Refresh(); GUI.FocusControl(null); }
            }
            EditorGUILayout.EndScrollView();
            GUILayout.Label("새 이벤트 ID", EditorStyles.miniLabel);
            newEvent = EditorGUILayout.TextField(newEvent);
            if (GUILayout.Button("이벤트 만들기"))
            {
                string id = newEvent.Trim();
                if (string.IsNullOrEmpty(id) || events.Contains(id) || id.IndexOfAny(new[] { ',', ':', ';', '\r', '\n' }) >= 0) notice = "중복되지 않는 이벤트 ID를 입력하세요. 쉼표·콜론·줄바꿈은 사용할 수 없습니다.";
                else Change("이벤트 만들기", () => { selectedEvent = id; selectedKey = document.AddTalk(id).key; });
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.HelpBox("이벤트 ID는 던전과 퀘스트에서 참조합니다. 기존 ID는 유지하세요.", MessageType.None);
        }
    }

    private void DrawSteps()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(230)))
        {
            GUILayout.Label("2. 진행 순서", EditorStyles.boldLabel);
            stepScroll = EditorGUILayout.BeginScrollView(stepScroll);
            foreach (DialogueRow row in lines)
            {
                string indent = DialogueDocument.Kind(row) == "BRANCH" ? "    └ " : "";
                string text = row.Text.Replace('\n', ' ').Replace('\r', ' ');
                if (text.Length > 20) text = text.Substring(0, 20) + "…";
                string label = $"{indent}{row.Seq} · {Label(row)}\n{indent}{text}";
                if (GUILayout.Toggle(row.key == selectedKey, label, "Button", GUILayout.MinHeight(42)) && selectedKey != row.key)
                { selectedKey = row.key; GUI.FocusControl(null); }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(lines.Count == 0))
            {
                if (GUILayout.Button("＋ 대사 추가"))
                {
                    Change("대사 추가", () =>
                    {
                        var last = lines.LastOrDefault();
                        DialogueRow added = document.AddTalk(selectedEvent);
                        // Appending after an ordinary ending extends that ending deliberately.
                        if (last != null && DialogueDocument.Kind(last) == "TALK" && last.NextID.Equals("END", StringComparison.OrdinalIgnoreCase)) last.NextID = added.Seq;
                        selectedKey = added.key;
                    });
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("＋ 선택 질문과 선택지 2개"))
                {
                    Change("선택 질문 추가", () =>
                    {
                        var last = lines.LastOrDefault();
                        var choice = document.AddChoice(selectedEvent);
                        if (last != null && DialogueDocument.Kind(last) == "TALK" && last.NextID.Equals("END", StringComparison.OrdinalIgnoreCase)) last.NextID = choice.Seq;
                        selectedKey = choice.key;
                    });
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.HelpBox("선택 질문과 아래 선택지들은 한 묶음으로 이동합니다. 이동 후 '다음 진행'도 확인하세요.", MessageType.None);
        }
    }

    private void DrawDetail()
    {
        DialogueRow row = lines.FirstOrDefault(r => r.key == selectedKey);
        if (row == null) { EditorGUILayout.HelpBox("이벤트 또는 단계를 선택하세요.", MessageType.Info); return; }
        GUILayout.Label("3. " + Label(row) + " 편집", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("단계 " + row.Seq, EditorStyles.miniLabel);
            if (GUILayout.Button("위로")) { Change("단계 이동", () => document.MoveBlock(row, -1)); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("아래로")) { Change("단계 이동", () => document.MoveBlock(row, 1)); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("복제")) { Change("단계 복제", () => selectedKey = document.Duplicate(row).key); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("삭제") && EditorUtility.DisplayDialog("단계 삭제", "선택 질문은 하위 선택지도 함께 삭제합니다. 이 단계를 참조하는 연결은 직접 수정해야 합니다. 되돌리기로 복구할 수 있습니다.", "삭제", "취소"))
            { Change("단계 삭제", () => document.Delete(row)); GUIUtility.ExitGUI(); }
        }
        if (DialogueDocument.Kind(row) == "CHOICE" && GUILayout.Button("이 질문에 선택지 추가"))
        { Change("선택지 추가", () => selectedKey = document.AddBranch(row).key); GUIUtility.ExitGUI(); }

        assetSearch = EditorGUILayout.TextField(new GUIContent("소재 검색", "캐릭터·배경·아이템의 이름 또는 ID로 검색합니다."), assetSearch);
        Undo.RecordObject(document, "대화 내용 편집");
        EditorGUI.BeginChangeCheck();
        string kind = DialogueDocument.Kind(row);
        if (kind != "CHOICE" && kind != "BRANCH")
        {
            int current = Array.IndexOf(Types, kind);
            int picked = EditorGUILayout.Popup("단계 종류", current, TypeLabels);
            if (picked != current && picked >= 0) row.Type = Types[picked];
        }
        row.CharacterID = Pick("등장인물", row.CharacterID, catalog.characters, "없음 / 내레이션");
        row.BackgroundID = Pick("배경", row.BackgroundID, catalog.backgrounds, "이전 배경 유지", true);
        row.Name = EditorGUILayout.TextField(new GUIContent("표시 이름", "비워두면 등장인물의 등록된 이름을 표시합니다."), row.Name);
        GUILayout.Label(kind == "BRANCH" ? "선택지 문장" : "대사 내용", EditorStyles.boldLabel);
        row.Text = EditorGUILayout.TextArea(row.Text, new GUIStyle(EditorStyles.textArea) { wordWrap = true }, GUILayout.MinHeight(110));
        if (kind == "CHOICE") EditorGUILayout.HelpBox("아래에 붙어 있는 선택지들이 게임에서 버튼으로 표시됩니다. 선택지마다 다음 진행을 설정하세요.", MessageType.None);
        else row.NextID = PickNext(row.NextID, kind == "BRANCH");
        if (kind == "BRANCH")
        {
            GUILayout.Label("이 선택지가 보이는 조건 (모두 충족)", EditorStyles.boldLabel);
            row.Condition = DrawCommands(row.Condition, true);
            GUILayout.Label("선택했을 때 실행할 효과", EditorStyles.boldLabel);
            row.Action = DrawCommands(row.Action, false);
        }
        advanced = EditorGUILayout.Foldout(advanced, "고급: 기존 ID·명령 원문", true);
        if (advanced)
        {
            EditorGUILayout.HelpBox("기존 데이터를 복구할 때 사용하세요. 단계 ID를 바꾸면 연결을 직접 수정해야 합니다.", MessageType.Warning);
            row.Seq = EditorGUILayout.DelayedTextField("단계 ID", row.Seq);
            row.CharacterID = EditorGUILayout.TextField("등장인물 ID", row.CharacterID);
            row.BackgroundID = EditorGUILayout.TextField("배경 ID", row.BackgroundID);
            row.Condition = EditorGUILayout.TextField("조건 원문", row.Condition);
            row.Action = EditorGUILayout.TextField("효과 원문", row.Action);
            row.NextID = EditorGUILayout.TextField("다음 진행 원문", row.NextID);
        }
        if (EditorGUI.EndChangeCheck())
        {
            selectedKey = row.key;
            EditorUtility.SetDirty(document); Refresh(); PersistDraft();
        }
        foreach (DialogueIssue issue in issues.Where(i => i.row == row))
            EditorGUILayout.HelpBox(issue.message, issue.error ? MessageType.Error : MessageType.Warning);
        if (!string.IsNullOrEmpty(row.CharacterID) && !catalog.characters.Any(e => e.id == row.CharacterID))
            EditorGUILayout.HelpBox("등록된 등장인물에서 이 ID를 찾지 못했습니다. 기존 값은 보존합니다.", MessageType.Warning);
        if (!string.IsNullOrEmpty(row.BackgroundID) && !new[] { "NONE", "CLEAR" }.Contains(row.BackgroundID.ToUpperInvariant()) && !catalog.backgrounds.Any(e => e.id == row.BackgroundID))
            EditorGUILayout.HelpBox("등록된 배경에서 이 ID를 찾지 못했습니다. 기존 값은 보존합니다.", MessageType.Warning);
    }

    private string Pick(string label, string current, List<DialogueAssetCatalog.Entry> entries, string empty, bool background = false)
    {
        var ids = new List<string> { "" }; var labels = new List<string> { empty };
        if (background) { ids.Add("CLEAR"); labels.Add("배경 지우기"); }
        foreach (var entry in entries)
            if (entry.id == current || entry.id.Contains(assetSearch, StringComparison.OrdinalIgnoreCase) || entry.label.Contains(assetSearch, StringComparison.OrdinalIgnoreCase))
            { ids.Add(entry.id); labels.Add(entry.label + "  (" + entry.id + ")"); }
        if (!ids.Contains(current)) { ids.Add(current); labels.Add("현재 값: " + current); }
        int index = ids.IndexOf(current);
        int selected = EditorGUILayout.Popup(label, index, labels.ToArray());
        return selected >= 0 && selected < ids.Count ? ids[selected] : current;
    }

    private string PickNext(string current, bool branch)
    {
        var ids = new List<string> { "END", "" };
        var labels = new List<string> { "대화 종료", branch ? "대화 종료 (빈 값)" : "목록의 다음 단계" };
        foreach (DialogueRow target in lines.Where(r => DialogueDocument.Kind(r) != "BRANCH"))
        {
            ids.Add(target.Seq);
            string text = target.Text.Replace('\n', ' ');
            labels.Add(target.Seq + " · " + Label(target) + " · " + (text.Length > 28 ? text.Substring(0, 28) + "…" : text));
        }
        if (!ids.Contains(current)) { ids.Add(current); labels.Add("연결 확인 필요: " + current); }
        int selected = EditorGUILayout.Popup("다음 진행", ids.IndexOf(current), labels.ToArray());
        return selected >= 0 && selected < ids.Count ? ids[selected] : current;
    }

    private string DrawCommands(string text, bool condition)
    {
        var commands = DialogueDocument.None(text) ? new List<string>() : text.Split(';').ToList();
        string[] labels = condition ? new[] { "기록이 켜져 있음", "돈이 일정 금액 이상", "아이템을 가지고 있음" }
            : new[] { "기록 켜기/끄기", "돈 소모", "아이템 소모" };
        bool changed = false;
        for (int i = 0; i < commands.Count; i++)
        {
            string raw = commands[i]; string[] p = raw.Trim().Split(':');
            string op = p[0].ToUpperInvariant(); string value = p.Length > 1 ? p[1] : "";
            int mode = op == (condition ? "FLAG" : "SET_FLAG") ? 0 : op == (condition ? "HASITEM" : "REMOVE") ? (value.StartsWith("Gold_", StringComparison.Ordinal) ? 1 : 2) : -1;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool typeChanged = false;
                using (new EditorGUILayout.HorizontalScope())
                {
                    int nextMode = EditorGUILayout.Popup(mode, labels);
                    if (GUILayout.Button("제거", GUILayout.Width(45))) { commands.RemoveAt(i); changed = true; GUI.changed = true; break; }
                    if (nextMode != mode && nextMode >= 0)
                    { mode = nextMode; value = mode == 0 ? "새_기록" : mode == 1 ? "Gold_100" : ""; p = new[] { "", value, "true" }; typeChanged = true; changed = true; GUI.changed = true; }
                }
                EditorGUI.BeginChangeCheck();
                string encoded = raw;
                if (mode == 0)
                {
                    value = EditorGUILayout.TextField("기록 이름", value);
                    bool state = p.Length > 2 && p[2].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (!condition) state = EditorGUILayout.Toggle("켜짐", state);
                    encoded = condition ? "FLAG:" + value : "SET_FLAG:" + value + ":" + state.ToString().ToLowerInvariant();
                }
                else if (mode == 1)
                {
                    int.TryParse(value.StartsWith("Gold_", StringComparison.Ordinal) ? value.Substring(5) : "100", out int amount);
                    amount = EditorGUILayout.IntField("금액", amount);
                    encoded = (condition ? "HASITEM:Gold_" : "REMOVE:Gold_") + amount;
                }
                else if (mode == 2)
                {
                    value = Pick("아이템", value, catalog.items, "아이템 선택");
                    int count = 1; if (p.Length > 2) int.TryParse(p[2], out count);
                    if (!condition) count = EditorGUILayout.IntField("수량", count);
                    encoded = (condition ? "HASITEM:" : "REMOVE:") + value + (condition ? "" : ":" + count);
                }
                else encoded = EditorGUILayout.TextField("기존 명령", raw);
                bool fieldChanged = EditorGUI.EndChangeCheck();
                if (GUILayout.Button("설정 적용")) { fieldChanged = true; GUI.changed = true; }
                // Never canonicalize a legacy token just by displaying it.
                if (fieldChanged || typeChanged) { commands[i] = encoded; changed = true; GUI.changed = true; }
            }
        }
        if (GUILayout.Button(condition ? "＋ 조건 추가" : "＋ 효과 추가"))
        { commands.Add(condition ? "FLAG:새_기록" : "SET_FLAG:새_기록:true"); changed = true; GUI.changed = true; }
        return changed ? string.Join(";", commands) : text;
    }

    private void DrawPreview()
    {
        GUILayout.Label("4. 흐름 미리보기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("게임을 실행하지 않아도 확인할 수 있습니다. 조건은 평가하지 않고 모든 선택지를 표시합니다. 돈·아이템·기록·동료 상태는 바꾸지 않습니다.", MessageType.None);
        if (GUILayout.Button("처음부터 보기")) { previewIndex = lines.Count > 0 ? 0 : -1; previewNotice = ""; previewBackground = ""; }
        if (previewIndex < 0 || previewIndex >= lines.Count) { GUILayout.Label("시작 버튼을 눌러 보세요."); DrawEventIssues(); return; }
        DialogueRow row = lines[previewIndex];
        GUILayout.Label("단계 " + row.Seq + " · " + Label(row), EditorStyles.miniLabel);
        var character = catalog.characters.FirstOrDefault(e => e.id == row.CharacterID);
        if (!string.IsNullOrEmpty(row.BackgroundID)) previewBackground = row.BackgroundID;
        var background = catalog.backgrounds.FirstOrDefault(e => e.id == previewBackground);
        if (background?.sprite != null) AssetPreview.DrawPreview(background.sprite, 95);
        if (character?.sprite != null) AssetPreview.DrawPreview(character.sprite, 70);
        GUILayout.Label(string.IsNullOrEmpty(row.Name) ? character?.label ?? row.CharacterID : row.Name, EditorStyles.boldLabel);
        GUILayout.Label(row.Text, new GUIStyle(EditorStyles.helpBox) { wordWrap = true, fontSize = 14 }, GUILayout.MinHeight(90));
        if (DialogueDocument.Kind(row) == "JOIN" || DialogueDocument.Kind(row) == "LEAVE") GUILayout.Label("동료 상태 변경은 실행하지 않습니다.", EditorStyles.wordWrappedMiniLabel);
        if (DialogueDocument.Kind(row) == "CHOICE")
        {
            var branches = document.Block(row).Skip(1).ToList();
            if (branches.Count == 0) EditorGUILayout.HelpBox("선택지가 없습니다.", MessageType.Error);
            foreach (var branch in branches)
            {
                if (!DialogueDocument.None(branch.Condition)) GUILayout.Label("조건: " + branch.Condition, EditorStyles.wordWrappedMiniLabel);
                if (!DialogueDocument.None(branch.Action)) GUILayout.Label("효과: " + branch.Action, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(branch.Text, new GUIStyle("Button") { wordWrap = true })) Follow(branch, true);
            }
        }
        else if (GUILayout.Button("다음")) Follow(row, false);
        if (!string.IsNullOrEmpty(previewNotice)) EditorGUILayout.HelpBox(previewNotice, MessageType.Info);
        DrawEventIssues();
    }

    private void DrawEventIssues()
    {
        GUILayout.Space(15);
        GUILayout.Label("이 이벤트의 확인 항목", EditorStyles.boldLabel);
        foreach (var issue in issues.Where(i => i.row.EventID == selectedEvent))
            if (GUILayout.Button(issue.row.Seq + " · " + issue.message, new GUIStyle(EditorStyles.helpBox) { wordWrap = true })) selectedKey = issue.row.key;
    }

    private void Follow(DialogueRow row, bool branch)
    {
        if (row.NextID.Equals("END", StringComparison.OrdinalIgnoreCase) || (branch && string.IsNullOrEmpty(row.NextID)))
        { previewIndex = -1; notice = "미리보기: 대화가 종료되었습니다."; return; }
        int next = string.IsNullOrEmpty(row.NextID) ? lines.IndexOf(row) + 1 : lines.FindIndex(r => r.Seq == row.NextID);
        if (string.IsNullOrEmpty(row.NextID) && next == lines.Count)
        { previewIndex = -1; notice = "미리보기: 대화가 종료되었습니다."; return; }
        if (next < 0 || next >= lines.Count) { previewNotice = "다음 단계를 찾을 수 없어 진행을 멈췄습니다."; return; }
        previewIndex = next; previewNotice = "";
    }

    private void ExportDraft()
    {
        string path = EditorUtility.SaveFilePanel("현재 초안 내보내기", "", "EventScripts.draft", "csv");
        if (string.IsNullOrEmpty(path)) return;
        if (Path.GetFullPath(path).Equals(Path.GetFullPath(DialogueFileStore.CsvPath), StringComparison.OrdinalIgnoreCase)) { notice = "원본 CSV는 저장 버튼으로 저장하세요. 초안은 다른 파일 이름을 선택하세요."; return; }
        try { File.WriteAllText(path, document.Encode(), new UTF8Encoding(true)); notice = "초안을 내보냈습니다. 검증 전 초안이므로 원본 CSV로 바로 교체하지 마세요."; }
        catch (Exception ex) { notice = "초안을 내보내지 못했습니다. " + ex.Message; }
    }

    private static string Label(DialogueRow row)
    {
        switch (DialogueDocument.Kind(row))
        {
            case "TALK": return "대사";
            case "CHOICE": return "선택 질문";
            case "BRANCH": return "선택지";
            case "JOIN": return "동료 합류";
            case "LEAVE": return "동료 이탈";
            default: return "알 수 없는 종류";
        }
    }

    private static class AssetPreview
    {
        public static void DrawPreview(Sprite sprite, float height)
        {
            Rect rect = GUILayoutUtility.GetRect(80, height, GUILayout.ExpandWidth(true));
            Texture texture = UnityEditor.AssetPreview.GetAssetPreview(sprite)
                ?? UnityEditor.AssetPreview.GetMiniThumbnail(sprite);
            if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        }
    }
}
