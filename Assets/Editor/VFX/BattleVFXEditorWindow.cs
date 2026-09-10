using System;
using System.Collections.Generic;
using System.Linq;
using RPGProject.Feature.Battle;

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

namespace RPGProject.Editor.VFX
{
    public sealed class BattleVFXEditorWindow : EditorWindow
    {
        [SerializeField] private BattleVFXDocument document;
        [SerializeField] private DefaultAsset folder;
        [SerializeField] private string search = "";
        [SerializeField] private bool loop = true;
        [SerializeField] private float speed = 1, zoom = 1, referencePixelsPerUnit = 100;
        [SerializeField] private Color background = new Color(0.12f, 0.13f, 0.16f, 1);
        private readonly List<GameObject> prefabs = new List<GameObject>();
        private SerializedObject serializedDocument;
        private ReorderableList frameList;
        private BattleVFXTimeline timeline;
        private BattleVFXPreview preview;
        private Vector2 libraryScroll, frameScroll;
        private string notice = "";
        private double nextRepaint;
        private bool refreshPending;
        private static readonly string[] DefaultFolders = { "Assets/Prefab/VFX", "Assets/Prefab/Battle/VFX" };

        [MenuItem("Tools/Battle VFX Editor")]
        public static void Open() => GetWindow<BattleVFXEditorWindow>("마법 효과 편집기").Show();

        [MenuItem("Assets/마법 효과 편집기로 열기", true)]
        private static bool CanOpenSelected() => Selection.activeObject is GameObject selected &&
            AssetDatabase.GetAssetPath(selected).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        [MenuItem("Assets/마법 효과 편집기로 열기")]
        private static void OpenSelected()
        {
            var window = GetWindow<BattleVFXEditorWindow>("마법 효과 편집기");
            window.Show();
            window.SelectPrefab(Selection.activeObject as GameObject);
        }

        private void OnEnable()
        {
            minSize = new Vector2(1000, 760);
            saveChangesMessage = "마법 효과의 수정 내용을 프리팹에 저장할까요?";
            timeline = new BattleVFXTimeline { Loop = loop, Speed = speed };
            preview = new BattleVFXPreview();
            if (folder == null)
            {
                string path = DefaultFolders.FirstOrDefault(AssetDatabase.IsValidFolder);
                if (path != null) folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            }
            RefreshLibrary();
            if (document != null) BindDocument();
            EditorApplication.update += Tick;
            EditorApplication.projectChanged += ProjectChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Undo.undoRedoPerformed += UndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.projectChanged -= ProjectChanged;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            Undo.undoRedoPerformed -= UndoRedo;
            preview?.Dispose();
            preview = null;
            serializedDocument?.Dispose();
            serializedDocument = null;
            frameList = null;
            // Keep the serialized draft reference across script/domain reloads.
        }

        private void OnDestroy()
        {
            if (document == null) return;
            Undo.ClearUndo(document);
            DestroyImmediate(document);
        }

        private void ProjectChanged() { refreshPending = true; Repaint(); }

        private void PlayModeChanged(PlayModeStateChange state)
        {
            timeline?.SetPlaying(false, EditorApplication.timeSinceStartup);
            if (state == PlayModeStateChange.ExitingEditMode) preview?.Dispose();
            if (state == PlayModeStateChange.EnteredEditMode && document != null) BindDocument();
            Repaint();
        }

        private void UndoRedo()
        {
            if (document == null || serializedDocument == null) return;
            serializedDocument.Update();
            Changed();
        }

        private void Tick()
        {
            if (refreshPending && !EditorApplication.isCompiling)
            {
                refreshPending = false;
                RefreshLibrary();
                // Asset updates must not replace a dirty draft.
            }
            if (timeline == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            double now = EditorApplication.timeSinceStartup;
            bool changed = timeline.Tick(now);
            if (changed || (timeline.Playing && now >= nextRepaint))
            {
                nextRepaint = now + 1.0 / 60;
                Repaint();
            }
        }

        private void RefreshLibrary()
        {
            prefabs.Clear();
            string path = AssetDatabase.GetAssetPath(folder);
            if (!AssetDatabase.IsValidFolder(path)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.GetComponentsInChildren<BattleVFXAnimator>(true).Length > 0) prefabs.Add(prefab);
            }
            prefabs.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
        }

