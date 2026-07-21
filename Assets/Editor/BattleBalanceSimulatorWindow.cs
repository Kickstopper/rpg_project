using UnityEngine;
using UnityEditor;
using Helper;
using System.Collections.Generic;
using System.Linq;
using UI.Battle;

public class BattleBalanceSimulatorWindow : EditorWindow
{
    // 데이터베이스 연동
    private MonsterDatabase monsterDB;
    
    // --- 파티 스폰 설정 ---
    [System.Serializable]
    public class PlayerSimSettings
    {
        public int level = 10, str = 15, vit = 10, agi = 12, luc = 5;
    }
    private int playerSpawnCount = 4; // 기본 4명
    private PlayerSimSettings[] partySettings = new PlayerSimSettings[6];

    // --- 몬스터 스폰 설정 ---
    private int enemySpawnCount = 3;
    private int[] selectedMonsterIndices = new int[6]; 

    // 테스트 반복 횟수
    private int simulationCount = 1000;

    // 결과 데이터
    private int winCount = 0;
    private float avgTurns = 0f;
    private float avgRemainingHp = 0f;
    private float avgSurvivingPlayers = 0f;
    private int minTurns = 9999, maxTurns = 0;
    private bool hasResults = false;

    // 스크롤 뷰 위치
    private Vector2 scrollPos;

    [MenuItem("Tools/RPG Balance Simulator")]
    public static void ShowWindow()
    {
        GetWindow<BattleBalanceSimulatorWindow>("밸런스 시뮬레이터");
    }

