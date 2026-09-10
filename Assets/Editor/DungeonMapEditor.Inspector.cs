using System;
using System.Collections.Generic;
using System.Linq;
using RPGProject.Feature.Exploration;
using UnityEditor;
using UnityEngine;

public partial class DungeonMapEditor
{
    [SerializeField] private bool mapSettingsOpen = true, resizeOpen, issuesOpen = true;
    [SerializeField] private int resizeWidth = 10, resizeHeight = 10, resizeAnchor;
    [SerializeField] private bool interactionObjectPalette;
    private static readonly string[] CellNames = { "바닥 구멍", "통로", "천장 구멍", "무기점", "방어구점", "잡화점", "회복 지점", "터미널", "사무소", "내려가는 계단", "올라가는 계단", "엘리베이터", "입구", "낙하 함정", "독 함정" };
    private static readonly string[] EntranceNames = { "던전 / 월드 이동", "상점", "엘리베이터", "터미널", "사무소", "필드 이동 메뉴", "랜덤 미로" };
    internal static string CellTypeName(int value) => value >= -1 && value + 1 < CellNames.Length ? CellNames[value + 1] : $"알 수 없는 종류 ({value})";

    private void DrawInspector()
    {
        DrawMapSettings();
        EditorGUILayout.Space(8);
        showAdvanced = EditorGUILayout.ToggleLeft("고급 설정과 내부 ID 표시", showAdvanced);
        if (Active == null) EditorGUILayout.HelpBox("맵에서 타일을 선택하면 속성을 편집할 수 있습니다.", MessageType.Info);
        else
        {
            GUILayout.Label(selection.Count == 1 ? $"타일 ({Active.x}, {Active.y})" : $"{selection.Count}개 타일 일괄 편집", EditorStyles.boldLabel);
            if (selection.Count > 1) EditorGUILayout.HelpBox("— 표시는 값이 서로 다름을 뜻합니다. 변경한 항목만 선택된 모든 타일에 적용됩니다.", MessageType.None);
            inspectorTab = GUILayout.Toolbar(inspectorTab, new[] { "타일", "물체", "입구", "이벤트" });
            GUILayout.Space(6);
            switch (inspectorTab)
            {
                case 0: DrawTileProperties(); break;
                case 1: DrawObjectProperties(); break;
                case 2: DrawEntranceProperties(); break;
                case 3: DrawEventProperties(); break;
            }
        }
        GUILayout.Space(10);
        DrawRegistration();
        DrawIssues();
    }
    private void DrawMapSettings()
    {
        mapSettingsOpen = EditorGUILayout.Foldout(mapSettingsOpen, "맵 설정", true);
        if (!mapSettingsOpen) return;
        MapText("맵 ID", Map.mapID, value => Map.mapID = value);
        MapText("상위 지역 ID", Map.locationID, value => Map.locationID = value);
        var themes = index.themes.ToArray();
        int selectedTheme = Array.IndexOf(themes, Theme) + 1;
        EditorGUI.BeginChangeCheck();
        int next = EditorGUILayout.Popup("테마", selectedTheme, new[] { "선택하세요 / 누락됨" }.Concat(themes.Select(t => t.name + " (" + t.themeID + ")")).ToArray());
        if (EditorGUI.EndChangeCheck()) Edit("맵 테마 변경", m => m.themeID = next > 0 ? themes[next - 1].themeID : "");
        if (Theme == null && !string.IsNullOrEmpty(Map.themeID)) GUILayout.Label("현재 테마 ID: " + Map.themeID, EditorStyles.wordWrappedMiniLabel);
        EditorGUI.BeginChangeCheck();
        bool ceiling = EditorGUILayout.Toggle("천장 표시", Map.hasCeil);
        var start = EditorGUILayout.Vector2IntField("시작 좌표", new Vector2Int(Map.startX, Map.startY));
        int direction = EditorGUILayout.Popup("시작 방향", (int)Map.startDirection, DirectionNames);
        if (EditorGUI.EndChangeCheck()) Edit("맵 시작 설정", m => { m.hasCeil = ceiling; m.startX = start.x; m.startY = start.y; m.startDirection = (Direction)direction; });
        if (GUILayout.Button("맵에서 시작점 지정")) tool = MapTool.Start;
        bool resize = EditorGUILayout.Foldout(resizeOpen, $"크기 변경 ({Map.width}×{Map.height})", true);
        if (resize && !resizeOpen) { resizeWidth = Map.width; resizeHeight = Map.height; }
        resizeOpen = resize;
        if (resizeOpen)
        {
            resizeWidth = EditorGUILayout.IntField("가로", resizeWidth);
            resizeHeight = EditorGUILayout.IntField("세로", resizeHeight);
            resizeAnchor = EditorGUILayout.Popup("고정할 모서리", resizeAnchor, new[] { "왼쪽 아래", "오른쪽 아래", "왼쪽 위", "오른쪽 위" });
            if (GUILayout.Button("크기 적용"))
            {
                int width = resizeWidth, height = resizeHeight;
                int dx = resizeAnchor % 2 == 1 ? width - Map.width : 0;
                int dy = resizeAnchor >= 2 ? height - Map.height : 0;
                Later(() => ResizeMap(width, height, dx, dy));
            }
        }
        if (GUILayout.Button("현재 크기로 무작위 미로 생성")) Later(GenerateMaze);
    }
    private void MapText(string label, string value, Action<string> setter)
    {
        EditorGUI.BeginChangeCheck();
        string next = EditorGUILayout.TextField(label, value ?? "");
        if (EditorGUI.EndChangeCheck()) Edit(label + " 변경", _ => setter(next));
    }
    private void Mixed<T>(string label, Func<CellData, T> get, Func<T, T> draw, Action<CellData, T> set)
    {
        var cells = Selected.ToArray();
        if (cells.Length == 0) return;
        T current = get(cells[0]);
        EditorGUI.showMixedValue = cells.Any(c => !EqualityComparer<T>.Default.Equals(get(c), current));
        EditorGUI.BeginChangeCheck();
        T next = draw(current);
        bool changed = EditorGUI.EndChangeCheck();
        EditorGUI.showMixedValue = false;
        if (changed) EditSelected(label, c => set(c, next));
    }
    private void Boolean(string label, Func<CellData, bool> get, Action<CellData, bool> set) =>
        Mixed(label, get, v => EditorGUILayout.Toggle(label, v), set);
    private void CellText(string label, Func<CellData, string> get, Action<CellData, string> set, List<string> choices = null)
    {
        Mixed(label, get, v => EditorGUILayout.TextField(label, v ?? ""), set);
        if (choices == null || choices.Count == 0) return;
        if (GUILayout.Button(label + " 목록에서 선택", EditorStyles.miniButton))
        {
            var coordinates = selection.ToArray(); var expected = Map; var values = choices.ToArray();
            DungeonMapChoiceWindow.Show(label, values, i => ApplyAt(expected, coordinates, label, c => set(c, values[i])));
        }
    }
    private void ApplyAt(MapData expected, Vector2Int[] coordinates, string label, Action<CellData> apply)
    {
        if (this == null || Map != expected) return;
        Edit(label, map => { foreach (var p in coordinates) { var c = map.GetCell(p.x, p.y); if (c != null) apply(c); } });
    }
    private void Material(string label, Func<CellData, int> get, Action<CellData, int> set, bool objects, string empty)
    {
        var cells = Selected.ToArray();
        if (cells.Length == 0) return;
        int id = get(cells[0]);
        bool mixed = cells.Any(c => get(c) != id);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(102));
            if (GUILayout.Button(mixed ? "— 서로 다른 값" : id == -1 ? empty : TextureOrObjectLabel(id, objects)))
            {
                var expected = Map; var coordinates = selection.ToArray();
                PickMaterial(label, objects, empty, value => ApplyAt(expected, coordinates, label, c => set(c, value)));
            }
        }
        if (showAdvanced) Mixed(label + " ID", get, v => EditorGUILayout.IntField("내부 ID", v), set);
    }
    private string TextureOrObjectLabel(int id, bool objects)
    {
        Texture2D texture = null;
        if (objects && Theme != null && Theme.objectSprites != null)
            texture = Theme.objectSprites.FirstOrDefault(o => o.objectID == id).texture;
        else if (!objects && DungeonMapValidation.TextureValid(Theme, id)) texture = Theme.texture[id];
        string label = texture != null ? texture.name : "누락된 재료";
        return showAdvanced || texture == null ? $"{label} [{id}]" : label;
    }
    private void PickMaterial(string title, bool objects, string empty, Action<int> apply)
    {
        var ids = new List<int> { -1 }; var names = new List<string> { empty }; var images = new List<Texture> { null };
        var theme = Theme;
        if (objects && theme != null && theme.objectSprites != null)
            foreach (var item in theme.objectSprites)
            { ids.Add(item.objectID); names.Add($"{(item.texture != null ? item.texture.name : "이미지 없음")} [{item.objectID}]"); images.Add(item.texture); }
        else if (!objects && theme != null && theme.texture != null)
            for (int i = 0; i < theme.texture.Length; i++)
            { ids.Add(i); names.Add($"{(theme.texture[i] != null ? theme.texture[i].name : "이미지 없음")} [{i}]"); images.Add(theme.texture[i]); }
        DungeonMapChoiceWindow.Show(title, names.ToArray(), i => apply(ids[i]), images.ToArray());
    }
    private void DrawTileProperties()
    {
        Mixed("타일 종류", c => c.value, v => EditorGUILayout.Popup("종류", v + 1, CellNames) - 1, (c, v) => c.value = v);
        Material("바닥", c => c.floorTexIdx, (c, v) => c.floorTexIdx = v, false, "테마 기본값");
        Material("천장", c => c.ceilTexIdx, (c, v) => c.ceilTexIdx = v, false, "테마 기본값");
        GUILayout.Space(4);
        for (int d = 0; d < 4; d++)
        { int direction = d; Material("벽 " + DirectionNames[d], c => c.wallTextureIDs[direction], (c, v) => c.wallTextureIDs[direction] = v, false, "벽 없음"); }
        if (GUILayout.Button("네 방향 벽 모두 제거")) EditSelected("벽 모두 제거", c => DungeonMapEditing.SetWalls(c, -1));
        using (new EditorGUI.DisabledScope(paintLayer != DungeonPaintLayer.Wall || !CanPaint()))
            if (GUILayout.Button("네 방향에 팔레트의 벽 적용")) EditSelected("벽 네 방향 적용", c => DungeonMapEditing.SetWalls(c, paintValue));
        EditorGUILayout.HelpBox("벽 편집은 타일 종류를 바꾸지 않습니다. 바닥 구멍은 위의 종류에서 직접 선택합니다.", MessageType.None);
    }
    private void DrawObjectProperties()
    {
        Material("중앙 물체", c => c.centerObjectID, (c, v) => c.centerObjectID = v, true, "없음");
        for (int d = 0; d < 4; d++)
        { int direction = d; Material("벽면 " + DirectionNames[d], c => c.faceObjectIDs[direction], (c, v) => c.faceObjectIDs[direction] = v, true, "없음"); }
        Boolean("조사 / 상호작용 가능", c => c.canInteract, (c, v) => c.canInteract = v);
        if (!Selected.Any(c => c.canInteract)) return;
        CellText("필요한 조건 플래그", c => c.interactReqFlag, (c, v) => c.interactReqFlag = v, index.flags);
        Boolean("조건이 켜져 있을 때", c => c.interactReqFlagState, (c, v) => c.interactReqFlagState = v);
        CellText("조사 후 바꿀 플래그", c => c.interactSetFlag, (c, v) => c.interactSetFlag = v, index.flags);
        Boolean("조사 후 플래그 켜기", c => c.interactSetFlagState, (c, v) => c.interactSetFlagState = v);
        CellText("실행할 이벤트", c => c.interactEventID, (c, v) => c.interactEventID = v, index.events);
        CellText("이벤트가 없을 때 문구", c => c.interactSystemMessage, (c, v) => c.interactSystemMessage = v);
        interactionObjectPalette = EditorGUILayout.Popup("상호작용 재료 목록", interactionObjectPalette ? 1 : 0, new[] { "벽 이미지", "오브젝트 이미지" }) == 1;
        Material("대상 재료", c => c.interactTargetTexID, (c, v) => c.interactTargetTexID = v, interactionObjectPalette, "제한 없음");
        Material("조사 후 재료", c => c.interactChangeObjectID, (c, v) => c.interactChangeObjectID = v, interactionObjectPalette, "현재 재료 유지");
        EditorGUILayout.HelpBox("상호작용 재료 번호는 벽을 조사하면 벽 이미지, 중앙 물체를 조사하면 오브젝트 ID로 해석됩니다. 목록 전환은 번호를 바꾸지 않습니다.", MessageType.None);
        EditorGUILayout.HelpBox("빈 조건은 항상 허용합니다. 이벤트·플래그 목록은 기존 맵에서 사용한 ID를 제안하며, 새 ID도 입력할 수 있습니다.", MessageType.None);
    }
    private void DrawEntranceProperties()
    {
        if (selection.Count != 1) { EditorGUILayout.HelpBox("입구는 한 타일을 선택해서 편집하세요.", MessageType.Info); return; }
        var cell = Active;
        var entrance = Map.GetEntranceAt(cell.x, cell.y);
        if (entrance == null)
        {
            if (GUILayout.Button("이 타일에 입구 추가")) Edit("입구 추가", map => map.entrances.Add(new EntranceData
            { sourceX = cell.x, sourceY = cell.y, type = EntranceType.Map, destinationID = "", isWallEntrance = true, targetX = -1, targetY = -1 }));
            return;
        }
        // 단일 임시 복사본에 모든 필드를 표시하고 한 번만 커밋합니다.
        var draft = JsonUtility.FromJson<EntranceData>(JsonUtility.ToJson(entrance));
        EditorGUI.BeginChangeCheck();
        draft.type = (EntranceType)EditorGUILayout.Popup("입구 종류", (int)draft.type, EntranceNames);
        draft.isWallEntrance = EditorGUILayout.Popup("발동 방식", draft.isWallEntrance ? 0 : 1, new[] { "벽으로 이동을 시도할 때", "타일을 밟을 때" }) == 0;
        if (draft.type == EntranceType.Map || draft.type == EntranceType.RandomMaze)
            draft.stairType = (StairType)EditorGUILayout.Popup("이동 연출", (int)draft.stairType, new[] { "즉시 이동", "올라가는 계단", "내려가는 계단" });
        switch (draft.type)
        {
            case EntranceType.Map:
                draft.isWorldMap = EditorGUILayout.Toggle("월드 지역으로 이동", draft.isWorldMap);
                draft.destinationID = EditorGUILayout.TextField("목적지 ID", draft.destinationID ?? "");
                if (!draft.isWorldMap)
                {
                    bool defaults = draft.targetX == -1 && draft.targetY == -1;
                    bool useDefault = EditorGUILayout.Toggle("목적지 기본 시작점", defaults);
                    if (defaults != useDefault) { draft.targetX = useDefault ? -1 : 0; draft.targetY = useDefault ? -1 : 0; }
                    if (!useDefault)
                    {
                        var target = EditorGUILayout.Vector2IntField("도착 좌표", new Vector2Int(draft.targetX, draft.targetY));
                        draft.targetX = target.x; draft.targetY = target.y;
                    }
                    draft.targetDirection = (Direction)EditorGUILayout.Popup("도착 방향", (int)draft.targetDirection, DirectionNames);
                }
                break;
            case EntranceType.RandomMaze:
                draft.randomMapWidth = EditorGUILayout.IntField("미로 가로", draft.randomMapWidth);
                draft.randomMapHeight = EditorGUILayout.IntField("미로 세로", draft.randomMapHeight);
                int floors = EditorGUILayout.IntField("전체 층수", draft.randomMapMaxCount);
                if (floors != draft.randomMapMaxCount) { draft.randomMapMaxCount = floors; draft.randomMapRepeatCount = floors; }
                draft.randomMapThemeID = EditorGUILayout.TextField("미로 테마 ID (빈칸=현재)", draft.randomMapThemeID ?? "");
                draft.finalDestinationID = EditorGUILayout.TextField("마지막 도착 맵", draft.finalDestinationID ?? "");
                if (showAdvanced) draft.randomMapRepeatCount = EditorGUILayout.IntField("초기 남은 층수", draft.randomMapRepeatCount);
                break;
            case EntranceType.Shop:
            case EntranceType.Elevator:
                draft.destinationID = EditorGUILayout.TextField("목적지 ID", draft.destinationID ?? "");
                break;
            case EntranceType.FieldMap:
                draft.destinationID = EditorGUILayout.TextField("필드 경로 출발 노드", draft.destinationID ?? "");
                EditorGUILayout.HelpBox("이 노드에서 출발하는 필드 이동 경로 목록을 엽니다.", MessageType.None);
                break;
            default: EditorGUILayout.HelpBox("이 종류는 런타임의 전용 메뉴를 엽니다.", MessageType.None); break;
        }
        if (EditorGUI.EndChangeCheck()) Edit("입구 설정 변경", map => map.entrances[map.entrances.IndexOf(entrance)] = draft);
        // 위에서 문서가 갱신되었을 수 있으므로 최신 입구를 다시 얻습니다.
        entrance = Map.GetEntranceAt(cell.x, cell.y);
        var expected = Map;
        var coordinate = new Vector2Int(cell.x, cell.y);
        if (entrance.type == EntranceType.Map || entrance.type == EntranceType.RandomMaze || entrance.type == EntranceType.Shop ||
            entrance.type == EntranceType.Elevator || entrance.type == EntranceType.FieldMap)
        {
            var ids = new List<string>(); var labels = new List<string>();
            if (entrance.type == EntranceType.Shop)
                foreach (var shop in index.shops) { ids.Add(shop.shopID); labels.Add(shop.displayName + " (" + shop.shopID + ")"); }
            else if (entrance.type == EntranceType.Map && entrance.isWorldMap)
                foreach (var region in index.regions) { ids.Add(region.regionID); labels.Add(region.name + " (" + region.regionID + ")"); }
            else if (entrance.type == EntranceType.Elevator || entrance.type == EntranceType.FieldMap)
            { ids.AddRange(entrance.type == EntranceType.Elevator ? index.ElevatorIDs() : index.FieldNodeIDs()); labels.AddRange(ids); }
            else foreach (var map in index.maps) { ids.Add(map.Key); labels.Add(map.Label); }
            if (GUILayout.Button(entrance.type == EntranceType.RandomMaze ? "마지막 도착 맵 선택" : entrance.type == EntranceType.FieldMap ? "출발 노드 선택" : "목적지 목록에서 선택"))
            {
                bool final = entrance.type == EntranceType.RandomMaze;
                DungeonMapChoiceWindow.Show("목적지 선택", labels.ToArray(), i =>
                {
                    if (this == null || Map != expected) return;
                    Edit("입구 목적지 선택", map =>
                    {
                        var e = map.GetEntranceAt(coordinate.x, coordinate.y); if (e == null) return;
                        if (final) e.finalDestinationID = ids[i];
                        else { e.destinationID = ids[i]; e.targetX = -1; e.targetY = -1; }
                    });
                });
            }
        }
        if (entrance.type == EntranceType.RandomMaze && GUILayout.Button("미로 테마 선택"))
        {
            var themes = index.themes.ToArray();
            DungeonMapChoiceWindow.Show("미로 테마", themes.Select(t => t.name).ToArray(), i =>
            {
                if (this == null || Map != expected) return;
                Edit("미로 테마 선택", m => { var e = m.GetEntranceAt(coordinate.x, coordinate.y); if (e != null) e.randomMapThemeID = themes[i].themeID; });
            });
        }
        if (entrance.type == EntranceType.Map && !entrance.isWorldMap && GUILayout.Button("도착 맵에서 좌표 선택"))
        {
            var target = entrance.destinationID == Map.mapID ? Map : index.Map(entrance.destinationID)?.map;
            if (target == null) status = "먼저 유효한 목적지 맵을 선택하세요.";
            else
            {
                var expectedEntrance = entrance; string expectedDestination = entrance.destinationID;
                DungeonMapTargetWindow.Show(target, entrance.targetDirection, (p, direction) =>
                {
                    if (this == null || Map != expected) return;
                    var live = Map.GetEntranceAt(coordinate.x, coordinate.y);
                    if (live != expectedEntrance || live.destinationID != expectedDestination)
                    { status = "입구 설정이 변경되었습니다. 도착점 선택 창을 다시 열어주세요."; return; }
                    Edit("입구 도착 좌표 선택", map =>
                    { var e = map.GetEntranceAt(coordinate.x, coordinate.y); e.targetX = p.x; e.targetY = p.y; e.targetDirection = direction; });
                });
            }
        }
        if (GUILayout.Button("입구 삭제")) Edit("입구 삭제", m => m.entrances.RemoveAll(e => e.sourceX == coordinate.x && e.sourceY == coordinate.y));
    }
    private void DrawEventProperties()
    {
        if (selection.Count != 1) { EditorGUILayout.HelpBox("이벤트 순서는 한 타일을 선택해서 편집하세요.", MessageType.Info); return; }
        var cell = Active;
        GUILayout.Label("위에서부터 조건을 만족한 첫 이벤트 실행", EditorStyles.wordWrappedMiniLabel);
        for (int i = 0; i < cell.events.Count; i++)
        {
            int eventIndex = i;
            var original = cell.events[i];
            var draft = JsonUtility.FromJson<CellEventData>(JsonUtility.ToJson(original));
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{i + 1}. 이벤트", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(i == 0))
                        if (GUILayout.Button("↑", GUILayout.Width(28))) { Edit("이벤트 순서 변경", _ => { cell.events.RemoveAt(eventIndex); cell.events.Insert(eventIndex - 1, original); }); break; }
                    using (new EditorGUI.DisabledScope(i == cell.events.Count - 1))
                        if (GUILayout.Button("↓", GUILayout.Width(28))) { Edit("이벤트 순서 변경", _ => { cell.events.RemoveAt(eventIndex); cell.events.Insert(eventIndex + 1, original); }); break; }
                    if (GUILayout.Button("삭제", GUILayout.Width(42))) { Edit("이벤트 삭제", _ => cell.events.RemoveAt(eventIndex)); break; }
                }
                EditorGUI.BeginChangeCheck();
                draft.eventID = EditorGUILayout.TextField("실행할 이벤트 ID", draft.eventID ?? "");
                draft.requiredFlag = EditorGUILayout.TextField("필요한 조건 플래그", draft.requiredFlag ?? "");
                if (!string.IsNullOrEmpty(draft.requiredFlag)) draft.requiredFlagState = EditorGUILayout.Toggle("조건이 켜져 있을 때", draft.requiredFlagState);
                draft.isEventRepeatable = EditorGUILayout.Toggle("여러 번 실행", draft.isEventRepeatable);
                draft.triggerOnAttempt = EditorGUILayout.Popup("발동 시점", draft.triggerOnAttempt ? 1 : 0, new[] { "타일에 도착한 뒤", "이동을 시도할 때" }) == 1;
                draft.useForceDir = EditorGUILayout.Toggle("실행 시 방향 고정", draft.useForceDir);
                if (draft.useForceDir) draft.evForceDir = (Direction)EditorGUILayout.Popup("바라볼 방향", (int)draft.evForceDir, DirectionNames);
                if (EditorGUI.EndChangeCheck()) Edit("이벤트 설정 변경", _ => cell.events[eventIndex] = draft);
                if (GUILayout.Button("기존 이벤트 ID 선택", EditorStyles.miniButton)) PickEventField(cell, eventIndex, false);
                if (GUILayout.Button("기존 조건 플래그 선택", EditorStyles.miniButton)) PickEventField(cell, eventIndex, true);
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1회 이벤트 추가")) Edit("이벤트 추가", _ => cell.events.Add(new CellEventData()));
            if (GUILayout.Button("반복 이벤트 추가")) Edit("반복 이벤트 추가", _ => cell.events.Add(new CellEventData { isEventRepeatable = true }));
        }
        if (GUILayout.Button("이동 시도 이벤트 추가")) Edit("이동 시도 이벤트 추가", _ => cell.events.Add(new CellEventData { triggerOnAttempt = true }));
        EditorGUILayout.HelpBox("이벤트 ID는 대화/이벤트 스크립트에 정의된 ID를 연결합니다. 빈 조건은 항상 허용합니다.", MessageType.None);
    }
    private void PickEventField(CellData cell, int eventIndex, bool flag)
    {
        var expected = Map; var original = cell.events[eventIndex];
        var values = (flag ? index.flags : index.events).ToArray();
        DungeonMapChoiceWindow.Show(flag ? "조건 플래그" : "이벤트 ID", values, i =>
        {
            if (this == null || Map != expected || Map.GetCell(cell.x, cell.y) != cell || !cell.events.Contains(original)) return;
            Edit("이벤트 연결", _ => { if (flag) original.requiredFlag = values[i]; else original.eventID = values[i]; });
        });
    }
    private void DrawRegistration()
    {
        GUILayout.Label("게임에서 사용하기", EditorStyles.boldLabel);
        index.managersPrefab = (GameObject)EditorGUILayout.ObjectField("매니저 프리팹", index.managersPrefab, typeof(GameObject), false);
        var catalog = AssetDatabase.LoadAssetAtPath<DungeonMapCatalog>(DungeonMapEditorIndex.CatalogPath);
        bool connected = DungeonMapEditorIndex.CatalogConnected(index.managersPrefab, catalog);
        GUILayout.Label(connected ? "카탈로그 연결됨" : "최초 1회: 매니저 프리팹에 카탈로그를 연결하세요.", EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button("검사 후 게임 카탈로그에 등록")) Later(RegisterMap);
        using (new EditorGUI.DisabledScope(catalog == null || index.managersPrefab == null || connected))
            if (GUILayout.Button("카탈로그를 매니저 프리팹에 연결")) Later(() =>
            { DungeonMapEditorIndex.ConnectCatalog(index.managersPrefab, catalog); status = "매니저 프리팹 연결 완료"; });
    }
    // 저장/등록/목록 갱신에서 issues가 바뀌어도 현재 Layout의 표시 항목은 유지합니다.
    private DungeonMapIssue[] layoutIssues = new DungeonMapIssue[0];
    private string[] layoutIndexProblems = new string[0];
    private int layoutIssueCount;

    private void CaptureIssueLayout()
    {
        layoutIssueCount = issues.Count;
        layoutIssues = issues.Take(80).ToArray();
        layoutIndexProblems = index.problems.Take(5).ToArray();
    }

    private void DrawIssues()
    {
        issuesOpen = EditorGUILayout.Foldout(issuesOpen, $"맵 검사 ({layoutIssueCount}건)", true);
        if (!issuesOpen) return;
        foreach (var issue in layoutIssues)
        {
            string prefix = issue.severity == MessageType.Error ? "오류" : "안내";
            string coordinate = issue.position.x >= 0 ? $" ({issue.position.x},{issue.position.y})" : "";
            if (GUILayout.Button($"{prefix}{coordinate}: {issue.message}", EditorStyles.wordWrappedMiniLabel)) FocusCell(issue.position);
        }
        if (layoutIssueCount > 80) GUILayout.Label($"외 {layoutIssueCount - 80}건. 먼저 표시된 항목을 수정하세요.", EditorStyles.wordWrappedMiniLabel);
        foreach (var problem in layoutIndexProblems) EditorGUILayout.HelpBox(problem, MessageType.Warning);
    }
}