        private GameObject Source => document == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(document.AssetPath);

        private bool ResolveUnsaved()
        {
            if (document == null || !document.HasChanges) return true;
            int choice = EditorUtility.DisplayDialogComplex("저장하지 않은 변경", "현재 마법 효과의 수정 내용을 어떻게 할까요?", "저장", "취소", "버리기");
            if (choice == 1) return false;
            return choice == 2 || TrySave();
        }

        private void SelectPrefab(GameObject prefab)
        {
            if (prefab == Source || prefab == null) return;
            if (!AssetDatabase.GetAssetPath(prefab).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            { notice = "씬 오브젝트 대신 프로젝트의 .prefab 에셋을 선택해 주세요."; return; }
            try { BattleVFXDocument.FindAnimator(prefab); }
            catch (Exception error) { notice = error.Message; return; }
            if (!ResolveUnsaved()) return;
            BattleVFXDocument next = CreateInstance<BattleVFXDocument>();
            next.hideFlags = HideFlags.HideAndDontSave;
            try { next.Load(prefab); }
            catch (Exception error) { DestroyImmediate(next); notice = error.Message; return; }
            serializedDocument?.Dispose();
            serializedDocument = null;
            if (document != null) { Undo.ClearUndo(document); DestroyImmediate(document); }
            document = next;
            notice = "";
            frameScroll = Vector2.zero;
            BindDocument();
            timeline.SetPlaying(true, EditorApplication.timeSinceStartup);
        }

        private void BindDocument()
        {
            serializedDocument?.Dispose();
            serializedDocument = new SerializedObject(document);
            frameList = new ReorderableList(serializedDocument, serializedDocument.FindProperty("frames"), true, true, true, true);
            frameList.elementHeight = 50;
            frameList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "프레임 순서                       스프라이트                              유지 시간 (초)");
            frameList.drawElementCallback = DrawFrame;
            frameList.onSelectCallback = list =>
            {
                if (!timeline.Playing) timeline.SeekFrame(list.index, EditorApplication.timeSinceStartup);
                Repaint();
            };
            frameList.onAddCallback = list =>
            {
                var frames = list.serializedProperty;
                int index = frames.arraySize++;
                var frame = frames.GetArrayElementAtIndex(index);
                frame.FindPropertyRelative("frameSprite").objectReferenceValue = null;
                frame.FindPropertyRelative("duration").floatValue = document.defaultDuration;
                list.index = index;
                ApplyProperties();
                timeline.SeekFrame(index, EditorApplication.timeSinceStartup);
            };
            frameList.onRemoveCallback = list =>
            {
                if (list.index < 0 || list.index >= list.serializedProperty.arraySize) return;
                list.serializedProperty.DeleteArrayElementAtIndex(list.index);
                list.index = Mathf.Clamp(list.index, 0, list.serializedProperty.arraySize - 1);
                ApplyProperties();
            };
            frameList.onReorderCallback = list => ApplyProperties();
            ConfigureTimeline(true);
            try
            {
                var animator = BattleVFXDocument.FindAnimator(Source);
                preview.Bind(animator.GetComponent<Image>());
                preview.Configure(document.frames, document.useNativeSize, referencePixelsPerUnit);
            }
            catch (Exception error) { preview.Dispose(); notice = error.Message; }
            hasUnsavedChanges = document.HasChanges;
            Repaint();
        }

        private void ConfigureTimeline(bool reset)
        {
            timeline.Configure(document.Durations(), EditorApplication.timeSinceStartup, reset);
            timeline.Loop = loop;
            timeline.Speed = speed;
        }

        private void Changed()
        {
            hasUnsavedChanges = document.HasChanges;
            ConfigureTimeline(false);
            if (document.Validate() != null) timeline.SetPlaying(false, EditorApplication.timeSinceStartup);
            preview?.Configure(document.frames, document.useNativeSize, referencePixelsPerUnit);
            Repaint();
        }

