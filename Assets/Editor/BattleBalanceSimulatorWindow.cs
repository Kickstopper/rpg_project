using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using RPGProject.Balance;
using UnityEditor;
using UnityEngine;

public sealed class BattleBalanceSimulatorWindow : EditorWindow
{
    BalanceScenario working, asset;
    SerializedObject serialized;
    BalanceBatch batch;
    BalanceSimulation replay;
    SimReport baseline;
    Vector2 scroll, logScroll;
    int tab, resultIndex, trialIndex, frameIndex;
    bool sweep, replayPlaying;
    float playbackSpeed = 5;
    double nextReplay;
    string error, displayError, displayBatchError;
    int displayCompleted, displayFrameIndex;
    bool displayTruncated;
    [MenuItem("Tools/RPG Balance Simulator")]
    public static void ShowWindow() => GetWindow<BattleBalanceSimulatorWindow>("Battle Balance");
    void OnEnable()
    {
        minSize = new Vector2(850, 620);
        working = CreateInstance<BalanceScenario>(); working.hideFlags = HideFlags.HideAndDontSave;
        string draft = SessionState.GetString("RPG.Balance.Draft", "");
        if (!string.IsNullOrEmpty(draft))
            try { EditorJsonUtility.FromJsonOverwrite(draft, working); } catch { }
        serialized = new SerializedObject(working);
        EditorApplication.update += UpdateWork;
        AssemblyReloadEvents.beforeAssemblyReload += StopWork;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }
    void OnDisable()
    {
        EditorApplication.update -= UpdateWork; AssemblyReloadEvents.beforeAssemblyReload -= StopWork;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        if (working != null) SessionState.SetString("RPG.Balance.Draft", EditorJsonUtility.ToJson(working));
        StopWork(); if (working != null) DestroyImmediate(working);
    }
    void OnPlayMode(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) StopWork(); }
    void StopWork() { replay?.Dispose(); replay = null; replayPlaying = false; batch?.Dispose(); batch = null; }
    void UpdateWork()
    {
        if (batch != null && batch.Running) { batch.Tick(); Repaint(); }
        if (replayPlaying && replay != null && EditorApplication.timeSinceStartup >= nextReplay)
        {
            nextReplay = EditorApplication.timeSinceStartup + 1.0 / playbackSpeed;
            AdvanceReplay(); Repaint();
        }
    }
    void OnGUI()
    {
        if (working == null) return;
        if (Event.current.type == EventType.Layout)
        {
            displayError = error; displayBatchError = batch?.Error;
            displayCompleted = batch?.Completed ?? 0; displayFrameIndex = frameIndex; displayTruncated = replay != null && replay.TraceTruncated;
        }
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var selected = (BalanceScenario)EditorGUILayout.ObjectField(asset, typeof(BalanceScenario), false, GUILayout.Width(250));
            if (selected != asset && selected != null)
            {
                asset = selected; DestroyImmediate(working); working = Instantiate(asset);
                working.hideFlags = HideFlags.HideAndDontSave; serialized = new SerializedObject(working); GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("새 시나리오", EditorStyles.toolbarButton))
            {
                asset = null; DestroyImmediate(working); working = CreateInstance<BalanceScenario>(); working.hideFlags = HideFlags.HideAndDontSave;
                serialized = new SerializedObject(working); GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("시나리오 사본 저장", EditorStyles.toolbarButton)) SaveScenario();
            GUILayout.FlexibleSpace();
        }
        tab = GUILayout.Toolbar(tab, new[] { "시나리오", "반복 실행 · 비교", "전투 재생" });
        EditorGUILayout.HelpBox("공유: 실제 능력치·피해·명중·상태이상·치료 코드. DB의 기본 AI 4종 지원; 수동 정책 선택 가능. 입력 예약/타깃 재선정은 일부 별도 모델. 제외: 난입 게이지, 협동기, 교섭, 총기/탄약, 장비 방어구, 다단 공격. 최종 검증은 PlayMode 실전과 비교하세요.", MessageType.Info);
        if (!string.IsNullOrEmpty(displayError)) EditorGUILayout.HelpBox(displayError, MessageType.Error);
        if (!string.IsNullOrEmpty(displayBatchError)) EditorGUILayout.HelpBox(displayBatchError, MessageType.Error);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (tab == 0) DrawScenario(); else if (tab == 1) DrawBatch(); else DrawReplay();
        EditorGUILayout.EndScrollView();
    }
    void DrawScenario()
    {
        using (new EditorGUI.DisabledScope(batch != null && batch.Running))
        {
            serialized.Update();
            foreach (string name in new[] { "scenarioName", "monsterDatabase", "party", "enemies", "opening", "useDatabaseAI", "chargeEnemySkillCost", "trials", "seed", "maxRounds",
                "partyHpScale", "enemyHpScale", "enemyStatScale", "skillChance", "healThreshold", "replayFrameLimit" })
                EditorGUILayout.PropertyField(serialized.FindProperty(name), true);
            serialized.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("적의 monsterId를 지정하면 DB 능력치/내성/스킬을 사용합니다. 빈 ID는 수동 능력치를 사용합니다. 아군은 수동 능력치/내성/무기를 사용합니다. position 0~2=전열, 3~5=후열. 기본 공격은 단일 공격으로 모델링합니다.", MessageType.None);
            DrawMonsterPicker();
            if (GUILayout.Button("피로 상태 프리셋: 아군 HP 50% / MP 25%"))
            {
                foreach (var slot in working.party) { slot.hpRatio = .5f; slot.mpRatio = .25f; }
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("아군 HP/MP 100%로 복원"))
            {
                foreach (var slot in working.party) { slot.hpRatio = slot.mpRatio = 1; }
                GUIUtility.ExitGUI();
            }
        }
        DrawStart();
    }
    void DrawMonsterPicker()
    {
        if (working.monsterDatabase == null || working.monsterDatabase.entries == null) return;
        var entries = working.monsterDatabase.entries.Where(e => e != null).ToList();
        if (entries.Count == 0) return;
        var names = new[] { "수동 설정" }.Concat(entries.Select(e => e.id + " / " + e.name)).ToArray();
        for (int i = 0; i < working.enemies.Count; i++)
        {
            var slot = working.enemies[i]; if (slot == null) continue;
            int old = entries.FindIndex(e => e.id == slot.monsterId) + 1;
            int next = EditorGUILayout.Popup("적 " + (i + 1) + " DB 선택", old, names);
            if (next != old) { slot.monsterId = next == 0 ? "" : entries[next - 1].id; slot.label = next == 0 ? "Enemy" : entries[next - 1].name; }
        }
    }
    void DrawStart()
    {
        sweep = EditorGUILayout.Toggle("적 능력치 0.8 / 1.0 / 1.2배 비교", sweep);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || (batch != null && batch.Running)))
            if (GUILayout.Button("반복 시뮬레이션 시작", GUILayout.Height(34)))
            {
                error = string.Join("\n", BalanceSimulation.Validate(working));
                if (string.IsNullOrEmpty(error))
                {
                    StopWork();
                    try { batch = new BalanceBatch(working, sweep); resultIndex = trialIndex = 0; tab = 1; }
                    catch (Exception e) { error = e.Message; }
                }
                GUIUtility.ExitGUI();
            }
    }
    void DrawBatch()
    {
        DrawStart();
        if (batch == null) return;
        Rect progress = GUILayoutUtility.GetRect(10, 24);
        EditorGUI.ProgressBar(progress, batch.Requested == 0 ? 0 : (float)displayCompleted / batch.Requested,
            $"완료 {displayCompleted:N0} / {batch.Requested:N0} · 계산 시간 {batch.Seconds:F1}초");
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!batch.Running))
            {
                if (GUILayout.Button(batch.Paused ? "계속" : "일시정지")) batch.Paused = !batch.Paused;
                if (GUILayout.Button("취소 (완료 결과 유지)")) { batch.Cancel(); GUIUtility.ExitGUI(); }
            }
        }
        for (int i = 0; i < batch.Reports.Count; i++)
        {
            var r = batch.Reports[i];
            using (new EditorGUILayout.VerticalScope("box"))
            {
                GUILayout.Label(r.name, EditorStyles.boldLabel);
                var ci = SimReport.Wilson(r.Wins, r.trials.Count);
                GUILayout.Label($"완료 {r.trials.Count} | 승 {r.Wins} / 패 {r.Losses} / 제한 초과 {r.Timeouts} | 승률 {r.WinRate:P1} (95% CI {ci.x:P1}–{ci.y:P1})");
                Rect bar = GUILayoutUtility.GetRect(20, 16); DrawOutcomeBar(bar, r);
                var turns = r.trials.Select(t => t.rounds).OrderBy(t => t).ToList();
                GUILayout.Label(turns.Count == 0 ? "결과 대기" : $"라운드 평균 {turns.Average():F1} / 중앙값 {turns[turns.Count / 2]} / P90 {turns[Mathf.Clamp(Mathf.CeilToInt(turns.Count * .9f) - 1, 0, turns.Count - 1)]}");
                var wins = r.trials.Where(t => t.outcome == SimOutcome.Win).ToList();
                GUILayout.Label(wins.Count == 0 ? "승리 시 생존/잔여 HP: 해당 없음" : $"승리 시 평균 생존 {wins.Average(t => t.survivors):F1}명 / 잔여 HP {wins.Average(t => t.remainingHpRatio):P1}");
                DrawHistogram(GUILayoutUtility.GetRect(20, 65), turns);
                if (baseline != null) GUILayout.Label($"고정 기준 '{baseline.name}' 대비 승률 차이 {(r.WinRate - baseline.WinRate) * 100:+0.0;-0.0;0} %p (표본 {baseline.trials.Count})");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("이 결과를 기준으로 고정")) { baseline = JsonUtility.FromJson<SimReport>(JsonUtility.ToJson(r)); GUIUtility.ExitGUI(); }
                    if (GUILayout.Button("완료된 전투 재생")) { resultIndex = i; tab = 2; GUIUtility.ExitGUI(); }
                    if (GUILayout.Button("JSON 저장")) Export(r, false);
                    if (GUILayout.Button("CSV 저장")) Export(r, true);
                }
            }
        }
        GUILayout.Label("첫 번째 표본의 진행 화면 (나머지 반복에서는 기록 생성을 생략)", EditorStyles.boldLabel);
        DrawFrame(batch.LiveFrame);
    }
    void DrawReplay()
    {
        if (batch == null) { GUILayout.Label("먼저 반복 시뮬레이션을 실행하세요."); return; }
        resultIndex = EditorGUILayout.Popup("시나리오", resultIndex, batch.Reports.Select(r => r.name).ToArray());
        var report = batch.Reports[resultIndex];
        trialIndex = EditorGUILayout.IntField("표본 번호 (0부터)", trialIndex);
        using (new EditorGUI.DisabledScope(batch.Running || trialIndex < 0 || trialIndex >= report.trials.Count))
            if (GUILayout.Button("선택 표본을 동일 시드로 재생"))
            {
                replay?.Dispose();
                try { replay = new BalanceSimulation(batch.Configurations[resultIndex], report.trials[trialIndex].seed, true); frameIndex = 0; replayPlaying = false; }
                catch (Exception e) { error = e.Message; }
                GUIUtility.ExitGUI();
            }
        if (replay == null) return;
        GUILayout.Label($"Seed {replay.Result.seed} | {replay.Result.outcome} | 행동 {replay.Result.actions}");
        if (GUILayout.Button("재생 기록 JSON 저장"))
        {
            string path = EditorUtility.SaveFilePanel("재생 기록 저장", "", "BattleReplay", "json");
            if (!string.IsNullOrEmpty(path))
                try { File.WriteAllText(path, JsonUtility.ToJson(new ReplayExport { seed = replay.Result.seed, truncated = replay.TraceTruncated, frames = replay.Frames }, true)); }
                catch (Exception e) { error = e.Message; }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(replayPlaying ? "일시정지" : "재생")) replayPlaying = !replayPlaying;
            if (GUILayout.Button("다음 행동")) { replayPlaying = false; AdvanceReplay(); GUIUtility.ExitGUI(); }
            playbackSpeed = EditorGUILayout.Slider("초당 행동", playbackSpeed, 1, 30);
        }
        if (displayTruncated) EditorGUILayout.HelpBox("기록 상한에 도달했습니다. 시뮬레이션은 계속되지만 이후 프레임은 저장하지 않습니다.", MessageType.Warning);
        int selected = EditorGUILayout.IntSlider("기록 위치", frameIndex, 0, Mathf.Max(0, replay.Frames.Count - 1));
        if (selected != frameIndex) { frameIndex = selected; replayPlaying = false; }
        DrawFrame(replay.Frames.Count == 0 ? null : replay.Frames[frameIndex]);
        DrawHpHistory(GUILayoutUtility.GetRect(20, 110), replay.Frames, frameIndex);
        logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.Height(150));
        int start = Mathf.Max(0, displayFrameIndex - 30);
        for (int i = start; i <= displayFrameIndex && i < replay.Frames.Count; i++) GUILayout.Label($"{i}: {replay.Frames[i].message}");
        EditorGUILayout.EndScrollView();
    }
    void AdvanceReplay()
    {
        if (replay == null) return;
        if (frameIndex < replay.Frames.Count - 1) frameIndex++;
        else if (!replay.Done) { replay.Step(); frameIndex = replay.Frames.Count - 1; }
        else replayPlaying = false;
    }
    static void DrawFrame(SimFrame frame)
    {
        Rect rect = GUILayoutUtility.GetRect(20, 220); EditorGUI.DrawRect(rect, new Color(.11f, .12f, .14f));
        if (frame == null) { GUI.Label(rect, "전투 기록 대기"); return; }
        GUI.Label(new Rect(rect.x + 8, rect.y, rect.width - 16, 22), $"R{frame.round} / 행동 {frame.action} — {frame.message}");
        int left = 0, right = 0;
        foreach (var u in frame.units)
        {
            int row = u.party ? left++ : right++;
            float x = rect.x + (u.party ? 8 : rect.width / 2 + 4), y = rect.y + 24 + row * 31;
            float width = rect.width / 2 - 16;
            GUI.Label(new Rect(x, y, width, 18), $"{u.name}  HP {u.hp}/{u.maxHp}  MP {u.mp}/{u.maxMp}  {u.statuses.Replace("\n", " · ")}");
            EditorGUI.DrawRect(new Rect(x, y + 20, width, 6), Color.black);
            EditorGUI.DrawRect(new Rect(x, y + 20, width * u.hp / Mathf.Max(1f, u.maxHp), 6), u.party ? new Color(.25f, .8f, .5f) : new Color(.95f, .45f, .3f));
            EditorGUI.DrawRect(new Rect(x, y + 27, width, 3), Color.black);
            EditorGUI.DrawRect(new Rect(x, y + 27, width * u.mp / Mathf.Max(1f, u.maxMp), 3), new Color(.3f, .6f, 1f));
        }
    }
    static void DrawOutcomeBar(Rect rect, SimReport r)
    {
        EditorGUI.DrawRect(rect, Color.gray); if (r.trials.Count == 0) return;
        float win = rect.width * r.Wins / r.trials.Count, loss = rect.width * r.Losses / r.trials.Count;
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, win, rect.height), new Color(.2f, .65f, .4f));
        EditorGUI.DrawRect(new Rect(rect.x + win, rect.y, loss, rect.height), new Color(.8f, .3f, .3f));
    }
    static void DrawHistogram(Rect rect, List<int> turns)
    {
        EditorGUI.DrawRect(rect, new Color(.13f, .13f, .15f)); if (turns.Count == 0) return;
        int[] bins = new int[10]; int max = Mathf.Max(1, turns.Max());
        foreach (int t in turns) bins[Mathf.Clamp((t - 1) * 10 / max, 0, 9)]++;
        int peak = Mathf.Max(1, bins.Max());
        for (int i = 0; i < bins.Length; i++)
        {
            float height = (rect.height - 18) * bins[i] / peak;
            EditorGUI.DrawRect(new Rect(rect.x + i * rect.width / 10, rect.yMax - 18 - height, rect.width / 10 - 2, height), new Color(.4f, .65f, .95f));
        }
        GUI.Label(new Rect(rect.x, rect.yMax - 18, rect.width, 18), $"라운드 분포: 1 → {max} (10구간)");
    }
    static void DrawHpHistory(Rect rect, List<SimFrame> frames, int selected)
    {
        EditorGUI.DrawRect(rect, new Color(.12f, .12f, .14f)); if (frames.Count < 2) return;
        Handles.BeginGUI();
        foreach (bool party in new[] { true, false })
        {
            Handles.color = party ? Color.green : new Color(1, .4f, .2f);
            int stride = Mathf.Max(1, frames.Count / 500);
            Vector3? previous = null;
            for (int i = 0; i < frames.Count; i += stride)
            {
                var group = frames[i].units.Where(u => u.party == party).ToList();
                float ratio = (float)group.Sum(u => u.hp) / Mathf.Max(1, group.Sum(u => u.maxHp));
                var point = new Vector3(rect.x + rect.width * i / (frames.Count - 1), rect.yMax - 18 - ratio * (rect.height - 25));
                if (previous.HasValue) Handles.DrawLine(previous.Value, point); previous = point;
            }
        }
        Handles.color = Color.white; float cursor = rect.x + rect.width * selected / (frames.Count - 1);
        Handles.DrawLine(new Vector3(cursor, rect.y), new Vector3(cursor, rect.yMax)); Handles.EndGUI();
        GUI.Label(new Rect(rect.x, rect.yMax - 18, rect.width, 18), "행동별 총 HP 비율 — 녹색 아군 / 주황 적");
    }
    void SaveScenario()
    {
        string path = EditorUtility.SaveFilePanelInProject("시나리오 사본 저장", "BalanceScenario", "asset", "저장할 위치를 선택하세요.");
        if (string.IsNullOrEmpty(path)) return;
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) { error = "기존 에셋을 덮어쓰지 않도록 새 이름을 사용하세요."; return; }
        var copy = Instantiate(working); copy.hideFlags = HideFlags.None; AssetDatabase.CreateAsset(copy, path); AssetDatabase.SaveAssets(); asset = copy;
    }
    [Serializable] public sealed class ReplayExport
    {
        public int seed;
        public bool truncated;
        public List<SimFrame> frames;
    }
    void Export(SimReport report, bool csv)
    {
        string path = EditorUtility.SaveFilePanel("결과 저장", "", "BalanceResults", csv ? "csv" : "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            string text = csv ? "seed,outcome,rounds,actions,survivors,remainingHpRatio\n" + string.Join("\n", report.trials.Select(t =>
                $"{t.seed},{t.outcome},{t.rounds},{t.actions},{t.survivors},{t.remainingHpRatio.ToString(CultureInfo.InvariantCulture)}")) : JsonUtility.ToJson(report, true);
            File.WriteAllText(path, text, new System.Text.UTF8Encoding(true));
        }
        catch (Exception e) { error = e.Message; }
    }
}