    private void OnEnable()
    {
        // 파티 설정 배열 초기화
        for (int i = 0; i < 6; i++)
        {
            if (partySettings[i] == null)
                partySettings[i] = new PlayerSimSettings();
        }
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("데이터 연동", EditorStyles.boldLabel);
        monsterDB = (MonsterDatabase)EditorGUILayout.ObjectField("Monster Database", monsterDB, typeof(MonsterDatabase), false);

        EditorGUILayout.Space();
        GUILayout.Label("전투 시뮬레이션 설정", EditorStyles.boldLabel);
        simulationCount = EditorGUILayout.IntSlider("시뮬레이션 횟수", simulationCount, 100, 10000);

        EditorGUILayout.Space();
        
        // ---------------- 아군 파티 설정 UI ----------------
        GUILayout.BeginVertical("box");
        GUILayout.Label("아군 파티 그룹 (Player Group)", EditorStyles.boldLabel);
        playerSpawnCount = EditorGUILayout.IntSlider("파티 인원수", playerSpawnCount, 1, 6);

        EditorGUILayout.Space();
        for (int i = 0; i < playerSpawnCount; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"P{i + 1}", GUILayout.Width(25));
            partySettings[i].level = EditorGUILayout.IntField("Lv", partySettings[i].level);
            partySettings[i].str = EditorGUILayout.IntField("STR", partySettings[i].str);
            partySettings[i].vit = EditorGUILayout.IntField("VIT", partySettings[i].vit);
            partySettings[i].agi = EditorGUILayout.IntField("AGI", partySettings[i].agi);
            partySettings[i].luc = EditorGUILayout.IntField("LUC", partySettings[i].luc);
            EditorGUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // ---------------- 적군 몬스터 설정 UI ----------------
        GUILayout.BeginVertical("box");
        GUILayout.Label("적군 그룹 (Monster Group)", EditorStyles.boldLabel);

        if (monsterDB != null)
        {
            if (monsterDB.entries != null && monsterDB.entries.Count > 0)
            {
                string[] monsterNames = monsterDB.entries.Select(e => e.name).ToArray();
                enemySpawnCount = EditorGUILayout.IntSlider("스폰 마릿수", enemySpawnCount, 1, 6);

                for (int i = 0; i < enemySpawnCount; i++)
                {
                    if (selectedMonsterIndices[i] >= monsterNames.Length) selectedMonsterIndices[i] = 0;
                    selectedMonsterIndices[i] = EditorGUILayout.Popup($"몬스터 {i + 1}", selectedMonsterIndices[i], monsterNames);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("MonsterDatabase에 등록된 몬스터가 없습니다.", MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("MonsterDatabase를 상단에 할당해주세요.", MessageType.Error);
        }
        GUILayout.EndVertical();

        EditorGUILayout.Space();
        
        GUI.backgroundColor = Color.green;
        EditorGUI.BeginDisabledGroup(monsterDB == null);
        if (GUILayout.Button("시뮬레이션 실행 (자동 전투)", GUILayout.Height(40)))
        {
            RunSimulation();
        }
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = Color.white;

        // ---------------- 결과 표시 ----------------
        if (hasResults)
        {
            EditorGUILayout.Space();
            GUILayout.Label("시뮬레이션 결과", EditorStyles.boldLabel);
            
            float winRate = (winCount / (float)simulationCount) * 100f;
            GUIStyle resultStyle = new GUIStyle(EditorStyles.label);
            resultStyle.normal.textColor = winRate > 50f ? Color.blue : Color.red;

            EditorGUILayout.LabelField($"파티 승률: {winRate:F2}%", resultStyle);
            EditorGUILayout.LabelField($"승리 시 평균 생존 파티원: {avgSurvivingPlayers:F1} 명 (총 {playerSpawnCount}명 중)");
            EditorGUILayout.LabelField($"승리 시 파티 평균 잔여 HP 합산: {avgRemainingHp:F1}");
            EditorGUILayout.LabelField($"평균 소요 턴 수: {avgTurns:F1} 턴");
            EditorGUILayout.LabelField($"최소/최대 턴 수: {minTurns} 턴 / {maxTurns} 턴");
            
            EditorGUILayout.HelpBox("턴 수는 한 캐릭터의 행동이 아니라, 양 진영의 모든 캐릭터가 행동을 마치는 것을 기준으로 산정된 대략적인 횟수입니다.", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    private void RunSimulation()
    {
        winCount = 0;
        int totalTurns = 0;
        int totalRemainingHp = 0;
        int totalSurvivingPlayers = 0;
        minTurns = 9999;
        maxTurns = 0;

        for (int i = 0; i < simulationCount; i++)
        {
            SimulateGroupBattle(ref totalTurns, ref totalRemainingHp, ref totalSurvivingPlayers);
        }

        avgTurns = (float)totalTurns / simulationCount;
        avgRemainingHp = winCount > 0 ? (float)totalRemainingHp / winCount : 0f;
        avgSurvivingPlayers = winCount > 0 ? (float)totalSurvivingPlayers / winCount : 0f;
        hasResults = true;
    }

    private void SimulateGroupBattle(ref int totalTurns, ref int totalRemainingHp, ref int totalSurvivingPlayers)
    {
        List<GameObject> dummyObjects = new List<GameObject>();

        // 아군 파티 셋업
        List<PlayerController> livingPlayers = new List<PlayerController>();
        for (int i = 0; i < playerSpawnCount; i++)
        {
            GameObject pObj = new GameObject($"DummyPlayer_{i}");
            pObj.hideFlags = HideFlags.HideAndDontSave;
            dummyObjects.Add(pObj);

            PlayerController player = pObj.AddComponent<PlayerController>();
            var pData = partySettings[i];
            
            player.currentStats = new StatData { level = pData.level, str = pData.str, vit = pData.vit, agi = pData.agi, luc = pData.luc };
            player.level = pData.level;
            player.maxHp = BattleCalculator.GetMaxHP(pData.level, pData.str, pData.vit);
            player.currentHp = player.maxHp;

            livingPlayers.Add(player);
        }

        // 몬스터 그룹 셋업
        List<MonsterController> livingMonsters = new List<MonsterController>();
        for (int i = 0; i < enemySpawnCount; i++)
        {
            GameObject mObj = new GameObject($"DummyMonster_{i}");
            mObj.hideFlags = HideFlags.HideAndDontSave;
            dummyObjects.Add(mObj);

            MonsterController monster = mObj.AddComponent<MonsterController>();
            var entryData = monsterDB.entries[selectedMonsterIndices[i]];
            
            monster.sourceData = entryData;
            monster.level = entryData.stats.level;
            monster.maxHp = entryData.stats.vit * 5; 
            monster.currentHp = monster.maxHp;
            
            livingMonsters.Add(monster);
        }

        int currentTurn = 0;
        bool battleEnded = false;
        bool isPlayerWin = false;

        // 전투 루프
        while (!battleEnded && currentTurn < 100) 
        {
            currentTurn++;

            // 턴 순서 정렬 (모든 생존자 AGI 내림차순)
            List<BattleEntity> turnOrder = new List<BattleEntity>();
            turnOrder.AddRange(livingPlayers.Where(p => p.currentHp > 0));
            turnOrder.AddRange(livingMonsters.Where(m => m.currentHp > 0));
            turnOrder.Sort((a, b) => b.GetTotalAgi().CompareTo(a.GetTotalAgi()));

            // 행동 실행
            foreach (var actor in turnOrder)
            {
                if (actor.currentHp <= 0) continue; 

                if (actor is PlayerController)
                {
                    // 플레이어 -> 무작위 생존 몬스터 공격
                    var targets = livingMonsters.Where(m => m.currentHp > 0).ToList();
                    if (targets.Count > 0)
                    {
                        var randomTarget = targets[Random.Range(0, targets.Count)];
                        ExecuteAttack(actor, randomTarget);
                    }
                }
                else if (actor is MonsterController)
                {
                    // 몬스터 -> 무작위 생존 플레이어 공격
                    var targets = livingPlayers.Where(p => p.currentHp > 0).ToList();
                    if (targets.Count > 0)
                    {
                        var randomTarget = targets[Random.Range(0, targets.Count)];
                        ExecuteAttack(actor, randomTarget);
                    }
                }

                // 종료 조건 즉시 체크 (한 명이라도 공격 후 전멸 상태가 되었는지)
                bool allPlayersDead = livingPlayers.All(p => p.currentHp <= 0);
                bool allMonstersDead = livingMonsters.All(m => m.currentHp <= 0);

                if (allPlayersDead || allMonstersDead)
                {
                    battleEnded = true;
                    if (allMonstersDead) isPlayerWin = true;
                    break;
                }
            }
        }

        // 결과 집계
        totalTurns += currentTurn;
        if (currentTurn < minTurns) minTurns = currentTurn;
        if (currentTurn > maxTurns) maxTurns = currentTurn;

        if (isPlayerWin)
        {
            winCount++;
            int survivedCount = livingPlayers.Count(p => p.currentHp > 0);
            totalSurvivingPlayers += survivedCount;
            totalRemainingHp += livingPlayers.Sum(p => p.currentHp);
        }

        // 메모리 정리
        foreach (var go in dummyObjects) DestroyImmediate(go);
    }

    private void ExecuteAttack(BattleEntity attacker, BattleEntity defender)
    {
        var action = new UI.DungeonMapScene.BattleAction(attacker.gameObject, defender.gameObject, UI.ActionType.Attack, attacker.GetTotalAgi());

        if (BattleCalculator.CheckEvasion(attacker, defender, action, 0f)) return;

        bool isCrit = BattleCalculator.CheckCritical(attacker, defender, action);
        int damage = BattleCalculator.CalculateDamage(attacker, defender, action, isCrit, 1.0f);
        ResistTier tier = defender.GetResistances().GetResistanceTier(ElementType.Physical);
        if (tier == ResistTier.Repel)
        {
            attacker.currentHp = Mathf.Max(0, attacker.currentHp - damage);
        }
        else if (tier == ResistTier.Drain)
        {
            defender.currentHp = Mathf.Max(damage, defender.currentHp + damage);
        }
        else
        {
            defender.currentHp = Mathf.Max(0, defender.currentHp - damage);
        }
    }
}