        private void ApplyProperties()
        {
            if (serializedDocument.ApplyModifiedProperties()) Changed();
        }

        private void Edit(string label, Action action)
        {
            ApplyProperties();
            Undo.RecordObject(document, label);
            action();
            EditorUtility.SetDirty(document);
            serializedDocument.Update();
            Changed();
        }

        private bool TrySave()
        {
            try
            {
                ApplyProperties();
                document.Save();
                hasUnsavedChanges = false;
                notice = "저장했습니다: " + document.AssetPath;
                return true;
            }
            catch (Exception error) { notice = error.Message; Repaint(); return false; }
        }

        public override void SaveChanges()
        {
            if (document != null && !TrySave()) throw new InvalidOperationException(notice);
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            if (document != null && Source != null)
            {
                try { Undo.ClearUndo(document); document.Load(Source); BindDocument(); }
                catch (Exception error) { notice = error.Message; }
            }
            base.DiscardChanges();
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("플레이 모드를 종료하면 프리팹 편집과 미리보기를 계속할 수 있습니다.", MessageType.Info); return; }
            HandleShortcuts();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var nextFolder = (DefaultAsset)EditorGUILayout.ObjectField(folder, typeof(DefaultAsset), false, GUILayout.Width(240));
                if (EditorGUI.EndChangeCheck())
                {
                    if (nextFolder == null || AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(nextFolder))) { folder = nextFolder; RefreshLibrary(); }
                    else notice = "프리팹이 들어 있는 폴더를 선택해 주세요.";
                }
                search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(100));
                if (GUILayout.Button("목록 새로고침", EditorStyles.toolbarButton, GUILayout.Width(100))) RefreshLibrary();
                GUILayout.Label(prefabs.Count + "개", GUILayout.Width(50));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLibrary();
                using (new EditorGUILayout.VerticalScope())
                {
                    var chosen = (GameObject)EditorGUILayout.ObjectField("효과 프리팹", Source, typeof(GameObject), false);
                    if (chosen != null && chosen != Source) SelectPrefab(chosen);
                    if (document == null || serializedDocument == null)
                    { EditorGUILayout.HelpBox("왼쪽 목록에서 효과를 선택하거나 프리팹을 위 칸에 넣어 주세요.", MessageType.Info); }
                    else DrawDocument();
                    if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, MessageType.Info);
                }
            }
        }

        private void DrawLibrary()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(225)))
            {
                libraryScroll = EditorGUILayout.BeginScrollView(libraryScroll);
                GameObject source = Source;
                foreach (var prefab in prefabs)
                {
                    if (prefab == null || (!string.IsNullOrEmpty(search) && prefab.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                    bool selected = prefab == source;
                    if (GUILayout.Toggle(selected, prefab.name, "Button", GUILayout.Height(25)) && !selected) SelectPrefab(prefab);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDocument()
        {
            serializedDocument.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(hasUnsavedChanges ? "● 저장하지 않은 변경" : "저장된 상태", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(document.Validate() != null))
                    if (GUILayout.Button("프리팹 저장", GUILayout.Width(110))) TrySave();
                if (GUILayout.Button("원본 다시 읽기", GUILayout.Width(115)))
                {
                    if (!document.HasChanges || EditorUtility.DisplayDialog("원본 다시 읽기", "저장하지 않은 변경을 버리고 원본을 읽을까요?", "다시 읽기", "취소"))
                    {
                        try { Undo.ClearUndo(document); document.Load(Source); BindDocument(); notice = "원본을 다시 읽었습니다."; }
                        catch (Exception e) { notice = e.Message; }
                    }
                }
                if (GUILayout.Button("에셋 찾기", GUILayout.Width(75))) EditorGUIUtility.PingObject(Source);
            }
            DrawPlayback();
            EditorGUILayout.PropertyField(serializedDocument.FindProperty("defaultDuration"), new GUIContent("기본 시간 (초)", "새 프레임 추가·images에서 생성할 때 사용합니다. 기존 프레임에는 아래 버튼으로 적용하세요."));
            EditorGUILayout.PropertyField(serializedDocument.FindProperty("useNativeSize"), new GUIContent("스프라이트 원본 크기 사용"));
            ApplyProperties();
            EditorGUILayout.LabelField("기본 시간 변경은 기존 프레임의 시간을 바꾸지 않습니다.", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!BattleVFXTimeline.IsPositiveFinite(document.defaultDuration)))
                {
                    if (GUILayout.Button("기본 시간을 전체 적용")) Edit("VFX 전체 프레임 시간", document.ApplyDefaultToAll);
                    if (GUILayout.Button("images에서 다시 생성")) GenerateFromImages();
                }
                using (new EditorGUI.DisabledScope(frameList.index < 0 || frameList.index >= document.frames.Length))
                    if (GUILayout.Button("선택 프레임 복제")) DuplicateFrame();
                if (GUILayout.Button("순서 뒤집기")) Edit("VFX 역순", () => Array.Reverse(document.frames));
            }
            Rect drop = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            GUI.Box(drop, "여기에 Sprite 여러 개 또는 스프라이트 시트를 놓으면 이름 순서대로 추가됩니다.");
            HandleDrop(drop);
            string error = document.Validate();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            frameScroll = EditorGUILayout.BeginScrollView(frameScroll, GUILayout.MinHeight(110));
            frameList.DoLayoutList();
            EditorGUILayout.EndScrollView();
            ApplyProperties();
        }

        private void DrawPlayback()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!timeline.Valid || document.Validate() != null))
                    if (GUILayout.Button(timeline.Playing ? "일시정지" : "재생", GUILayout.Width(85))) timeline.SetPlaying(!timeline.Playing, EditorApplication.timeSinceStartup);
                if (GUILayout.Button("처음", GUILayout.Width(50))) timeline.SeekFrame(0, EditorApplication.timeSinceStartup);
                if (GUILayout.Button("이전", GUILayout.Width(50))) timeline.SeekFrame(timeline.Frame - 1, EditorApplication.timeSinceStartup);
                if (GUILayout.Button("다음", GUILayout.Width(50))) timeline.SeekFrame(timeline.Frame + 1, EditorApplication.timeSinceStartup);
                loop = GUILayout.Toggle(loop, "반복", GUILayout.Width(55));
                timeline.Loop = loop;
                GUILayout.Label("미리보기 속도", GUILayout.Width(85));
                speed = EditorGUILayout.Slider(speed, 0.1f, 3f, GUILayout.MinWidth(130));
                timeline.Speed = speed;
            }
            Rect area = GUILayoutUtility.GetRect(0, 235, GUILayout.ExpandWidth(true));
            int index = timeline.Frame;
            Sprite sprite = index >= 0 && index < document.frames.Length ? document.frames[index].frameSprite : null;
            try { preview.Draw(area, sprite, document.useNativeSize, !timeline.Finished, zoom, background); }
            catch (Exception error) { preview.Dispose(); notice = "미리보기 오류: " + error.Message + " — 원본 다시 읽기로 재시도할 수 있습니다."; }
            if (timeline.Finished) GUI.Label(area, "재생 완료", EditorStyles.centeredGreyMiniLabel);
            else if (sprite == null) GUI.Label(area, "스프라이트를 지정해 주세요.", EditorStyles.centeredGreyMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("확대", GUILayout.Width(32));
                zoom = EditorGUILayout.Slider(zoom, 0.1f, 4, GUILayout.MinWidth(120));
                background = EditorGUILayout.ColorField(GUIContent.none, background, false, false, false, GUILayout.Width(60));
                GUILayout.Label("Canvas PPU", GUILayout.Width(80));
                EditorGUI.BeginChangeCheck();
                referencePixelsPerUnit = EditorGUILayout.FloatField(referencePixelsPerUnit, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck())
                {
                    if (!BattleVFXTimeline.IsPositiveFinite(referencePixelsPerUnit)) referencePixelsPerUnit = 100;
                    preview.Configure(document.frames, document.useNativeSize, referencePixelsPerUnit);
                }
                if (GUILayout.Button("화면 맞춤", GUILayout.Width(75))) zoom = 1;
            }
            using (new EditorGUI.DisabledScope(!timeline.Valid))
            {
                EditorGUI.BeginChangeCheck();
                float time = EditorGUILayout.Slider("재생 위치 (초)", (float)timeline.Position, 0, (float)timeline.Duration);
                if (EditorGUI.EndChangeCheck()) timeline.SeekTime(time, EditorApplication.timeSinceStartup);
            }
            EditorGUILayout.LabelField($"프레임 {Math.Max(0, index + 1)} / {document.frames.Length}     전체 {timeline.Duration:0.###}초", EditorStyles.miniLabel);
        }

        private void DrawFrame(Rect rect, int index, bool active, bool focused)
        {
            if (index < 0 || index >= frameList.serializedProperty.arraySize) return;
            if (index == timeline.Frame) EditorGUI.DrawRect(rect, new Color(0.2f, 0.6f, 0.9f, 0.13f));
            var frame = frameList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 4;
            if (GUI.Button(new Rect(rect.x, rect.y, 58, 20), (index + 1).ToString("D3")))
            { timeline.SeekFrame(index, EditorApplication.timeSinceStartup); Repaint(); }
            EditorGUI.LabelField(new Rect(rect.x, rect.y + 21, 62, 18), timeline.StartOf(index).ToString("0.###") + "s", EditorStyles.miniLabel);
            EditorGUI.PropertyField(new Rect(rect.x + 65, rect.y, Mathf.Max(80, rect.width - 170), 40), frame.FindPropertyRelative("frameSprite"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(rect.xMax - 95, rect.y + 8, 90, 20), frame.FindPropertyRelative("duration"), GUIContent.none);
        }

        private void DuplicateFrame()
        {
            int index = frameList.index;
            Edit("VFX 프레임 복제", () =>
            {
                var frames = document.frames.ToList();
                frames.Insert(index + 1, frames[index]);
                document.frames = frames.ToArray();
            });
            frameList.index = index + 1;
        }

        private void GenerateFromImages()
        {
            Sprite[] sprites;
            try { sprites = BattleVFXDocument.FindAnimator(Source).images; }
            catch (Exception error) { notice = error.Message; return; }
            if (sprites == null || sprites.Length == 0) { notice = "원본의 images 배열이 비어 있습니다."; return; }
            if (document.frames.Length > 0 && !EditorUtility.DisplayDialog("프레임 다시 생성", "현재 프레임 순서와 개별 시간을 images와 기본 시간으로 교체할까요?", "생성", "취소")) return;
            Edit("VFX images에서 생성", () =>
            {
                document.frames = sprites.Select(sprite => new BattleVFXAnimator.FrameData { frameSprite = sprite, duration = document.defaultDuration }).ToArray();
            });
        }

        private void HandleDrop(Rect rect)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition) || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var sprites = new List<Sprite>();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is Sprite sprite) sprites.Add(sprite);
                    else if (obj is Texture2D) sprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(obj)).OfType<Sprite>());
                }
                sprites = sprites.Distinct().ToList();
                sprites.Sort((a, b) =>
                {
                    int comparison = EditorUtility.NaturalCompare(a.name, b.name);
                    return comparison != 0 ? comparison : string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b));
                });
                if (sprites.Count > 0) Edit("VFX 스프라이트 추가", () => document.Append(sprites.ToArray()));
                else notice = "Sprite 또는 Sprite로 임포트된 텍스처를 놓아 주세요.";
            }
            e.Use();
        }

        private void HandleShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || document == null) return;
            if ((e.control || e.command) && e.keyCode == KeyCode.S) { TrySave(); e.Use(); }
            else if (e.keyCode == KeyCode.Space && !EditorGUIUtility.editingTextField && document.Validate() == null)
            { timeline.SetPlaying(!timeline.Playing, EditorApplication.timeSinceStartup); e.Use(); }
        }
    }
}
