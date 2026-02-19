using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UI.DungeonMapScene;
using TMPro;
using UnityEngine.EventSystems;
using Data;
using UI;
using DG.Tweening;
using Helper;
using Manager;

namespace Controller
{
    public enum BattleState { Start, PlayerInput, EnemyInput, Processing, Won, Lost }
    
    public class BattleManager : MonoBehaviour
    {
        [Header("UI References")]
        public BattleUIController uiController; // 인스펙터에서 할당
        public BattleVisualController visualController; // 인스펙터에서 할당
        public Transform damagePopupContainer;
        
        [Header("Escape Settings")]
        public int guaranteedEscapeAttempts = 3; // 몇 번째 시도부터 무조건 성공할지 설정
        private int currentEscapeAttempts = 0;   // 현재 전투에서의 시도 횟수
        private int currentFightBtnIndex = 0; // fight 메뉴용 인덱스
        private int currentBaseBtnIndex = 0;  // Base 메뉴용 인덱스

        // 메뉴 계층 관리 변수
        private bool isSubMenuActive = false; // 현재 서브 메뉴가 열려있는지
        
        private List<Button> cachedMainMenuButtons = new List<Button>(); // 메인 메뉴 버튼들을 임시 저장할 리스트 (서브 메뉴에서 돌아올 때 복구용)

        [Header("Prefabs")]
        public GameObject defaultMonsterPrefab;
        public GameObject playerPrefab;
        public GameObject damagePopupPrefab;
        
        [Header("First Focus Buttons")]
        public GameObject baseFirstButton;    // Base 메뉴의 첫 버튼 (Fight 버튼)
        public GameObject attackButton;    // Fight 메뉴의 첫 버튼 (Attack 버튼)

        public Vector3 cursorOffset = new Vector3(0, 50, 0); // 몬스터 머리 위 오프셋

        // 타겟팅 로직 변수
        private List<BattleEntity> validTargets = new();
        private int currentTargetIndex = 0;

        [Header("Managers & Data")]
        public MonsterDatabase monsterDB;
        // PartyManager.Instance 사용

        [Header("Spawn Points")]
        public Transform enemyFrontRowContainer;
        public Transform enemyBackRowContainer;
        public Transform playerFrontRowContainer;
        public Transform playerBackRowContainer;

        [Header("Player Slots")]
        //아군 슬롯 리스트
        private List<Transform> playerFrontSlots = new();
        private List<Transform> playerBackSlots = new();

        //이동 모드 관련 변수
        private bool isSelectingMoveTarget = false;
        private int currentMoveSlotIndex = 0; // 0~2: 전열, 3~5: 후열

        [Header("Highlight Colors")]
        private Color currentTargetColor = new Color32(128, 0, 178, 255);
        private Color moveSourceColor = Color.gray;   // 이동하려는 내 캐릭터 색상

        [Header("Button Colors")]
        private Color colorNormal = Color.white;          // 일반 텍스트
        private Color colorGrayout = Color.gray;          // 사용 불가 텍스트

        [Header("Slot Management")]
        // 몬스터들의 슬롯을 관리할 리스트 (0,1,2: 전열 / 0,1,2: 후열)
        private List<Transform> frontSlots = new();
        private List<Transform> backSlots = new();
        
        private BaseRootData currentSelectedItem; // 현재 사용하려는 아이템
        private bool isAutoMode = false; // 오토 모드 활성화 여부
        // 오토 모드 종료 예약 플래그
        private bool reserveAutoOff = false;
        
        // 각 캐릭터(인덱스)가 마지막으로 수행한 행동 타입 저장
        private Dictionary<int, (ActionType type, BaseRootData data, GameObject target)> lastPlayerActions = new();
        // -------------------------------------------------------
        // [핵심 변수] 전투 상태 및 리스트
        // -------------------------------------------------------
        public BattleState state;
        [HideInInspector] public List<BattleEntity> activeMonsters = new();
        // 전투 로직용 리스트 (데이터가 있는 캐릭터만)
        private List<MonsterDatabase.MonsterEntry> encounterLog = new();
        private List<BattleEntity> activePlayers = new(); 

        // 렌더링 및 그리드 관리용 리스트 (Empty 포함, 총 6개 고정)
        private List<PlayerController> allSlotControllers = new();

        private List<CombatAction> actionQueue = new(); // 이번 턴의 모든 행동

        // 입력 제어용 변수
        private int currentPlayerIndex = 0; // 지금 누구 차례?
        private ActionType currentSelectedAction;
        private bool isSelectingTarget = false;

        // 위치 정보를 반환하는 구조체
        public struct CombatPosition
        {
            public bool isFrontRow; // 전열이면 true
            public int columnIndex; // 0:왼쪽, 1:가운데, 2:오른쪽
        }

        // 마지막으로 선택된 UI 오브젝트를 기억하는 변수
        private GameObject lastSelectedObject;
        
        // 입력 중복 방지용 쿨타임
        private float inputCooldown = 0f;

        // "싸우다"를 선택했는지 여부
        private bool isFightMode = false;
        // 배수진(Last Stand) 활성화 플래그
        private bool isLastStandActive = false;
        private bool isLastStandInputMode = false; // isLastStandActive는 '실행/데미지'용이고, 이건 '입력 스킵'용

        // Union Attack 참가자 목록 (턴 스킵 및 애니메이션용)
        private List<PlayerController> currentUnionParticipants = new List<PlayerController>();
        private bool isUnionAttackUsedThisTurn = false;
        
        // 점멸 효과 트윈 저장용 (취소 시 멈추기 위해)
        private List<Tween> blinkTweens = new List<Tween>();

        // 자주 쓰는 딜레이 캐싱
        private WaitForSeconds wait01 = new WaitForSeconds(0.1f);
        private WaitForSeconds wait05 = new WaitForSeconds(0.5f);
        private WaitForSeconds wait10 = new WaitForSeconds(1f);

        private bool isBattleState = false; // 현재 전투 상태인지 아닌지

        public struct BattleReward
        {
            public int totalExp;      // 파티가 획득한 총 경험치
            public int expPerMember;  // 개인당 돌아가는 경험치
            public int totalGold;     // 획득한 총 골드
            public List<string> dropItems; // 획득한 아이템 ID 목록
        }
        
        void Start()
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            OnGameStateChanged(GameStateManager.Instance.CurrentState);
        }

        void OnDestroy()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        // 상태가 바뀔 때마다 자동으로 호출되는 함수
        void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Battle)
            {
                //SoundManager.Instance.PlayBGM();
                isBattleState = true;
            }
            else
            {
                isBattleState = false;
            }
        }

        public void Initialize(List<string> monsterIds)
        {
            // 전투 진입 시 UI를 일단 모두 숨김 (깜빡임 방지)
            uiController.Initialize();

            
            SetEnemyVisualsActive(false);
            
            isAutoMode = false;         
            reserveAutoOff = false;     
            uiController.SetAutoButtonVisible(false);

            isFightMode = false;        
            Time.timeScale = 1.0f;

            currentBaseBtnIndex = 0;
            currentFightBtnIndex = 0;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            
            state = BattleState.Start;

            // 도망 횟수 초기화
            currentEscapeAttempts = 0; 

            activeMonsters.Clear();
            encounterLog.Clear();
            ClearParty();
            InitializeSlots();

            if (monsterIds == null || monsterIds.Count == 0) return;
            
            int maxSpawnLimit = Mathf.Min(monsterIds.Count, 6);
            int spawnCount = Random.Range(1, maxSpawnLimit + 1); 

            Debug.Log($"[Encounter] 몬스터 {spawnCount}마리가 출현합니다!");

            for (int i = 0; i < spawnCount; i++)
            {
                int randomIndex = Random.Range(0, monsterIds.Count);
                SpawnMonster(monsterIds[randomIndex]);
            }
            
            SpawnParty();

            if (activePlayers.Count == 0)
            {
                GameStateManager.Instance.ChangeState(GameState.Exploration);
                return;  
            }  

            // =========================================================
            // 인스턴트 윈 조건 체크 및 분기
            // =========================================================
            if (CheckInstantWinCondition())
            {
                Debug.Log("조건 만족: 인스턴트 전투 실행");
                
                // 유닛들의 모습(Sprite)을 숨김
                SetPlayerVisualsActive(false);
                StartCoroutine(ProcessInstantWin());
            }
            else
            {
                SetPlayerVisualsActive(true);
                uiController.ShowBattleStartAnimation(()=> { 
                    StartCoroutine(SetupBattle()); 
                });
            }
        }

        // 전투 유닛 및 슬롯 컨테이너 표시/숨김 제어
        void SetEnemyVisualsActive(bool isActive)
        {
            if (enemyFrontRowContainer) enemyFrontRowContainer.gameObject.SetActive(isActive);
            if (enemyBackRowContainer) enemyBackRowContainer.gameObject.SetActive(isActive);
            
        }

        void SetPlayerVisualsActive(bool isActive)
        {
            if (playerFrontRowContainer) playerFrontRowContainer.gameObject.SetActive(isActive);
            if (playerBackRowContainer) playerBackRowContainer.gameObject.SetActive(isActive);
            
        }

        void SpawnParty()
        {
            activePlayers.Clear();
            allSlotControllers.Clear();

            // ---------------------------------------------------------
            // 1단계: 배치 시뮬레이션 (누가 어디에 설지 미리 결정)
            // ---------------------------------------------------------
            
            // 6개의 슬롯에 들어갈 데이터 배열 (null이면 빈자리)
            RuntimeCharacterData[] slotAssignments = new RuntimeCharacterData[6];
            
            // 자리를 잡지 못한 캐릭터들을 모아둘 리스트
            List<RuntimeCharacterData> pendingCharacters = new List<RuntimeCharacterData>();

            int partyCount = PartyManager.Instance.partyData.Count;

            // [Pass 1] 선호하는 위치에 우선 배치
            for (int i = 0; i < partyCount; i++)
            {
                var member = PartyManager.Instance.GetMember(i);
                if (member == null || member.currentHp <= 0) continue;

                // 데이터상의 위치를 인덱스로 변환
                // 전열(0,1,2), 후열(3,4,5)
                int rowIndex = (member.row == RowType.Front) ? 0 : 3;
                int colIndex = (int)member.column; // Left(0), Center(1), Right(2)
                
                // 안전장치: 컬럼이 범위를 벗어나면 Center(1)로 보정하거나 Clamp
                colIndex = Mathf.Clamp(colIndex, 0, 2);

                int targetSlotIndex = rowIndex + colIndex;

                // 자리가 비어있다면 -> 배정
                if (slotAssignments[targetSlotIndex] == null)
                {
                    slotAssignments[targetSlotIndex] = member;
                }
                else
                {
                    // 자리가 이미 있다면 -> 대기열로 이동
                    pendingCharacters.Add(member);
                }
            }

            // [Pass 2] 남은 빈자리에 대기 인원 배치
            foreach (var pendingMember in pendingCharacters)
            {
                for (int i = 0; i < 6; i++)
                {
                    // 빈 자리를 발견하면
                    if (slotAssignments[i] == null)
                    {
                        slotAssignments[i] = pendingMember;

                        // 실제 배치된 위치에 맞춰 데이터 갱신 (저장 시 반영되도록)
                        bool isFront = (i < 3);
                        pendingMember.row = isFront ? RowType.Front : RowType.Back;
                        pendingMember.column = (ColumnType)(isFront ? i : i - 3);
                        
                        break; // 배치 완료했으니 다음 대기 인원으로
                    }
                }
            }

            // ---------------------------------------------------------
            // 2단계: 결정된 배치대로 실제 오브젝트 생성 (Instantiate)
            // ---------------------------------------------------------
            for (int i = 0; i < 6; i++)
            {
                // 1. 타겟 슬롯 Transform 찾기
                bool isFront = (i < 3);
                int localIndex = isFront ? i : (i - 3);
                Transform targetSlot = isFront ? playerFrontSlots[localIndex] : playerBackSlots[localIndex];

                // 2. 프리팹 생성
                GameObject go = Instantiate(playerPrefab, targetSlot);
                go.transform.localPosition = Vector3.zero;

                PlayerController pc = go.GetComponent<PlayerController>();
                allSlotControllers.Add(pc);

                // 생성된 플레이어 버튼의 자동 내비게이션 비활성화
                if (pc.selectButton != null)
                {
                    Navigation nav = new Navigation();
                    nav.mode = Navigation.Mode.None;
                    pc.selectButton.navigation = nav;
                }

                // 3. 데이터 주입
                RuntimeCharacterData assignedData = slotAssignments[i];

                if (assignedData != null)
                {
                    // 실제 캐릭터 초기화
                    pc.Initialize(assignedData, this, true);
                    
                    pc.columnIndex = i;
                    pc.gameObject.name = pc.entityName;
                    activePlayers.Add(pc);
                }
                else
                {
                    // 빈 슬롯 초기화
                    pc.InitializeEmpty(i);
                }
            }
        }

        IEnumerator SetupBattle()
        {
            SetEnemyVisualsActive(true);
            SoundManager.Instance.PlayBGM(BgmID.Encounter);
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(enemyFrontRowContainer as RectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(playerFrontRowContainer as RectTransform);
            yield return wait10;
            PreparePlayerTurn();
        }

        private void PrepareWeaponAction(WeaponData weapon, ActionType actionType)
        {
            BattleEntity currentActor = activePlayers[currentPlayerIndex];
            TargetScope scope = TargetScope.Front_Single_Enemy; 
            
            if (weapon != null) scope = weapon.attackRange;
            else if (actionType == ActionType.Shoot) return; 

            if (scope == TargetScope.Front_Single_Enemy || scope == TargetScope.Single_Enemy)
            {
                validTargets = activeMonsters.Where(m => m.currentHp > 0).ToList();

                if (scope == TargetScope.Front_Single_Enemy)
                {
                    validTargets = validTargets.Where(m => m.transform.parent.parent == enemyFrontRowContainer).ToList();
                    if (validTargets.Count == 0) validTargets = activeMonsters.Where(m => m.currentHp > 0).ToList();
                }
                
                validTargets = validTargets.OrderBy(m => m.transform.parent.parent == enemyBackRowContainer)
                                            .ThenBy(m => m.transform.position.x).ToList();

                if (validTargets.Count == 0) return; 

                currentSelectedAction = actionType;
                isSelectingTarget = true;
                
                uiController.SetCmdPanelVisible(false);
                uiController.ShowLog("SELECT TARGET");
                currentTargetIndex = 0;
                UpdateTargetHighlight();
                inputCooldown = 0.2f;
            }
            else
            {
                currentSelectedAction = actionType;
                int speed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty;
                currentActor.nextTurnSpeedPenalty = 0; 

                CombatAction action = new CombatAction(currentActor.gameObject, null, actionType, speed);
                actionQueue.Add(action);

                NextPlayerInput();
            }
        }

        void PreparePlayerTurn()
        {
            isUnionAttackUsedThisTurn = false;
            isLastStandInputMode = false; 

            if (isLastStandActive)
            {
                isLastStandActive = false;
                foreach (var p in activePlayers)
                {
                    if (p.columnIndex < 3)
                    {
                        // 복귀 애니메이션
                        p.transform.DOLocalMove(Vector3.zero, 0.3f).SetEase(Ease.OutQuad);
                        p.transform.DOScale(Vector3.one, 0.3f);
                    }
                }
            }

            if (reserveAutoOff)
            {
                isAutoMode = false;
                reserveAutoOff = false;
                uiController.SetAutoButtonVisible(false);
                Time.timeScale = 1.0f; 
                uiController.HideLog();
            }

            StartCoroutine(PreparePlayerTurnRoutine());
        }

        IEnumerator PreparePlayerTurnRoutine()
        {
            foreach (var player in activePlayers) player.ResetStatus(); 
            foreach (var monster in activeMonsters) monster.ResetStatus(); 

            yield return StartCoroutine(ProcessEnemyRowShift());
            yield return StartCoroutine(ProcessPlayerRowShift());

            state = BattleState.PlayerInput;
            actionQueue.Clear(); 
            currentPlayerIndex = -1; 
            isFightMode = false;

            CalculateAndShowTurnOrder();
            NextPlayerInput();
        }

        IEnumerator ProcessPlayerRowShift()
        {
            for (int col = 0; col < 3; col++)
            {
                int frontIdx = col;
                int backIdx = col + 3;

                PlayerController frontPC = allSlotControllers[frontIdx];
                PlayerController backPC = allSlotControllers[backIdx];

                bool backCanMove = !backPC.IsEmpty && backPC.currentHp > 0;
                if (!backCanMove) continue;

                bool frontIsOpen = frontPC.IsEmpty || frontPC.currentHp <= 0;

                if (frontIsOpen)
                {
                    yield return StartCoroutine(SwapPlayerSlots(frontIdx, backIdx));
                }
            }
        }

        // 슬롯 교체 애니메이션
        IEnumerator SwapPlayerSlots(int frontIdx, int backIdx)
        {
            PlayerController frontPC = allSlotControllers[frontIdx];
            PlayerController backPC = allSlotControllers[backIdx];

            Transform frontSlot = playerFrontSlots[frontIdx]; 
            Transform backSlot = playerBackSlots[backIdx - 3];

            Debug.Log($"[전진] {backPC.name}가 전열로 이동");

            allSlotControllers[frontIdx] = backPC;
            allSlotControllers[backIdx] = frontPC;

            backPC.columnIndex = frontIdx;
            frontPC.columnIndex = backIdx;

            // 부모 변경
            backPC.transform.SetParent(frontSlot, true);
            frontPC.transform.SetParent(backSlot, true);

            // 두 캐릭터를 동시에 이동
            Sequence seq = DOTween.Sequence();
            seq.Join(backPC.transform.DOLocalMove(Vector3.zero, 0.4f).SetEase(Ease.InOutSine));
            seq.Join(frontPC.transform.DOLocalMove(Vector3.zero, 0.4f).SetEase(Ease.InOutSine));
            
            yield return seq.WaitForCompletion();
        }

        void CalculateAndShowTurnOrder()
        {
            activePlayers.Sort((a, b) => 
            {
                // 1. 사망자 처리 (죽은 사람은 뒤로)
                bool aAlive = a.currentHp > 0;
                bool bAlive = b.currentHp > 0;
                if (aAlive && !bAlive) return -1; // a 생존, b 사망 -> a가 앞
                if (!aAlive && bAlive) return 1;  // a 사망, b 생존 -> b가 앞
                if (!aAlive && !bAlive) return 0;

                // 2. 속도 계산 (AGI - Penalty)
                // Next나 Gun으로 인한 nextTurnSpeedPenalty가 여기서 반영.
                int speedA = a.GetTotalAgi() - a.nextTurnSpeedPenalty;
                int speedB = b.GetTotalAgi() - b.nextTurnSpeedPenalty;
                
                // 3. 속도 비교 (내림차순: 속도 높은 사람이 먼저)
                if (speedA != speedB) return speedB.CompareTo(speedA);

                // 4. 동점일 경우 행운(LUC) 비교
                return b.GetTotalLuc().CompareTo(a.GetTotalLuc());
            });

            // 정렬된 순서대로 UI 텍스트 갱신
            int orderCounter = 1;
            foreach (var player in activePlayers)
            {
                if (player.turnOrderText != null)
                {
                    if (player.currentHp > 0)
                    {
                        player.turnOrderText.gameObject.SetActive(true);
                        player.turnOrderText.text = orderCounter.ToString();
                        orderCounter++;
                    }
                    else
                    {
                        player.turnOrderText.gameObject.SetActive(false);
                    }
                }
            }
        }
        
        void Update()
        {
            if (!isBattleState) return;
            
            if (isAutoMode)
            {
                if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.LeftShift))
                {
                    if (!reserveAutoOff)
                    {
                        uiController.SetAutoButtonSelect();
                        reserveAutoOff = true;
                    }
                }
            }

            if (inputCooldown > 0) inputCooldown -= Time.deltaTime;

            if (state == BattleState.PlayerInput)
            {
                if (isAutoMode) return;
                Time.timeScale = 1.0f;

                if (uiController.IsItemUIVisible) return; 
                if (uiController.IsSkillUIVisible) return;

                if (isSelectingTarget)
                {
                    if (inputCooldown <= 0) HandleTargetSelectionInput();
                }
                else if (isSelectingMoveTarget)
                {
                    if (inputCooldown <= 0) HandleMoveSelectionInput();
                }
                else 
                {
                    if (inputCooldown <= 0) HandleCommandInput();
                }

                bool isPopupActive = uiController.IsCmdPanelVisible && (uiController.IsItemUIVisible || uiController.IsSkillUIVisible);
                
                if (!isSelectingTarget && !isSelectingMoveTarget && !isPopupActive)
                {
                    MaintainSelection();
                }
            }
        }

        void MaintainSelection()
        {
            if (EventSystem.current.currentSelectedGameObject != null)
            {
                if (EventSystem.current.currentSelectedGameObject != lastSelectedObject)
                    lastSelectedObject = EventSystem.current.currentSelectedGameObject;
            }
            else
            {
                if (lastSelectedObject != null && lastSelectedObject.activeInHierarchy)
                    EventSystem.current.SetSelectedGameObject(lastSelectedObject);
                else
                    EventSystem.current.SetSelectedGameObject(attackButton);
            }
        }

        // 인스턴트 전투 처리 메인 루틴
        IEnumerator ProcessInstantWin()
        {
            // 화면 번쩍임 효과
            yield return uiController.ShowFlashEffect();

            // 내부 시뮬레이션 (애니메이션 없이 계산만 수행)
            SimulateAutoBattleLogic();

            // 결과 텍스트 구성
            List<PlayerController> allPlayers = activePlayers.OfType<PlayerController>().ToList();
            
            BattleReward reward = CombatCalculator.CalculateRewards(allPlayers, encounterLog);
            foreach(var p in allPlayers)
            {
                if (p != null && p.currentHp > 0) {
                    p.ApplyExperience(reward.expPerMember); 
                }
            }
            InventoryManager.Instance.AddGold(reward.totalGold);
            foreach(var itemId in reward.dropItems) InventoryManager.Instance.AddItem(itemId, 1);
            
            SoundManager.Instance.PlaySFX(SfxID.Attack_Sword); // 타격음 한번 재생

            // 결과 표시
            yield return uiController.ShowInstantWinPanel(reward);

            uiController.HideInstantWinPanel();

            // 전투 종료 처리
            ClearCombatField(); 
            GameStateManager.Instance.ChangeState(GameState.Exploration);
        }

        // 인스턴트 킬 시뮬레이션 로직: 아군 선공으로 적이 전멸할 때까지 반복
        void SimulateAutoBattleLogic()
        {
            bool battleEnded = false;
            int safetyBreak = 0; // 무한 루프 방지

            while (!battleEnded && safetyBreak < 100)
            {
                safetyBreak++;

                // 1. 아군 공격 턴
                foreach (var player in activePlayers)
                {
                    if (player.currentHp <= 0) continue;

                    // 살아있는 적 중 하나 랜덤 타겟
                    var target = activeMonsters.FirstOrDefault(m => m.currentHp > 0);
                    if (target == null) 
                    {
                        battleEnded = true; 
                        break; 
                    }

                    // 데미지 계산 (기존 CalculateDamage 재활용)
                    // CombatAction을 가짜로 만들어서 전달
                    CombatAction fakeAction = new CombatAction(player.gameObject, target.gameObject, ActionType.Attack, 0);
                    BattleEntity pEntity = player.GetComponent<BattleEntity>();
                    BattleEntity tEntity = target.GetComponent<BattleEntity>();
                    int dmg = CombatCalculator.CalculateDamage(pEntity, tEntity, fakeAction, false, 1.0f);

                    // HP 즉시 차감 (애니메이션 함수 호출 X)
                    target.currentHp = Mathf.Max(0, target.currentHp - dmg);
                }

                if (battleEnded) break;

                // 2. 적군 반격 턴 (살아남은 적이 있다면)
                foreach (var monster in activeMonsters)
                {
                    if (monster.currentHp <= 0) continue;

                    var target = activePlayers.FirstOrDefault(p => p.currentHp > 0);
                    if (target == null) break;

                    CombatAction fakeAction = new CombatAction(monster.gameObject, target.gameObject, ActionType.Attack, 0);
                    BattleEntity mEntity = monster.GetComponent<BattleEntity>();
                    BattleEntity ptEntity = target.GetComponent<BattleEntity>();
                    int dmg = CombatCalculator.CalculateDamage(mEntity, ptEntity, fakeAction, false, 1.0f);
                    target.currentHp = Mathf.Max(0, target.currentHp - dmg);
                    // 아군 UI(HP바) 갱신이 필요하다면 여기서 호출하거나, 탐험 복귀 시 갱신됨
                }
            }
        }

        // 인스턴트 킬 조건 검사
        bool CheckInstantWinCondition()
        {
            // 앱이 설치되지 않았으면 패스
            if (!AppManager.Instance.IsInstalled(AppFeature.KillSwitch)) return false;
            // 아직 몬스터나 플레이어가 세팅되지 않았으면 패스
            if (activeMonsters.Count == 0 || activePlayers.Count == 0) return false;

            int pCount = activePlayers.Count;
            int mCount = activeMonsters.Count;

            // 조건 1: 적 그룹의 수가 아군보다 작아야 함
            if (mCount >= pCount) return false;

            // 조건 2: 적 평균 레벨 <= 아군 평균 레벨
            float pAvgLevel = (float)activePlayers.Average(p => ((PlayerController)p).level);
            float mAvgLevel = (float)activeMonsters.Average(m => m.level); 

            if (mAvgLevel > pAvgLevel) return false;

            return true;
        }

        private bool CheckUnionAttackCondition(PlayerController actor)
        {
            List<PlayerController> unionPartners = GetValidUnionPartners(actor);
            return !isUnionAttackUsedThisTurn && (unionPartners.Count >= 2);
        }

        private bool CheckLastStandCondition(PlayerController actor)
        {
            bool isFrontRow = (actor.columnIndex < 3);
            bool isFirstFrontRowInput = true;
            for (int i = 0; i < currentPlayerIndex; i++) {
                 if (activePlayers[i] is PlayerController prevPlayer) {
                    if (prevPlayer.currentHp > 0 && prevPlayer.columnIndex < 3) {
                        isFirstFrontRowInput = false; break;
                    }
                }
            }
            int frontLivingCount = allSlotControllers.Take(3).Count(p => p != null && !p.IsEmpty && p.currentHp > 0);
            
            return isFrontRow && isFirstFrontRowInput && (frontLivingCount == 3);
        }

        // Rolling Vulcan 발동 조건 검사
        bool CheckRollingVulcanCondition(PlayerController leader)
        {
            // 조건 3: 첫 번째 행동 지정 상태
            if (currentPlayerIndex != 0) return false;

            // 생존자 리스트 확인
            var livingPlayers = activePlayers.Where(p => p.currentHp > 0).ToList();
            int count = livingPlayers.Count;

            // 최소 인원 4명 이상이면 5명, 6명도 허용
            if (count < 4) return false;

            // 조건 1-2: 모든 생존자가 장비(gun_099) 및 탄환(Max) 확인
            foreach (var p in livingPlayers)
            {
                var pc = p as PlayerController;
                if (pc.equippedGunId != "gun_000") return false;
                if (pc.currentGun == null || pc.currentGunAmmo < pc.currentGun.maxHits) return false;
            }

            // 조건 2: "인접한 두 열"이 꽉 찼는지 확인 (사각형 형성 여부)
            // 0열(좌측), 1열(중앙), 2열(우측) 각각 전후열이 모두 찼는지 검사
            bool col0Full = IsSlotActive(0) && IsSlotActive(3); // 좌측 열 완성?
            bool col1Full = IsSlotActive(1) && IsSlotActive(4); // 중앙 열 완성?
            bool col2Full = IsSlotActive(2) && IsSlotActive(5); // 우측 열 완성?

            // Case A: 좌측 + 중앙 열이 꽉 참 (0열, 1열) -> 사각형 OK
            bool isLeftSquare = col0Full && col1Full;

            // Case B: 중앙 + 우측 열이 꽉 참 (1열, 2열) -> 사각형 OK
            bool isRightSquare = col1Full && col2Full;

            // 둘 중 하나라도 만족하면 조건 통과
            if (isLeftSquare || isRightSquare)
            {
                return true;
            }

            return false;
        }

        // [헬퍼 함수] 해당 슬롯 인덱스의 플레이어가 전투 가능한 상태인지 확인
        bool IsSlotActive(int index)
        {
            if (index < 0 || index >= allSlotControllers.Count) return false;
            PlayerController pc = allSlotControllers[index];
            
            // pc가 존재하고, 빈 슬롯이 아니며, 체력이 0보다 커야 함
            return pc != null && !pc.IsEmpty && pc.currentHp > 0;
        }

        // 메인 메뉴 버튼 갱신 및 순서 정렬
        void RefreshCommandButtons(PlayerController actor)
        {
            uiController.InitCommandButtons();
            
            // Skill 조건: 배운 스킬이 있어야 함
            bool canSkill = actor.learnedSkillIds.Count > 0;

            // Gun 메뉴 조건: 쏘거나 장전할 수 있어야 함
            bool canShoot = actor.CanShootGun() && actor.currentGunAmmo > 0;
            bool canReload = (actor.currentGun != null) && (actor.currentGunAmmo < actor.currentGun.maxHits);
            bool showGunMenu = canShoot || canReload;

            // Extra 메뉴 조건: 이동/방어/대기(항상 가능) 중 하나라도 가능하면
            bool canItem = (InventoryManager.Instance.GetAllItemIds().Count > 0);

            // Tactics 메뉴 조건: 협동 공격이나 배수진이나 롤링발칸이 가능해야 함
            bool canUnion = CheckUnionAttackCondition(actor);
            bool canLastStand = CheckLastStandCondition(actor);
            bool canRollingVulcan = CheckRollingVulcanCondition(actor);

            bool showTacticsMenu = canUnion || canLastStand || canRollingVulcan;

            // ---------------------------------------------------------
            // 3. 메인 메뉴 버튼 등록 (순서 중요: Attack > Skill > Gun > Extra > Tactics)
            // ---------------------------------------------------------
            
            // 메인 메뉴
            // 1. Attack
            AddButtonToActiveList(ActionType.Attack, true);
            // 2. Skill
            AddButtonToActiveList(ActionType.Skill, canSkill);
            // 3. Item
            AddButtonToActiveList(ActionType.Item, canItem);

            // 서브 메뉴
            // 4. Gun Menu ▶ (Shoot, Reload)
            AddButtonToActiveList(ActionType.Menu_Gun, showGunMenu);
            // 5. Extra Menu ▶ (Move, Guard, Next)
            AddButtonToActiveList(ActionType.Menu_Extra, true);
            // 6. Tactics Menu ▶ (Union, LastStand, RollingVulcan)
            AddButtonToActiveList(ActionType.Menu_Tactics, showTacticsMenu);
            
            // 7. Next
            AddButtonToActiveList(ActionType.Next, true);

            // ---------------------------------------------------------
            // 4. UI 갱신 준비
            // ---------------------------------------------------------
            cachedMainMenuButtons = new List<Button>(uiController.activeFightButtons);
            uiController.currentMenuButtons = uiController.activeFightButtons;
            isSubMenuActive = false;

            uiController.SetSubMenuVisible(false);
            uiController.SetFightCmdInteractable(true);
            uiController.ResizeMenuButtonContainer(uiController.currentMenuButtons.Count);
            
            currentFightBtnIndex = 0;
        }
        
        // 버튼 추가 헬퍼 함수
        void AddButtonToActiveList(ActionType type, bool isActive, string customLabel = null)
        {
            CommandButton cmdBtn = uiController.allFightButtons.Find(b => b.type == type);
            if (cmdBtn != null)
            {
                cmdBtn.gameObject.SetActive(isActive);
                if (isActive)
                {
                    // "▶" 라벨 등 텍스트 변경이 필요하면 여기서 처리
                    if (customLabel != null) 
                    {
                        TextMeshProUGUI btnText = cmdBtn.GetComponentInChildren<TextMeshProUGUI>();
                        if (btnText) btnText.text = customLabel;
                    }
                    
                    // 네비게이션 끄기
                    Button btn = cmdBtn.button;
                    Navigation nav = btn.navigation;
                    nav.mode = Navigation.Mode.None;
                    btn.navigation = nav;

                    uiController.activeFightButtons.Add(btn);
                }
            }
        }
        
        void HandleCommandInput()
        {
            // 1. 공통 취소/뒤로가기 입력
            if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.LeftShift))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                
                // 서브 메뉴가 열려있다면 메인 메뉴로 돌아감
                if (isSubMenuActive)
                {
                    CloseSubMenu();
                }
                else
                {
                    // 메인 메뉴라면 이전 캐릭터로
                    if (isFightMode)
                    {
                        if (currentPlayerIndex == 0) ShowBaseMenu();
                        else GoToPreviousPlayer();
                    }
                    else
                    {
                        if (actionQueue.Count > 0 || currentPlayerIndex > 0) GoToPreviousPlayer();
                    }
                }
                return;
            }
            
            // 왼쪽 키: 서브 메뉴 닫기 (취소와 동일 효과)
            if (isSubMenuActive && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                CloseSubMenu();
                return;
            }

            // 메뉴 네비게이션 처리
            if (isFightMode) 
            {
                // currentMenuButtons 리스트를 사용하여 네비게이션
                HandleMenuNavigation(uiController.currentMenuButtons, ref currentFightBtnIndex);
                
                // 오른쪽 키: 서브 메뉴 진입 (확인 키와 동일 효과 - 단, 메뉴 타입일 때만)
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    Button currentBtn = uiController.currentMenuButtons[currentFightBtnIndex];
                    CommandButton cmdBtn = currentBtn.GetComponent<CommandButton>();
                    
                    // 현재 포커스된 버튼이 '메뉴 진입용' 버튼이면 실행(진입)
                    if (cmdBtn.type == ActionType.Menu_Gun || 
                        cmdBtn.type == ActionType.Menu_Extra || 
                        cmdBtn.type == ActionType.Menu_Tactics)
                    {
                        currentBtn.onClick.Invoke();
                        return;
                    }
                }
            }
            else 
            {
                HandleMenuNavigation(uiController.baseButtons, ref currentBaseBtnIndex);
            }
        }

        // 서브 메뉴 진입
        void OpenSubMenu(ActionType menuType)
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;

            // 1. 메인 메뉴 인터랙션 비활성화
            uiController.SetFightCmdInteractable(false);

            // 2. 서브 메뉴 리스트 구성
            List<Button> subButtons = new List<Button>();
            float posY = -112f;
            if (menuType == ActionType.Menu_Gun)
            {
                // Shoot 버튼: 쏠 수 없으면 아예 비활성화
                bool canShoot = actor.CanShootGun() && actor.currentGunAmmo > 0;
                AddSubButton(ActionType.Shoot, canShoot, subButtons); 

                // Reload 버튼: 총이 있다면 항상 표시 및 활성화 (포커스 이동 위해)
                bool hasGun = (actor.currentGun != null);
                AddSubButton(ActionType.Reload, hasGun, subButtons);
            }
            else if (menuType == ActionType.Menu_Extra)
            {
                posY -= 32f;
                AddSubButton(ActionType.Move, true, subButtons);
                AddSubButton(ActionType.Guard, true, subButtons);
            }
            else if (menuType == ActionType.Menu_Tactics)
            {
                posY -= 64f;
                bool canUnion = CheckUnionAttackCondition(actor);
                bool canLastStand = CheckLastStandCondition(actor); 
                bool canRollingVulcan = CheckRollingVulcanCondition(actor);
                AddSubButton(ActionType.Union_Attack, canUnion, subButtons);
                AddSubButton(ActionType.Last_Stand, canLastStand, subButtons);
                AddSubButton(ActionType.Rolling_Vulcan, canRollingVulcan, subButtons);
            }

            // 3. 서브 메뉴 버튼들을 별도 패널로 이동 및 활성화
            uiController.SetSubMenuVisible(true);
            uiController.SetSubMenuButtons(subButtons, posY);

            // 4. 상태 전환
            uiController.currentMenuButtons = subButtons;
            isSubMenuActive = true;
            currentFightBtnIndex = 0;

            // 버튼들의 초기 색상 상태 갱신 (선택되지 않은 버튼들의 Grayout 처리)
            RefreshButtonVisuals(uiController.currentMenuButtons);
            if (uiController.currentMenuButtons.Count > 0) StartCoroutine(SelectButtonDelayed(uiController.currentMenuButtons, 0));
        }

        // 리스트 내 모든 버튼의 시각적 상태(텍스트 색상) 갱신
        void RefreshButtonVisuals(List<Button> buttons)
        {
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;
            
            foreach (var btn in buttons)
            {
                CommandButton cmdBtn = btn.GetComponent<CommandButton>();
                if (cmdBtn == null) continue;

                TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt == null) continue;

                // 1. 버튼의 사용 가능 여부 판별
                bool isUsable = IsCommandUsable(actor, cmdBtn.type);

                // 2. 현재 선택된 버튼인지 확인 (선택된 버튼은 UpdateSelection에서 처리하므로 여기서는 비선택 상태만)
                // 하지만 일관성을 위해 전체 적용 후 UpdateSelection이 덮어쓰도록 함.
                txt.color = isUsable ? colorNormal : colorGrayout;
            }
        }

        // 커맨드 사용 가능 여부 판별 로직
        bool IsCommandUsable(PlayerController actor, ActionType type)
        {
            switch (type)
            {
                case ActionType.Reload:
                    // 총이 있고, 탄환이 꽉 차지 않았을 때만 사용 가능
                    return (actor.currentGun != null) && (actor.currentGunAmmo < actor.currentGun.maxHits);
                
                case ActionType.Shoot:
                    return actor.CanShootGun() && actor.currentGunAmmo > 0;

                // 필요한 경우 다른 커맨드 조건도 추가
                default:
                    return true;
            }
        }

        // 서브 메뉴 버튼 추가 헬퍼
        void AddSubButton(ActionType type, bool isActive, List<Button> list)
        {
            CommandButton cmdBtn = uiController.allFightButtons.Find(b => b.type == type);
            if (cmdBtn != null)
            {
                cmdBtn.gameObject.SetActive(isActive);
                if (isActive) list.Add(cmdBtn.button);
            }
        }

        // 서브 메뉴 닫기 (메인 메뉴로 복귀)
        void CloseSubMenu()
        {
            inputCooldown = 0.2f;

            // 1. 서브 메뉴 버튼들 정리
            uiController.HideSubMenu();

            // 2. 메인 메뉴 활성화
            uiController.SetFightCmdInteractable(true);

            // 3. 상태 복구
            uiController.currentMenuButtons = cachedMainMenuButtons;
            isSubMenuActive = false;
            
            // 메인 메뉴 컨테이너(btnContainer) 리사이징 (복구)
            uiController.ResizeMenuButtonContainer(uiController.currentMenuButtons.Count);
            // 4. 인덱스 복구 및 포커스
            currentFightBtnIndex = lastMainIndex; 
            StartCoroutine(SelectButtonDelayed(uiController.currentMenuButtons, currentFightBtnIndex));
        }
        
        // 메인 메뉴 인덱스 기억용 변수
        private int lastMainIndex = 0;

        void HandleMenuNavigation(List<Button> currentList, ref int currentIndex)
        {
            if (currentList == null || currentList.Count == 0) return;
            bool changed = false;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                currentIndex = (currentIndex - 1 + currentList.Count) % currentList.Count;
                changed = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                currentIndex = (currentIndex + 1) % currentList.Count;
                changed = true;
            }

            if (changed) UpdateSelection(currentList, currentIndex);

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                // 버튼이 상호작용 가능할 때만 실행
                if (currentList[currentIndex].interactable)
                {
                    currentList[currentIndex].onClick.Invoke();
                }
                else
                {
                    // 비활성화된 버튼을 누르면 거부 사운드 재생
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                }
            }
        }

        void UpdateSelection(List<Button> list, int index)
        {
            if (list == null || list.Count == 0 || index < 0 || index >= list.Count) return;
            list[index].Select();
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
        }

        // [UI Buttons]
        public void OnBaseCommand_Fight()
        {
            isFightMode = true;
            uiController.SetBaseCmdVisible(false);
            uiController.SetFightCmdVisible(true);
            inputCooldown = 0.2f;
            currentFightBtnIndex = 0;
            StartCoroutine(SelectButtonDelayed(uiController.activeFightButtons, currentFightBtnIndex));
        }

        public void OnBaseCommand_Escape() 
        {
            currentEscapeAttempts++;
            Debug.Log($"도망 시도: {currentEscapeAttempts}/{guaranteedEscapeAttempts}"); 
            StartCoroutine(ProcessRunAttempt());
        }
        
        public void OnBaseCommand_Talk() { Debug.Log("대화하기 (미구현)"); }

        public void OnBaseCommand_Auto()
        {
            SoundManager.Instance.PlayBGM(BgmID.Normal_Battle);
            isAutoMode = true;
            reserveAutoOff = false; 
            uiController.SetAutoButtonVisible(true);
            Time.timeScale = 2.0f;

            uiController.SetBaseCmdVisible(false);
            uiController.SetFightCmdVisible(false);
            uiController.SetCmdPanelVisible(false);
            NextPlayerInput();
        }

        public void OnFightCommand_Menu_Gun() 
        { 
            inputCooldown = 0.2f;
            lastMainIndex = currentFightBtnIndex; // 현재 메인 메뉴 위치 기억
            OpenSubMenu(ActionType.Menu_Gun); 
        }
        
        public void OnFightCommand_Menu_Extra() 
        { 
            inputCooldown = 0.2f;
            lastMainIndex = currentFightBtnIndex;
            OpenSubMenu(ActionType.Menu_Extra); 
        }
        
        public void OnFightCommand_Menu_Tactics() 
        {
            inputCooldown = 0.2f;
            lastMainIndex = currentFightBtnIndex;
            OpenSubMenu(ActionType.Menu_Tactics); 
        }

        public void OnFightCommand_Attack()
        {
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;
            PrepareWeaponAction(actor.currentWeapon, ActionType.Attack);
        }

        public void OnFightCommand_shoot()
        {
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;
            if (!actor.CanShootGun())
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel); 
                uiController.ShowLog("CANNOT USE GUN");
                return;
            }
            PrepareWeaponAction(actor.currentGun, ActionType.Shoot);
        }

        public void OnFightCommand_Reload()
        {
            inputCooldown = 0.2f;
            PlayerController currentActor = activePlayers[currentPlayerIndex] as PlayerController;

            // 실행 차단: 이미 탄환이 가득 찬 경우
            if (currentActor.currentGun != null && currentActor.currentGunAmmo >= currentActor.currentGun.maxHits)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel); // 거부 효과음
                uiController.ShowLog("NO NEED TO RELOAD");
                StartCoroutine(HideLogAfterDelay(1.0f));
                return;
            }

            int speed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty; 

            CombatAction action = new CombatAction(
                currentActor.gameObject, 
                currentActor.gameObject, 
                ActionType.Reload,
                speed
            );

            actionQueue.Add(action);
            NextPlayerInput();
        }

        public void OnFightCommand_Next()
        {
            inputCooldown = 0.2f;
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);

            PlayerController currentActor = activePlayers[currentPlayerIndex] as PlayerController;

            // 이번 턴의 속도는 평소대로 계산 (현재 턴 순서는 이미 정해져 있으므로)
            int currentSpeed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty;
            currentActor.nextTurnSpeedPenalty = 0; // 페널티 초기화 (이번 턴 소모)

            // Next 액션 생성
            CombatAction action = new CombatAction(
                currentActor.gameObject, 
                currentActor.gameObject, 
                ActionType.Next, 
                currentSpeed
            );

            actionQueue.Add(action);
            
            // 다음 캐릭터 입력으로
            NextPlayerInput();
        }

        public void OnFightCommand_Skill()
        {
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;

            if (actor.learnedSkillIds.Count == 0)
            {
                uiController.ShowLog("NO SKILLS AVAILABLE");
            }
            else
            {
                uiController.ShowLog("CHOOSE A SKILL");
                uiController.SetFightCmdInteractable(false);
                uiController.ShowSkills(actor.learnedSkillIds, actor);
            } 
        }

        public void OnFightCommand_Item()
        {
            if (InventoryManager.Instance.GetAllItemIds().Count == 0)
            {
                uiController.ShowLog("NO ITEM AVAILABLE");
            }
            else
            {
                uiController.ShowLog("SELECT ITEM");
                uiController.SetFightCmdInteractable(false);
                uiController.ShowItems();
            }
        }

        public void OnPopupMenuClosed()
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
            currentFightBtnIndex = 0;
            uiController.SetFightCmdInteractable(true);
            StartCoroutine(SelectButton(attackButton)); 
        }

        

        public void OnPopupItemSelected(BaseRootData item)
        {
            currentSelectedItem = item;
            if (item is SkillData) currentSelectedAction = ActionType.Skill;
            else if (item is ConsumableItemData) currentSelectedAction = ActionType.Item;

            TargetScope scope = item.targetScope;

            // 대상을 직접 찍어야 하는 경우만 StartItemTargetSelection 호출
            if (scope == TargetScope.Single_Enemy || scope == TargetScope.One_Ally || scope == TargetScope.Dead_Ally || scope == TargetScope.Front_Single_Enemy)
            {
                StartItemTargetSelection(scope); 
            }
            else
            {
                // All_Allies, Self, Front_Enemies, All_Enemies 등은 대상 선택 없이 즉시 사용 예약
                
                // 아이템 선택 키 입력이 다음 턴의 명령 선택(Attack 등)으로 이어지지 않도록 쿨타임 부여
                inputCooldown = 0.2f; 

                // 이때 target은 null로 전달되지만, 수정한 HandleItemAction이 scope를 보고 대상을 찾음
                QueuePolymorphicAction(null); 
            }
        }

        void StartItemTargetSelection(TargetScope scope)
        {
            validTargets.Clear();
            if (scope == TargetScope.Single_Enemy)
                validTargets.AddRange(activeMonsters.Where(m => m != null && m.currentHp > 0));
            else if (scope == TargetScope.One_Ally) 
                validTargets.AddRange(activePlayers.Where(p => p != null && p.currentHp > 0));
            else if (scope == TargetScope.Dead_Ally)
                validTargets.AddRange(activePlayers.Where(p => p != null && p.currentHp <= 0));
            
            if (validTargets.Count == 0)
            {
                uiController.ShowLog("NO TARGET!");
                StartCoroutine(HideLogAfterDelay(1.0f));
                return; 
            }
            
            isSelectingTarget = true;

            // 현재 선택된 데이터 타입에 따라 분기
            if (currentSelectedItem is SkillData) 
                currentSelectedAction = ActionType.Skill;
            else 
                currentSelectedAction = ActionType.Item;
            
            currentTargetIndex = 0; 
            UpdateTargetHighlight();
            inputCooldown = 0.2f;

            uiController.SetFightCmdInteractable(false);
        }

        IEnumerator HideLogAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            uiController.HideLog();
        }

        void QueuePolymorphicAction(GameObject target)
        {
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;
            CombatAction action = new CombatAction(actor.gameObject, target, currentSelectedAction, actor.GetTotalAgi());
            action.itemData = currentSelectedItem; 
            if (currentSelectedItem is SkillData skill) action.skillData = skill;

            // 즉시 실행되는 행동(All_Allies, Self 등)도 Auto 모드를 위해 저장
            if (lastPlayerActions.ContainsKey(currentPlayerIndex))
                lastPlayerActions[currentPlayerIndex] = (currentSelectedAction, currentSelectedItem, target);
            else
                lastPlayerActions.Add(currentPlayerIndex, (currentSelectedAction, currentSelectedItem, target));

            actionQueue.Add(action);
            NextPlayerInput();
        }

        public void OnFightCommand_Guard()
        {
            inputCooldown = 0.2f;
            PlayerController currentActor = activePlayers[currentPlayerIndex] as PlayerController;
            int guardSpeed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty + 2000;
            currentActor.nextTurnSpeedPenalty = 0; 

            CombatAction action = new CombatAction(currentActor.gameObject, currentActor.gameObject, ActionType.Guard, guardSpeed);
            actionQueue.Add(action);
            NextPlayerInput();
        }

        public void OnFightCommand_Union_Attack()
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            PlayerController leader = activePlayers[currentPlayerIndex] as PlayerController;

            // 1. 참가자 확정 및 저장
            currentUnionParticipants = GetValidUnionPartners(leader);

            // 2. 참가자들 깜빡임 효과 (Visual Feedback)
            blinkTweens.Clear();
            foreach (var p in currentUnionParticipants)
            {
                // 이미지 알파값을 조절하여 깜빡임 (LoopType.Yoyo)
                p.SetHighlightColor(currentTargetColor);
                Image img = p.highlightImage;
                if (img)
                {
                    Tween t = img.DOFade(0.4f, 0.3f).SetLoops(-1, LoopType.Yoyo);
                    blinkTweens.Add(t);
                }
            }

            // 3. 타겟 선택 시작 (전열 몬스터만 선택 가능하게 하거나, 전체 선택)
            // 조건: "전열의 몬스터 타겟을 하나 선택"
            currentSelectedAction = ActionType.Union_Attack;
            StartUnionTargetSelection();
        }

        void StartUnionTargetSelection()
        {
            validTargets.Clear();
            // 전열 몬스터만 필터링
            validTargets = activeMonsters
                .Where(m => m.currentHp > 0 && m.transform.parent.parent == enemyFrontRowContainer)
                .ToList();

            if (validTargets.Count == 0)
            {
                // 전열이 없으면 전체 대상으로 확장? 아니면 불가능 메시지?
                // 여기서는 편의상 후열까지 포함하거나 메시지 출력
                uiController.ShowLog("NO ENEMIES IN THE FRONT LINE!");
                StartCoroutine(HideLogAfterDelay(1.0f));
                CancelUnionSelection(); // 취소 처리
                return;
            }

            isSelectingTarget = true;
            currentTargetIndex = 0;
            UpdateTargetHighlight();
            
            // UI 숨기기
            uiController.SetCmdPanelVisible(false);
            uiController.ShowLog("SELECT TARGET");
            inputCooldown = 0.2f;
        }

        // 취소 시 깜빡임 멈춤
        void CancelUnionSelection()
        {
            StopBlinkEffects();
            currentUnionParticipants.Clear();
            // ... 기존 취소 로직(CancelTargetSelection) 호출 ...
            CancelTargetSelection();
        }

        void StopBlinkEffects()
        {
            foreach (var t in blinkTweens) t.Kill(true); // 트윈 즉시 종료 및 원상복구
            blinkTweens.Clear();
            // 알파값 완전 복구
            foreach (var p in activePlayers) (p as PlayerController).ResetHighlightColor();
        }

        public void OnFightCommand_LastStand()
        {
            inputCooldown = 0.2f;
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            PlayerController leader = activePlayers[currentPlayerIndex] as PlayerController;

            CombatAction leaderAction = new CombatAction(leader.gameObject, leader.gameObject, ActionType.Last_Stand, 9999);
            actionQueue.Add(leaderAction);

            isLastStandInputMode = true;
            NextPlayerInput();
        }

        // 버튼 연결용 함수
        public void OnFightCommand_Rolling_Vulcan()
        {
            // 입력 쿨타임 추가하여 연타/중복 입력 방지
            inputCooldown = 0.2f;

            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            PlayerController leader = activePlayers[currentPlayerIndex] as PlayerController;

            // 1. 참가자 확정
            currentUnionParticipants = GetRollingVulcanParticipants();
            
            // 안전 장치
            if (currentUnionParticipants.Count < 4) 
            {
                Debug.LogWarning("Rolling Vulcan 조건 불충족: 참가자 부족");
                return;
            }

            // 2. 행동 생성 (속도 9999)
            CombatAction action = new CombatAction(leader.gameObject, null, ActionType.Rolling_Vulcan, 9999);
            
            // 3. 큐 등록 및 다음 입력으로
            actionQueue.Add(action);
            NextPlayerInput();
        }

        // Rolling Vulcan 참가자 선별 (사각형 형성 멤버만 추출)
        List<PlayerController> GetRollingVulcanParticipants()
        {
            List<PlayerController> participants = new List<PlayerController>();

            // 각 열의 전/후열이 모두 찼는지 확인
            bool col0Full = IsSlotActive(0) && IsSlotActive(3); // 좌측 열
            bool col1Full = IsSlotActive(1) && IsSlotActive(4); // 중앙 열
            bool col2Full = IsSlotActive(2) && IsSlotActive(5); // 우측 열

            // 사각형 형성 여부
            bool isLeftSquare = col0Full && col1Full; // 0,1열 (좌측 사각형)
            bool isRightSquare = col1Full && col2Full; // 1,2열 (우측 사각형)

            List<int> validIndices = new List<int>();

            // 우선순위: 6명(양쪽 모두) -> 왼쪽 -> 오른쪽
            if (isLeftSquare && isRightSquare)
            {
                // 6명 전원 참가
                validIndices.AddRange(new int[] { 0, 1, 2, 3, 4, 5 });
            }
            else if (isLeftSquare)
            {
                // 좌측 4명만 참가 (0, 1, 3, 4)
                validIndices.AddRange(new int[] { 0, 1, 3, 4 });
            }
            else if (isRightSquare)
            {
                // 우측 4명만 참가 (1, 2, 4, 5)
                validIndices.AddRange(new int[] { 1, 2, 4, 5 });
            }

            // 인덱스를 실제 캐릭터 객체로 변환
            foreach (int idx in validIndices)
            {
                if (idx < allSlotControllers.Count)
                {
                    var pc = allSlotControllers[idx];
                    if (pc != null && pc.currentHp > 0)
                        participants.Add(pc);
                }
            }

            return participants;
        }

        void ShowBaseMenu()
        {
            isFightMode = false; 
            uiController.SetFightCmdVisible(false);
            uiController.SetBaseCmdVisible(true);
            currentBaseBtnIndex = 0; 
            UpdateSelection(uiController.baseButtons, currentBaseBtnIndex);
        }

        void GoToPreviousPlayer()
        {
            // 더 이상 뒤로 갈 수 없으면 리턴
            if (actionQueue.Count == 0 && currentPlayerIndex <= 0) return;

            bool keepRemoving = true;

            // 반복문을 통해 '자동으로 스킵된 행동'들을 연쇄적으로 삭제
            while (keepRemoving && actionQueue.Count > 0)
            {
                // 1. 마지막 행동 확인
                int lastIndex = actionQueue.Count - 1;
                CombatAction lastAction = actionQueue[lastIndex];
                PlayerController actor = lastAction.actor.GetComponent<PlayerController>();

                // 2. 행동 삭제
                actionQueue.RemoveAt(lastIndex);

                // 3. 행동 타입에 따른 분기 처리
                
                // --- A. Union Attack 취소 ---
                if (lastAction.type == ActionType.Union_Attack)
                {
                    Debug.Log("Union Attack 원본 취소됨: 상태 초기화");
                    isUnionAttackUsedThisTurn = false;
                    currentUnionParticipants.Clear();
                    keepRemoving = false; // 원본을 지웠으니 정지
                }
                else if (lastAction.type == ActionType.Guard && currentUnionParticipants.Contains(actor))
                {
                    // Union 참가자의 자동 방어 -> 계속 뒤로
                    keepRemoving = true; 
                }
                
                // --- B. Last Stand 취소 [신규 추가] ---
                else if (lastAction.type == ActionType.Last_Stand)
                {
                    Debug.Log("Last Stand 원본 취소됨: 상태 초기화");
                    isLastStandInputMode = false; // 입력 스킵 모드 해제
                    keepRemoving = false; // 원본을 지웠으니 정지
                }
                else if (lastAction.type == ActionType.Guard && isLastStandInputMode && actor.columnIndex < 3)
                {
                    // Last Stand 모드 중 전열 캐릭터의 방어 -> 자동 스킵된 행동이므로 계속 뒤로
                    Debug.Log($"Last Stand로 스킵된 {actor.name}의 행동 삭제");
                    keepRemoving = true;
                }
                
                // --- C. 일반 행동 ---
                else
                {
                    keepRemoving = false; // 일반 행동 하나 지우고 정지
                }
            }

            // 4. 인덱스 재조정
            // 현재 큐에 남은 행동 수 - 1 위치로 이동 (NextPlayerInput에서 ++ 되므로)
            currentPlayerIndex = actionQueue.Count - 1;

            NextPlayerInput();
        }
        
        void NextPlayerInput()
        {
            ResetPlayerSlotHighlights();

            currentPlayerIndex++;
            if (currentPlayerIndex >= activePlayers.Count) { ProcessTurn(); return; }

            PlayerController currentPlayer = activePlayers[currentPlayerIndex] as PlayerController;
            if (currentPlayer.currentHp <= 0) { NextPlayerInput(); return; }

            // Union Attack / Rolling Vulcan 참가자 스킵 처리
            if (currentUnionParticipants.Contains(currentPlayer))
            {
                Debug.Log($"Union Attack 또는 Rolling Vulcan 참가로 {currentPlayer.name}의 턴 스킵");
                
                // 스킵 액션 주석처리                
                /* CombatAction skipAction = new CombatAction(currentPlayer.gameObject, currentPlayer.gameObject, ActionType.Guard, 0);
                actionQueue.Add(skipAction);
                */
                
                NextPlayerInput();
                return;
            }

            if (isLastStandInputMode && currentPlayer.columnIndex < 3)
            {
                CombatAction supportAction = new CombatAction(currentPlayer.gameObject, currentPlayer.gameObject, ActionType.Guard, currentPlayer.GetTotalAgi() + 2000);
                actionQueue.Add(supportAction);
                NextPlayerInput(); 
                return; 
            }

            RefreshCommandButtons(currentPlayer);
            currentPlayer.SetHighlightColor(currentTargetColor);

            if (isAutoMode)
            {
                ProcessAutoAction(currentPlayer);
                return; 
            }

            isSelectingTarget = false;
            uiController.SetCmdPanelVisible(true);
            uiController.ShowLog("WAITING...");
            uiController.SetTargetCursorVisible(false);
            currentPlayer.SetMessage("생각중...");
            
            if (isFightMode)
            {
                uiController.SetBaseCmdVisible(false);
                uiController.SetFightCmdVisible(true);
                currentFightBtnIndex = 0;
                StartCoroutine(SelectButtonDelayed(uiController.activeFightButtons, currentFightBtnIndex));
            }
            else
            {
                uiController.SetBaseCmdVisible(true);
                uiController.SetFightCmdVisible(false);
                currentBaseBtnIndex = 0;
                StartCoroutine(SelectButtonDelayed(uiController.baseButtons, currentBaseBtnIndex));
            }
        }

        IEnumerator SelectButtonDelayed(List<Button> list, int index)
        {
            yield return null; 
            if (list != null && list.Count > index)
            {
                EventSystem.current.SetSelectedGameObject(null);
                UpdateSelection(list, index);
            }
        }

        void ProcessAutoAction(PlayerController actor)
        {
            ActionType actionType = ActionType.Attack;
            BaseRootData autoData = null;
            GameObject autoTarget = null; // 저장된 타겟

            // 저장된 행동 불러오기
            if (lastPlayerActions.ContainsKey(currentPlayerIndex))
            {
                var info = lastPlayerActions[currentPlayerIndex];
                actionType = info.type;
                autoData = info.data; 
                autoTarget = info.target; // 타겟 복원
            }

            // 스코프 확인
            TargetScope scope = TargetScope.Front_Single_Enemy; 
            switch (actionType)
            {
                case ActionType.Attack: scope = (actor.currentWeapon != null) ? actor.currentWeapon.attackRange : TargetScope.Front_Single_Enemy; break;
                case ActionType.Shoot: scope = (actor.currentGun != null) ? actor.currentGun.attackRange : TargetScope.Front_Single_Enemy; break;
                case ActionType.Skill:
                case ActionType.Item: if (autoData != null) scope = autoData.targetScope; break;
            }

            // 타겟 결정
            GameObject finalTarget = null;

            // 아군 대상(회복/버프) 스코프인지 확인
            bool isAllyScope = (scope == TargetScope.One_Ally || scope == TargetScope.All_Allies || 
                                scope == TargetScope.Self || scope == TargetScope.Dead_Ally);

            if (isAllyScope)
            {
                // 아군 대상인 경우: 무조건 저장된 타겟(autoTarget) 사용
                // (One_Ally인 경우 지정했던 아군, All_Allies/Self인 경우 null 혹은 본인이 들어있음)
                finalTarget = autoTarget;
            }
            else
            {
                // 적 대상인 경우: 기존 로직대로 살아있는 몬스터 중 랜덤 선택
                // (공격 대상은 매번 바뀌거나 죽을 수 있으므로 랜덤이 일반적)
                List<BattleEntity> candidates = new List<BattleEntity>();
                var livingMonsters = activeMonsters.Where(m => m.currentHp > 0).ToList();
                bool targetFrontOnly = (scope == TargetScope.Front_Single_Enemy || scope == TargetScope.Random_Front_Enemy || scope == TargetScope.Front_Enemies);

                foreach (var m in livingMonsters)
                {
                    bool isFront = (m.transform.parent.parent == enemyFrontRowContainer);
                    if (targetFrontOnly && !isFront) continue;
                    candidates.Add(m);
                }

                if (candidates.Count > 0)
                {
                    finalTarget = candidates[Random.Range(0, candidates.Count)].gameObject;
                }
            }

            int speed = actor.GetTotalAgi() - actor.nextTurnSpeedPenalty;
            actor.nextTurnSpeedPenalty = 0;

            CombatAction action = new CombatAction(actor.gameObject, finalTarget, actionType, speed);
            action.itemData = autoData; 
            if (autoData is SkillData skill) action.skillData = skill;

            actionQueue.Add(action);
            NextPlayerInput();
        }

        IEnumerator SelectButton(GameObject btnToSelect)
        {
            yield return null; 
            EventSystem.current.SetSelectedGameObject(null);
            if (btnToSelect != null)
            {
                EventSystem.current.SetSelectedGameObject(btnToSelect);
                lastSelectedObject = btnToSelect;
            }
        }

        public void OnCommandButton_Move()
        {
            BattleEntity currentActor = activePlayers[currentPlayerIndex];
            isSelectingMoveTarget = true;
            
            uiController.SetCmdPanelVisible(false);
            uiController.ShowLog("CHOOSE YOUR PLACE");

            currentMoveSlotIndex = GetPlayerSlotIndex(currentActor.transform.parent);
            UpdateMoveCursor();
            RefreshMoveHighlights(currentMoveSlotIndex);
            inputCooldown = 0.2f;
            EventSystem.current.SetSelectedGameObject(null);
        }

        void HandleMoveSelectionInput()
        {
            bool moved = false;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { if (currentMoveSlotIndex % 3 > 0) { currentMoveSlotIndex--; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { if (currentMoveSlotIndex % 3 < 2) { currentMoveSlotIndex++; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { if (currentMoveSlotIndex >= 3) { currentMoveSlotIndex -= 3; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { if (currentMoveSlotIndex < 3) { currentMoveSlotIndex += 3; moved = true; } }

            if (moved)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                UpdateMoveCursor();
                RefreshMoveHighlights(currentMoveSlotIndex); 
            }

            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                CancelMoveSelection();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Click);
                BattleEntity currentActor = activePlayers[currentPlayerIndex];
                int myCurrentIndex = GetPlayerSlotIndex(currentActor.transform.parent);

                if (currentMoveSlotIndex == myCurrentIndex) { CancelMoveSelection(); return; }

                Transform targetSlot = GetPlayerSlotByIndex(currentMoveSlotIndex);
                int moveSpeed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty + 2000;
                currentActor.nextTurnSpeedPenalty = 0; 

                CombatAction action = new CombatAction(currentActor.gameObject, targetSlot.gameObject, ActionType.Move, moveSpeed);
                actionQueue.Add(action);

                isSelectingMoveTarget = false;
                
                uiController.SetTargetCursorVisible(false);
                ResetPlayerSlotHighlights();
                NextPlayerInput();
            }
        }

        int GetPlayerSlotIndex(Transform slot)
        {
            int index = playerFrontSlots.IndexOf(slot);
            if (index != -1) return index; 
            index = playerBackSlots.IndexOf(slot);
            if (index != -1) return index + 3; 
            return 0; 
        }

        Transform GetPlayerSlotByIndex(int index)
        {
            if (index < 0 || index >= 6) return null;
            if (index < 3) { if (index < playerFrontSlots.Count) return playerFrontSlots[index]; }
            else { int backIndex = index - 3; if (backIndex < playerBackSlots.Count) return playerBackSlots[backIndex]; }
            return null; 
        }

        void CancelMoveSelection()
        {
            isSelectingMoveTarget = false;
            currentFightBtnIndex = 0;
            ResetPlayerSlotHighlights();
            uiController.SetCmdPanelVisible(true);
            uiController.SetBaseCmdVisible(false);
            uiController.SetFightCmdVisible(true);

            uiController.ShowLog("WAITING...");

            (activePlayers[currentPlayerIndex] as PlayerController).SetHighlightColor(currentTargetColor);

            uiController.SetTargetCursorVisible(false);
            inputCooldown = 0.2f;
            StartCoroutine(SelectButton(attackButton)); 
        }

        void CancelTargetSelection()
        {
            isSelectingTarget = false;
            currentFightBtnIndex = 0;
            uiController.SetCmdPanelVisible(true);
            uiController.SetBaseCmdVisible(false);
            uiController.SetFightCmdVisible(true);
            uiController.SetTargetCursorVisible(false);
            uiController.ShowLog("WAITING...");

            (activePlayers[currentPlayerIndex] as PlayerController).SetHighlightColor(currentTargetColor);

            uiController.SetFightCmdInteractable(true);
            inputCooldown = 0.2f; 
            StartCoroutine(SelectButton(attackButton));
        }

        void UpdateMoveCursor()
        {
            Transform slot = GetPlayerSlotByIndex(currentMoveSlotIndex);
            uiController.SetTargetCursorVisible(true);
            uiController.SetTargetCursorPosition(slot.position + cursorOffset);
        }

        void ResetPlayerSlotHighlights()
        {
            foreach (PlayerController player in allSlotControllers)
            {
                player.SetMessage(string.Empty);
                player.ResetHighlightColor();
            } 
        }

        void RefreshMoveHighlights(int cursorSlotIndex)
        {
            ResetPlayerSlotHighlights();
            if (currentPlayerIndex < activePlayers.Count)
            {
                PlayerController sourcePlayer = activePlayers[currentPlayerIndex] as PlayerController;
                sourcePlayer.SetHighlightColor(moveSourceColor);
            }

            if (cursorSlotIndex < 0) return;
            Transform targetSlot = GetPlayerSlotByIndex(cursorSlotIndex);
            if (targetSlot != null)
            {
                PlayerController targetChar = targetSlot.GetComponentInChildren<PlayerController>();
                if (targetChar != null) targetChar.SetHighlightColor(currentTargetColor);
            }
        }

        // 회피 애니메이션
        IEnumerator ProcessDodgeAnimation(Transform targetTransform)
        {
            float direction = (Random.value > 0.5f) ? 1f : -1f;
            // DOPunchPosition: 지정된 값만큼 이동했다가 제자리로 복귀
            yield return targetTransform.DOPunchPosition(new Vector3(10.5f * direction, 0, 0), 0.3f, 1, 0).WaitForCompletion();
        }

        IEnumerator ProcessRunAttempt()
        {
            state = BattleState.Processing; 
            
            uiController.SetCmdPanelVisible(false);
            uiController.SetTargetCursorVisible(false);
            uiController.ShowLog("ESCAPE!");

            yield return wait10;

            if (CombatCalculator.CalculateEscapeSuccess(activePlayers, activeMonsters, currentEscapeAttempts, guaranteedEscapeAttempts))
            {
                SetEnemyVisualsActive(false);
                uiController.ShowMessage("휴~ 도망쳤다.");
                yield return wait10;
                uiController.ShowBattleEndAnimation(()=>{ GameStateManager.Instance.ChangeState(GameState.Exploration); });
            }
            else
            {
                uiController.ShowMessage("칙쇼!! 잡혀버렸다!");
                yield return wait10;
                uiController.HideMessage();
                actionQueue.Clear(); 
                ProcessTurn();
            }
        }

        void HandleTargetSelectionInput()
        {
            // 1. 취소 및 확정 입력 처리
            bool isCancel = (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape));
            if (isCancel || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (validTargets.Count > currentTargetIndex)
                {
                    validTargets[currentTargetIndex].SetSelectionState(false);
                    if (isCancel) 
                    {
                        if (currentSelectedAction == ActionType.Union_Attack)
                            CancelUnionSelection();
                        else
                            CancelTargetSelection();   
                    }
                    else 
                    {
                        OnTargetSelected(validTargets[currentTargetIndex]);
                    }
                }
                return;
            }

            // 2. 방향키 이동 로직
            BattleEntity currentEntity = validTargets[currentTargetIndex];
            
            // 타겟 그룹 판별 (플레이어 대상인지 몬스터 대상인지)
            Transform targetFrontContainer = (validTargets.Count > 0 && validTargets[0] is PlayerController) ? playerFrontRowContainer : enemyFrontRowContainer;
            
            // 현재 타겟이 전열에 있는지 확인
            bool isCurrentInFront = (currentEntity.transform.parent.parent == targetFrontContainer);

            // 현재 행(Row)과 다른 행(Row)의 타겟 리스트 분리
            var currentRowTargets = validTargets.Where(m => (m.transform.parent.parent == targetFrontContainer) == isCurrentInFront)
                                                .OrderBy(m => m.columnIndex).ToList();
            
            var otherRowTargets = validTargets.Where(m => (m.transform.parent.parent == targetFrontContainer) != isCurrentInFront)
                                              .OrderBy(m => m.columnIndex).ToList();

            BattleEntity nextEntity = null;
            bool moved = false;

            // 좌우 키: 같은 행 내에서 순환
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                int idx = currentRowTargets.IndexOf(currentEntity);
                idx--;
                if (idx < 0) idx = currentRowTargets.Count - 1; 
                nextEntity = currentRowTargets[idx];
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                int idx = currentRowTargets.IndexOf(currentEntity);
                idx++;
                if (idx >= currentRowTargets.Count) idx = 0;
                nextEntity = currentRowTargets[idx];
                moved = true;
            }
            // 상하 키: 행 교체 (같은 열 위치 유지)
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || 
                     Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                if (otherRowTargets.Count > 0)
                {
                    // 인덱스를 3으로 나눈 나머지(% 3)를 비교하여 같은 열(왼쪽끼리, 중앙끼리, 오른쪽끼리)을 우선적으로 찾음
                    int currentNormalizedCol = currentEntity.columnIndex % 3;

                    nextEntity = otherRowTargets
                        .OrderBy(t => Mathf.Abs((t.columnIndex % 3) - currentNormalizedCol))
                        .First();
                        
                    moved = true;
                }
            }
            
            // 3. 포커스 변경 적용
            if (moved && nextEntity != null)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                currentTargetIndex = validTargets.IndexOf(nextEntity);
                UpdateTargetHighlight();
            }
        }

        void UpdateTargetHighlight()
        {
            foreach (var monster in validTargets) monster.SetSelectionState(false);
            if (validTargets.Count > 0) validTargets[currentTargetIndex].SetSelectionState(true);
        }

        public void OnTargetSelected(BattleEntity targetEntity)
        {
            if (!isSelectingTarget) return;

            if (currentSelectedAction == ActionType.Union_Attack)
            {
                StopBlinkEffects();
            }

            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;

            // 타겟 정보(targetEntity.gameObject)까지 함께 저장
            if (lastPlayerActions.ContainsKey(currentPlayerIndex))
                lastPlayerActions[currentPlayerIndex] = (currentSelectedAction, currentSelectedItem, targetEntity.gameObject);
            else
                lastPlayerActions.Add(currentPlayerIndex, (currentSelectedAction, currentSelectedItem, targetEntity.gameObject));
            
            int finalSpeed = actor.GetTotalAgi() - actor.nextTurnSpeedPenalty;
            actor.nextTurnSpeedPenalty = 0;

            CombatAction action = new CombatAction(actor.gameObject, targetEntity.gameObject, currentSelectedAction, finalSpeed); 
            
            if (currentSelectedAction == ActionType.Union_Attack)
            {
                 action.speed = 9999; 
                 isUnionAttackUsedThisTurn = true;
            }
            if (currentSelectedAction == ActionType.Item || currentSelectedAction == ActionType.Skill)
            {
                action.itemData = currentSelectedItem; 
                
                // 스킬인 경우 추가 처리
                if (currentSelectedItem is SkillData skill)
                {
                    action.skillData = skill;
                }
                // 아이템 및 스킬 사용 딜레이 (TODO: 스킬의 위력이나 TargetScope에 따라 딜레이가 커지게 하자)
                action.speed += 500; 
            }

            actionQueue.Add(action);
            isSelectingTarget = false;
            uiController.SetTargetCursorVisible(false);
            targetEntity.SetSelectionState(false);
            uiController.SetFightCmdInteractable(true);
            NextPlayerInput();
        }

        void ProcessTurn()
        {
            SoundManager.Instance.PlayBGM(BgmID.Normal_Battle);
            state = BattleState.Processing; 
            
            uiController.SetCmdPanelVisible(false);
            uiController.HideLog();
            HideTurnOrderUI();

            actionQueue = actionQueue.OrderByDescending(x => x.speed).ToList();
            StartCoroutine(ExecuteActions());
        }

        void ProcessEnemyTurn()
        {
            if (CheckBattleEnd(out bool isWin)) { StartCoroutine(EndBattleRoutine(isWin)); return; }

            state = BattleState.EnemyInput; 
            actionQueue.Clear(); 

            List<BattleEntity> livingPlayers = activePlayers.Where(p => p.currentHp > 0).ToList();
            foreach (MonsterController monster in activeMonsters)
            {
                if (monster.currentHp <= 0) continue;
                
                CombatAction enemyAction = monster.ChooseAction(livingPlayers);
                if (enemyAction != null)
                {
                    enemyAction.speed = monster.GetTotalAgi() - monster.nextTurnSpeedPenalty;
                    monster.nextTurnSpeedPenalty = 0; 
                    actionQueue.Add(enemyAction);
                }
            }

            actionQueue = actionQueue.OrderByDescending(x => x.speed).ToList();
            StartCoroutine(ExecuteActions());
        }

        void HideTurnOrderUI()
        {
            foreach(var p in activePlayers) if(p.turnOrderText) p.turnOrderText.gameObject.SetActive(false);
            foreach(var m in activeMonsters) if(m.turnOrderText) m.turnOrderText.gameObject.SetActive(false);
        }

        IEnumerator ExecuteActions()
        {
            foreach (var action in actionQueue)
            {
                if (CheckBattleEnd(out bool isWin)) { StartCoroutine(EndBattleRoutine(isWin)); yield break; }

                bool isActorDead = false;
                if (action.actor == null || !action.actor.activeSelf) isActorDead = true;
                else if (action.actor.TryGetComponent(out IBattleTarget ib) && !ib.IsAlive) isActorDead = true;
                if (isActorDead) continue; 

                int delay = CalculateActionDelay(action);
                BattleEntity actorEntity = action.actor.GetComponent<BattleEntity>();
                if (actorEntity != null) actorEntity.nextTurnSpeedPenalty += delay; 

                yield return StartCoroutine(PerformAction(action));
            }

            if (state == BattleState.Processing) { yield return wait05; ProcessEnemyTurn(); }
            else if (state == BattleState.EnemyInput)
            {
                if (CheckBattleEnd(out bool win)) StartCoroutine(EndBattleRoutine(win));
                else PreparePlayerTurn(); 
            }
        }

        int CalculateActionDelay(CombatAction action)
        {
            int baseDelay = 0;
            switch (action.type)
            {
                case ActionType.Attack: baseDelay = 10; break;
                case ActionType.Shoot:    baseDelay = 15; break;
                case ActionType.Guard:  baseDelay = 0; break;
                case ActionType.Move:   baseDelay = 5; break;
                case ActionType.Item:   baseDelay = (action.itemData != null) ? action.itemData.actionDelay : 20; break;
                case ActionType.Skill:  baseDelay = (action.itemData != null) ? action.itemData.actionDelay : 30; break;
                case ActionType.Next:   baseDelay = -50; break;
            }
            return baseDelay;
        }

        bool CheckBattleEnd(out bool isWin)
        {
            isWin = false;
            bool allEnemiesDead = activeMonsters.TrueForAll(m => m.currentHp <= 0);
            if (allEnemiesDead) { isWin = true; return true; }

            bool allPlayersDead = activePlayers.TrueForAll(p => p.currentHp <= 0);
            if (allPlayersDead) { isWin = false; return true; }
            return false;
        }

        IEnumerator PerformAction(CombatAction action)
        {
            switch (action.type)
            {
                case ActionType.Item:       yield return HandleItemAction(action); break;
                case ActionType.Skill:      yield return HandleSkillAction(action); break;
                case ActionType.Guard:      yield return HandleGuardAction(action); break;
                case ActionType.Move:       yield return StartCoroutine(PerformMove(action)); break;

                case ActionType.Attack:
                case ActionType.Shoot:        yield return HandleAttackAction(action); break;
                case ActionType.Reload:     yield return HandleReloadAction(action); break;

                case ActionType.Union_Attack: yield return HandleUnionAttack(action); break;
                case ActionType.Last_Stand: yield return HandleLastStandAction(action); break;
                case ActionType.Rolling_Vulcan: yield return HandleRollingVulcan(action); break;

                case ActionType.Next:
                    uiController.ShowLog($"{action.actor.name} IS WATCHING FOR OPPORTUNITY...");
                    // 별도의 애니메이션 없이 짧게 대기
                    yield return wait05; 
                    break;
            }
            yield return wait01;
            uiController.HideLog();
        }

        IEnumerator HandleItemAction(CombatAction action)
        {
            BaseRootData item = action.itemData;

            // 아이템 소모 로직
            if (item is ConsumableItemData consumable)
            {
                if (!InventoryManager.Instance.UseItem(consumable.id))
                {
                    uiController.ShowLog($"NOT ENOUGH {item.dataName}");
                    yield return wait10;
                    yield break; 
                }
            }
            
            TargetScope scope = (item != null) ? item.targetScope : TargetScope.One_Ally;
            List<GameObject> targets = GetTargetsByScope(scope, action);

            uiController.ShowLog($"USE {item.dataName}");

            foreach (var targetObj in targets)
            {
                // 공격 계열 vs 보조 계열 분기 처리
                bool isAttack = item.effectType == EffectType.Special_Atk || item.effectType == EffectType.Magic_Atk;

                if (isAttack)
                {
                    // 공격: BattleManager의 데미지 공식 및 연출 사용
                    SoundManager.Instance.PlaySFX(SfxID.Attack_Magic);
                    visualController.SpawnVFX(VfxID.Magic, targetObj.transform.position);
                    
                    // 아이템의 고정 데미지(effectValue)를 그대로 줄지, 계산식을 탈지는 기획에 따라 다름
                    // 여기서는 ApplyDamage를 통해 피격 연출(OnDamageTaken)까지 연결
                    ApplyDamage(targetObj, item.effectValue, false);
                }
                else
                {
                    // 회복/보조: EffectManager에게 데이터 처리 위임
                    var battleTarget = targetObj.GetComponent<IBattleTarget>();
                    if (battleTarget != null)
                    {
                        bool success = EffectManager.Instance.ApplyEffect(battleTarget, item);
                        
                        if (success)
                        {
                            SoundManager.Instance.PlaySFX(SfxID.Attack_Magic); // 회복 사운드로 교체 필요
                            visualController.SpawnVFX(VfxID.Magic, targetObj.transform.position); // 회복 이펙트로 교체 필요
                        }
                    }
                }
            }
            
            yield return wait05;
        }

        IEnumerator HandleSkillAction(CombatAction action)
        {
            // 스킬 타겟 자동 변경 로직
            if (action.target == null || !IsAlive(action.target))
            {
                action.target = FindNearestLivingTarget(action.actor);
                if (action.target == null) yield break; 
            }

            SkillData skill = action.itemData as SkillData; 
            PlayerController actor = action.actor.GetComponent<PlayerController>();

            // 비용 지불 로직
            if (actor != null && skill != null)
            {
                if (skill.useHpCost)
                {
                    if (actor.currentHp <= skill.costValue) { /* 실패 처리 */ yield break; }
                    actor.currentHp -= skill.costValue;
                }
                else
                {
                    if (actor.currentMp < skill.costValue) { /* 실패 처리 */ yield break; }
                    actor.currentMp -= skill.costValue;
                }
            }
            
            TargetScope scope = (skill != null) ? skill.targetScope : TargetScope.Front_Single_Enemy;
            List<GameObject> targets = GetTargetsByScope(scope, action);

            uiController.ShowLog($"{action.actor.name}'S SKILL: {skill.dataName}");

            foreach (var targetObj in targets)
            {
                // 공격 계열 vs 보조 계열 분기
                bool isAttack = skill.effectType == EffectType.Special_Atk || skill.effectType == EffectType.Magic_Atk;

                if (isAttack)
                {
                    // 공격: BattleManager의 데미지 계산(속성, 방어력 등) 및 연출 사용
                    SoundManager.Instance.PlaySFX(SfxID.Attack_Magic);
                    visualController.SpawnVFX(VfxID.Magic, targetObj.transform.position);

                    // CalculateDamage를 통해 상성/방어력 계산 적용
                    // (스킬 위력은 skill.effectValue가 CalculateDamage 내부에서 참조됨)
                    BattleEntity attackerEntity = action.actor.GetComponent<BattleEntity>();
                    BattleEntity targetEntity = targetObj.GetComponent<BattleEntity>();

                    bool isCrit = CombatCalculator.CheckCritical(attackerEntity, targetEntity, action);
                    int dmg = CombatCalculator.CalculateDamage(attackerEntity, targetEntity, action, isCrit, 1.0f);
                    
                    ApplyDamage(targetObj, dmg, isCrit);
                }
                else
                {
                    // 회복/부활/보조: EffectManager 사용
                    var battleTarget = targetObj.GetComponent<IBattleTarget>();
                    if (battleTarget != null)
                    {
                        bool success = EffectManager.Instance.ApplyEffect(battleTarget, skill);
                        
                        if (success)
                        {
                            SoundManager.Instance.PlaySFX(SfxID.Attack_Magic);
                            visualController.SpawnVFX(VfxID.Magic, targetObj.transform.position);
                        }
                    }
                }
            }

            yield return wait05;
        }

        IEnumerator HandleGuardAction(CombatAction action)
        {
            SetGuardState(action.actor, true);
            uiController.ShowLog($"{action.actor.name} IS GUARDING...");
            yield return wait05;
            uiController.HideLog();
        }

        IEnumerator HandleUnionAttack(CombatAction action)
        {
            if (action.target == null || !IsAlive(action.target))
            {
                action.target = FindNearestLivingTarget(action.actor);
                if (action.target == null)
                {
                    Debug.Log("Union Attack 취소: 유효한 타겟 없음");
                    uiController.ShowLog("NO VALID TARGET!");
                    currentUnionParticipants.Clear(); 
                    yield return wait05;
                    yield break;
                }
            }

            // 1. 참가자 데이터 복원
            PlayerController leader = action.actor.GetComponent<PlayerController>();
            
            // GetValidUnionPartners를 다시 호출하지 않고, 
            // 입력 단계에서 확정된 currentUnionParticipants 리스트를 필터링하여 사용.
            // (자동으로 부여된 'Guard' 행동 때문에 재검증 시 탈락하는 문제를 방지)
            
            List<PlayerController> partners = new List<PlayerController>();
            
            if (currentUnionParticipants != null)
            {
                // 참가자 목록에서 살아있는 캐릭터만 추출
                partners = currentUnionParticipants
                    .Where(p => p != null && p.currentHp > 0)
                    .ToList();
            }

            // 안전 장치: 리더가 목록에 없으면 추가 (리더는 반드시 포함)
            if (!partners.Contains(leader) && leader.currentHp > 0)
            {
                partners.Add(leader);
            }
            
            // 파트너 부족 시 취소 (본인 포함 2명 이상이어야 함)
            if (partners.Count < 2) 
            {
                uiController.ShowLog("UNION ATTACK FAILED!");
                currentUnionParticipants.Clear(); 
                yield break;
            }

            // =========================================================
            // Next 대기 중인 파트너의 속도 보너스 상쇄 로직
            // =========================================================
            foreach (var p in partners)
            {
                if (p == leader) continue; 

                bool hasNextAction = actionQueue.Any(a => a.actor == p.gameObject && a.type == ActionType.Next);
                
                if (hasNextAction)
                {
                    p.nextTurnSpeedPenalty += 50;
                    Debug.Log($"[Union] {p.name}의 Next 속도 보너스 상쇄 (+50 적용)");
                }
            }
            // =========================================================

            uiController.ShowLog("UNION ATTACK!");
            SoundManager.Instance.PlaySFX(SfxID.Attack_Sword);

            // 2. 애니메이션: 타겟 앞으로 모이기
            GameObject target = action.target;
            Vector3 targetBasePos = target.transform.position;
            Vector3 rallyPoint = targetBasePos + new Vector3(0, -0.9f, 0); 

            Dictionary<PlayerController, Vector3> originPositions = new Dictionary<PlayerController, Vector3>();
            Sequence moveSeq = DOTween.Sequence();

            foreach (var p in partners)
            {
                originPositions[p] = p.transform.position; 
                Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), 0, 0);
                moveSeq.Join(p.transform.DOMove(rallyPoint + randomOffset, 0.3f).SetEase(Ease.OutBack));
            }
            yield return moveSeq.WaitForCompletion();

            // 3. 타격 및 데미지 계산
            visualController.SpawnVFX(VfxID.Slash, target.transform.position); 
            SoundManager.Instance.PlaySFX(SfxID.Attack_Sword);

            float critChance = 0.3f + (partners.Count * 0.1f);
            bool allSameAlign = partners.All(p => p.align == leader.align);
            if (allSameAlign) critChance += 0.3f;

            bool isCrit = Random.value < critChance;
            
            int totalStr = partners.Sum(p => p.GetTotalStr());
            float dmgMultiplier = 1.5f; 
            if (allSameAlign) dmgMultiplier = 2.0f; 

            BattleEntity leaderEntity = leader.GetComponent<BattleEntity>();
            BattleEntity targetEntity = target.GetComponent<BattleEntity>();
            int dmg = CombatCalculator.CalculateDamage(leaderEntity, targetEntity, action, isCrit, dmgMultiplier);
            dmg = Mathf.RoundToInt(dmg * (float)totalStr / leader.GetTotalStr()); 
            
            ApplyDamage(target, dmg, isCrit);
            
            yield return wait05;

            // 4. 복귀 (원래 자리로)
            Sequence returnSeq = DOTween.Sequence();
            foreach (var p in partners)
            {
                returnSeq.Join(p.transform.DOMove(originPositions[p], 0.3f).SetEase(Ease.OutQuad));
            }
            yield return returnSeq.WaitForCompletion();

            // =========================================================
            // 5. 타겟 위치에 따른 파티 포메이션 변경
            // =========================================================
            uiController.ShowLog("FORMATION CHANGING...");

            // 타겟 몬스터의 열(Column) 인덱스 확인
            // (Target이 몬스터가 아닐 경우, 기본값 1(Center)로 처리)
            int targetCol = 1; 
            MonsterController targetMonster = action.target.GetComponent<MonsterController>();
            if (targetMonster != null)
            {
                targetCol = targetMonster.columnIndex;
            }

            // 조건에 따른 진형 변경 실행
            yield return StartCoroutine(ApplyFormationChange(targetCol));
            
            // [핵심] 정상 종료 시 목록 초기화
            currentUnionParticipants.Clear();
        }

        // 타겟 위치에 따른 진형 변경 분기 처리
        IEnumerator ApplyFormationChange(int targetCol)
        {
            if (targetCol == 0) // 왼쪽 몬스터 공격
            {
                // 반시계 방향 회전 (Counter-Clockwise)
                Debug.Log("[Formation] Left Target -> Rotate CCW");
                yield return StartCoroutine(RotateParty(false));
            }
            else if (targetCol == 2) // 오른쪽 몬스터 공격
            {
                // 시계 방향 회전 (Clockwise)
                Debug.Log("[Formation] Right Target -> Rotate CW");
                yield return StartCoroutine(RotateParty(true));
            }
            else // 가운데(1) 또는 그 외
            {
                // 전열 3명 랜덤 셔플
                Debug.Log("[Formation] Center Target -> Shuffle Front Row");
                yield return StartCoroutine(ShuffleFrontRowOnly());
            }
        }

        // 6명 전체 회전 로직
        // 슬롯 배치: 전열(0,1,2), 후열(3,4,5)
        // 시각적 배치:
        // [0] [1] [2]  (Front)
        // [3] [4] [5]  (Back)
        IEnumerator RotateParty(bool clockwise)
        {
            // 새로운 순서를 담을 임시 배열
            PlayerController[] newOrder = new PlayerController[6];

            if (clockwise)
            {
                // 시계 방향 (0->1->2->5->4->3->0)
                newOrder[1] = allSlotControllers[0]; // 0 -> 1
                newOrder[2] = allSlotControllers[1]; // 1 -> 2
                newOrder[5] = allSlotControllers[2]; // 2 -> 5 (전열우측 -> 후열우측)
                newOrder[4] = allSlotControllers[5]; // 5 -> 4
                newOrder[3] = allSlotControllers[4]; // 4 -> 3
                newOrder[0] = allSlotControllers[3]; // 3 -> 0 (후열좌측 -> 전열좌측)
            }
            else
            {
                // 반시계 방향 (0->3->4->5->2->1->0)
                newOrder[3] = allSlotControllers[0]; // 0 -> 3 (전열좌측 -> 후열좌측)
                newOrder[4] = allSlotControllers[3]; // 3 -> 4
                newOrder[5] = allSlotControllers[4]; // 4 -> 5
                newOrder[2] = allSlotControllers[5]; // 5 -> 2 (후열우측 -> 전열우측)
                newOrder[1] = allSlotControllers[2]; // 2 -> 1
                newOrder[0] = allSlotControllers[1]; // 1 -> 0
            }

            // 변경 적용
            yield return StartCoroutine(ApplyPartyReorder(newOrder.ToList()));
        }

        // 전열(0,1,2)만 섞는 로직
        IEnumerator ShuffleFrontRowOnly()
        {
            // 현재 리스트 복사
            List<PlayerController> newOrderList = new List<PlayerController>(allSlotControllers);
            
            // 전열 인덱스(0,1,2)만 추출하여 섞기
            List<PlayerController> frontRow = new List<PlayerController>();
            for(int i=0; i<3; i++) frontRow.Add(allSlotControllers[i]);

            // Fisher-Yates Shuffle
            for (int i = 0; i < frontRow.Count; i++)
            {
                PlayerController temp = frontRow[i];
                int randomIndex = Random.Range(i, frontRow.Count);
                frontRow[i] = frontRow[randomIndex];
                frontRow[randomIndex] = temp;
            }

            // 섞인 결과를 다시 앞부분에 배치
            for(int i=0; i<3; i++)
            {
                newOrderList[i] = frontRow[i];
            }
            // 후열(3,4,5)은 그대로 유지

            yield return StartCoroutine(ApplyPartyReorder(newOrderList));
        }

        // [공통] 재배치 적용 및 애니메이션
        IEnumerator ApplyPartyReorder(List<PlayerController> newOrderedControllers)
        {
            Sequence shuffleSeq = DOTween.Sequence();

            for (int i = 0; i < 6; i++)
            {
                PlayerController pc = newOrderedControllers[i];
                
                // 목표 슬롯 결정
                Transform targetSlot = (i < 3) ? playerFrontSlots[i] : playerBackSlots[i - 3];
                
                // 데이터 갱신
                pc.columnIndex = i; 
                
                // 부모 변경 (WorldPositionStays=true로 하여 순간이동 방지)
                pc.transform.SetParent(targetSlot, true);
                
                // 이동 애니메이션 (LocalPosition 0으로 부드럽게 이동)
                shuffleSeq.Join(pc.transform.DOLocalMove(Vector3.zero, 0.5f).SetEase(Ease.InOutQuad));
            }
            
            // 메인 리스트 갱신
            allSlotControllers = newOrderedControllers;

            yield return shuffleSeq.WaitForCompletion();
            
            // UI 갱신
            ResetPlayerSlotHighlights();
        }

        // Last Stand 집결 애니메이션
        IEnumerator HandleLastStandAction(CombatAction action)
        {
            isLastStandActive = true; 
            uiController.ShowLog("LAST STAND!!");

            List<PlayerController> frontRowMembers = activePlayers
                .Where(p => p.currentHp > 0 && p.columnIndex < 3)
                .Select(p => p as PlayerController)
                .ToList();

            Sequence seq = DOTween.Sequence();

            foreach(var pc in frontRowMembers)
            {
                Vector3 targetPos = pc.transform.localPosition;
                if (pc.columnIndex % 3 == 0) targetPos += new Vector3(50f, 0, 0); 
                else if (pc.columnIndex % 3 == 2) targetPos += new Vector3(-50f, 0, 0);

                seq.Join(pc.transform.DOLocalMove(targetPos, 0.3f).SetEase(Ease.OutBack));
                
                visualController.SpawnVFX(VfxID.Guard, pc.transform.position); 
                pc.isGuarding = true; 
            }
            yield return seq.WaitForCompletion();
            
            yield return wait05;
            uiController.HideLog();
        }

        // Rolling Vulcan 실행 코루틴
        IEnumerator HandleRollingVulcan(CombatAction action)
        {
            var leader = action.actor.GetComponent<PlayerController>();
            int index = leader.columnIndex;
            leader.SetMessage("안되겠다! 롤링 발칸이다!");
            yield return wait10;
            foreach(PlayerController pc in activePlayers)
            {
                if (pc.columnIndex == index) pc.SetMessage(string.Empty);
                else pc.SetMessage("알았어! OK!!");
            }
            yield return wait10;
            
            ResetCharacterMessage();
            
            uiController.ShowMessage("롤링 발칸~~~!!");
            // SoundManager.Instance.PlaySFX(SfxID.Skill_Ultimate); 
            Color bgColor = uiController.GetBackgroundColor();
            
            // 1. 데이터 준비
            List<PlayerController> participants = currentUnionParticipants;
            int totalAmmo = participants.Sum(p => p.currentGunAmmo);

            // 2. 무지개 빛 효과 시작
            Coroutine rainbowRoutine = StartCoroutine(ProcessRainbowEffect(participants));

            // 3. 난사 시작
            float shotInterval = 0.08f; 
            
            for (int i = 0; i < totalAmmo; i++)
            {
                // 매 발사마다 살아있는 적 확인
                List<BattleEntity> enemies = activeMonsters.Where(m => m.currentHp > 0).ToList();
                
                // A. 적이 살아있을 때만 데미지 처리
                if (enemies.Count > 0)
                {
                    foreach (var enemy in enemies)
                    {
                        if (enemy.currentHp <= 0) continue;
                        
                        PlayerController shooter = participants[i % participants.Count];
                        BattleEntity enemyEntity = enemy.gameObject.GetComponent<BattleEntity>();
                        int dmg = CombatCalculator.CalculateGunDamage(shooter, enemyEntity, false);
                        
                        ApplyDamage(enemy.gameObject, dmg, false);
                        visualController.SpawnVFX(VfxID.Gun, enemy.transform.position);
                    }
                }
                
                // B. 효과음 및 애니메이션은 적 생존 여부와 무관하게 무조건 실행
                SoundManager.Instance.PlaySFX(SfxID.Attack_Gun);
                
                // 회전 대기
                yield return StartCoroutine(FastRotateParticipants(participants, true, shotInterval));
            }

            // 4. 마무리
            if (rainbowRoutine != null) StopCoroutine(rainbowRoutine);
            
            uiController.SetBackgroundColor(bgColor);

            foreach (var p in participants)
            {
                p.currentGunAmmo = 0;
                p.ResetHighlightColor(); 
            }
            
            currentUnionParticipants.Clear();
            uiController.HideMessage();
            yield return wait05;
        }

        // 롤링발칸 등에서 참여자들만 회전시키는 함수
        IEnumerator FastRotateParticipants(List<PlayerController> participants, bool clockwise, float duration)
        {
            // 1. 전체 슬롯의 시계 방향 순서 정의 (0 -> 1 -> 2 -> 5 -> 4 -> 3)
            int[] ringOrder = { 0, 1, 2, 5, 4, 3 };

            // 2. 현재 참여자들이 위치한 인덱스만 추출 (순서 유지)
            // 예: 4명(좌+중앙 열)인 경우 -> [0, 1, 4, 3] 추출됨
            List<int> currentIndices = new List<int>();
            foreach (int slotIdx in ringOrder)
            {
                // 해당 슬롯의 캐릭터가 참가자 명단에 있는지 확인
                if (slotIdx < allSlotControllers.Count)
                {
                    PlayerController pc = allSlotControllers[slotIdx];
                    if (participants.Contains(pc))
                    {
                        currentIndices.Add(slotIdx);
                    }
                }
            }

            // 만약 참여자가 1명 이하라면 회전 불필요
            if (currentIndices.Count < 2) yield break;

            // 3. 이동 목표 설정 (매핑)
            // Key: 캐릭터, Value: 이동할 목표 슬롯 인덱스
            Dictionary<PlayerController, int> moveMap = new Dictionary<PlayerController, int>();
            int count = currentIndices.Count;

            for (int i = 0; i < count; i++)
            {
                // 현재 슬롯의 주인
                int currentSlotIdx = currentIndices[i];
                PlayerController pc = allSlotControllers[currentSlotIdx];

                // 목표 슬롯 찾기
                // 시계 방향: 내 다음 순번의 슬롯으로 이동
                // 반시계 방향: 내 이전 순번의 슬롯으로 이동
                int nextIndex = clockwise ? (i + 1) : (i - 1);
                
                // 인덱스 보정 (Circular)
                if (nextIndex >= count) nextIndex = 0;
                if (nextIndex < 0) nextIndex = count - 1;

                int targetSlotIdx = currentIndices[nextIndex];
                moveMap.Add(pc, targetSlotIdx);
            }

            // 4. 데이터 갱신 및 애니메이션 실행
            // 데이터 꼬임 방지를 위해 리스트 복제본 생성
            List<PlayerController> nextAllSlots = new List<PlayerController>(allSlotControllers);
            Sequence seq = DOTween.Sequence();

            foreach (var kvp in moveMap)
            {
                PlayerController pc = kvp.Key;
                int targetIdx = kvp.Value;

                // A. 데이터 구조 상의 위치 변경 (임시 리스트에 기록)
                nextAllSlots[targetIdx] = pc;

                // B. 물리적 위치(부모) 및 인덱스 정보 변경
                Transform targetSlot = (targetIdx < 3) ? playerFrontSlots[targetIdx] : playerBackSlots[targetIdx - 3];
                pc.transform.SetParent(targetSlot, true);
                pc.columnIndex = targetIdx;

                // C. 애니메이션 (Duration 동안 이동)
                seq.Join(pc.transform.DOLocalMove(Vector3.zero, duration).SetEase(Ease.Linear));
            }

            // 5. 실제 데이터 리스트 교체
            allSlotControllers = nextAllSlots;

            yield return seq.WaitForCompletion();
        }

        // 무지개 색상 효과 코루틴
        IEnumerator ProcessRainbowEffect(List<PlayerController> players)
        {
            float globalHue = 0f;
            
            // [설정] 플레이어 간의 색상 차이 간격 (0.1 ~ 0.2)
            // 값이 클수록 알록달록해지고, 작으면 부드럽게 이어짐.
            float hueOffsetStep = 0.1f; 
            
            // [설정] 색상이 변하는 속도
            float speed = 1.0f;

            while (true)
            {
                // 시간 경과에 따라 기준 색상(globalHue)을 계속 변경
                globalHue += Time.deltaTime * speed;
                
                // 값이 계속 커지는 것을 방지 (0~1 사이 반복)
                if (globalHue > 1f) globalHue -= 1f;

                for (int i = 0; i < players.Count; i++)
                {
                    // 기준 색상(globalHue)에 "인덱스 * 간격"을 더해줌.
                    // 0번 플레이어: globalHue + 0
                    // 1번 플레이어: globalHue + 0.15
                    // 2번 플레이어: globalHue + 0.30 ...
                    float localHue = (globalHue + (i * hueOffsetStep)) % 1.0f;
                    
                    Color rainbow = Color.HSVToRGB(localHue, 1f, 1f); // 채도(S)와 명도(V)는 최대로
                    rainbow.a = 0.3f;
                    players[i].SetHighlightColor(rainbow);
                    uiController.SetBackgroundColor(rainbow * Color.gray);
                }
                yield return null;
            }
        }

        // 빠른 회전 (사격 간격에 맞춘 속도)
        IEnumerator FastRotateParty(bool clockwise, float duration)
        {
            // RotateParty 로직을 가져오되, DOTween 시간을 duration에 맞춤
            // 로직은 기존 RotateParty와 동일하게 배열 재배치
             PlayerController[] newOrder = new PlayerController[6];

            if (clockwise)
            {
                newOrder[1] = allSlotControllers[0];
                newOrder[2] = allSlotControllers[1];
                newOrder[5] = allSlotControllers[2];
                newOrder[4] = allSlotControllers[5];
                newOrder[3] = allSlotControllers[4];
                newOrder[0] = allSlotControllers[3];
            }

            // 위치 이동 (ApplyPartyReorder 변형)
            Sequence seq = DOTween.Sequence();
            for (int i = 0; i < 6; i++)
            {
                PlayerController pc = newOrder[i];
                Transform targetSlot = (i < 3) ? playerFrontSlots[i] : playerBackSlots[i - 3];
                
                pc.columnIndex = i;
                pc.transform.SetParent(targetSlot, true);
                
                // duration 만큼 빠르게 이동
                seq.Join(pc.transform.DOLocalMove(Vector3.zero, duration).SetEase(Ease.Linear));
            }
            allSlotControllers = newOrder.ToList();
            
            yield return seq.WaitForCompletion();
        }

        void SetGuardState(GameObject actor, bool state)
        {
            if (actor.TryGetComponent(out PlayerController pc)) pc.isGuarding = state;
            else if (actor.TryGetComponent(out MonsterController mc)) mc.isGuarding = state;
        }

        void ShowCharacterMessage(PlayerController pc, string msg)
        {
            if (pc == null) return;
            pc.SetMessage(msg);
        }

        // 장전 코루틴
        IEnumerator HandleReloadAction(CombatAction action)
        {
            PlayerController actor = action.actor.GetComponent<PlayerController>();
            if (actor != null && actor.currentGun != null)
            {
                // 탄환 최대치로 충전
                actor.currentGunAmmo = actor.currentGun.maxHits;
                
                ShowCharacterMessage(actor, "탄환 장전 완료!");
                // SoundManager.Instance.PlaySFX(SfxID.Reload); // 장전 효과음
                
                // 간단한 연출 위로 살짝 뛰기
                yield return actor.transform.DOLocalMoveY(10f, 0.2f).SetLoops(2, LoopType.Yoyo).WaitForCompletion();
                
                ShowCharacterMessage(actor, string.Empty);
            }
            yield return wait05;
        }

        IEnumerator HandleAttackAction(CombatAction action)
        {
            // =========================================================
            // 타겟 자동 변경(Retargeting) 로직
            // =========================================================
            // 타겟이 없거나 이미 죽은 상태라면?
            if (action.target == null || !IsAlive(action.target))
            {
                // 가장 가까운 살아있는 적을 찾는다
                GameObject newTarget = FindNearestLivingTarget(action.actor);

                if (newTarget != null)
                {
                    // 타겟 변경
                    action.target = newTarget;
                    // (선택사항) 로그에 타겟 변경 알림
                    // uiController.ShowLog("Target Changed!"); 
                }
                else
                {
                    // 더 이상 공격할 적이 없다면 공격 중단
                    yield break;
                }
            }
            // =========================================================

            GetWeaponInfo(action, out int minHits, out int maxHits, out TargetScope scope);
            bool isPlayer = (action.actor.GetComponent<PlayerController>() != null);
            bool isMonster = (action.actor.GetComponent<MonsterController>() != null);

            PlayerController pc = action.actor.GetComponent<PlayerController>();
            bool isGunAction = (action.type == ActionType.Shoot && pc != null);
            if (isGunAction)
            {
                // 현재 탄환보다 더 많이 쏠 수 없음
                maxHits = Mathf.Min(maxHits, pc.currentGunAmmo);
                // 최소 타격 수도 탄환 수에 맞춰 조정 (탄환이 1발이면 최소 1발)
                minHits = Mathf.Min(minHits, maxHits);
                
                Debug.Log($"[Gun] 잔여 탄환: {pc.currentGunAmmo}, 발사 가능: {maxHits}");
                
                // 탄환 부족 체크 및 메시지 출력
                if (pc.currentGunAmmo <= 0)
                {
                    Debug.Log($"[Auto] {action.actor.name}: 탄환 부족으로 사격 실패");
                    uiController.ShowLog($"{action.actor.name} 재장전 필요!");
                    ShowCharacterMessage(pc, "이런! 탄환이 부족해!");
                    
                    // 실패 효과음
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel); 

                    // 메시지를 읽을 시간을 주고 턴 종료 (공격 애니메이션 실행 X)
                    yield return wait10; 
                    ShowCharacterMessage(pc, string.Empty);
                    yield break; 
                }
            }
            else
            {
                pc?.SetMessage(Random.Range(0f, 1f) < 0.5f ? "오라오라!" : "흐이짜!");
            }

            string actStr = (action.type == ActionType.Shoot) ? "'S SHOOT!" : "'S SMASH!";
            uiController.ShowLog($"{action.actor.name}{actStr}");
            yield return wait10;

            pc?.SetMessage(string.Empty);
            
            // 등장 및 공격 모션 통합
            Vector3 originalPos = action.actor.transform.localPosition;
            Vector3 originalScale = action.actor.transform.localScale;

            // 1. 앞으로 나오기 / 커지기
            if (isMonster)
                yield return action.actor.transform.DOScale(originalScale * 1.2f, 0.15f).SetEase(Ease.OutQuad).WaitForCompletion();
            else
                yield return action.actor.transform.DOLocalMove(originalPos + new Vector3(0, 20f, 0), 0.15f).SetEase(Ease.OutQuad).WaitForCompletion();

            // 2. 타격 처리 (QTE or Auto)
            int currentHits = 0;
            int hitsPerformed = 0; // 실제로 수행한 타격 수 카운트

            if (isPlayer && !isAutoMode && maxHits > 0 && minHits < maxHits)
            {
                uiController.ShowQTESlider();

                float qteDuration = 2.0f; 
                float timer = 0f;
                uiController.ShowLog("READY!");
                ShowCharacterMessage(pc, "죽어!");
                int delay = CalculateActionDelay(action);
                while (timer < qteDuration && currentHits < maxHits)
                {
                    timer += Time.deltaTime;
                    uiController.UpdateQTESliderValue(1.0f - (timer / qteDuration));
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    {
                        List<GameObject> currentTargets = GetTargetsByScope(scope, action);
                        if (currentTargets.Count == 0) break;
                        foreach (var target in currentTargets) StartCoroutine(ProcessSingleHit(action, target));
                        currentHits++;
                        hitsPerformed++; // 실제 발사 수 증가
                        BattleEntity actorEntity = action.actor.GetComponent<BattleEntity>();
                        if (actorEntity) actorEntity.nextTurnSpeedPenalty += delay;
                        uiController.ShowLog($"SHOOT OUT! ({currentHits}/{maxHits})");
                        
                        SoundManager.Instance.PlaySFX(SfxID.Attack_Gun); 
                    }
                    yield return null; 
                }
                ShowCharacterMessage(pc, string.Empty);
                uiController.HideQTESlider();
                if (currentHits > 0) yield return wait01;
            }
            
            int autoHitCount = 0;
            if (!isPlayer || isAutoMode) 
            {
                // 랜덤 범위도 탄환 수 안에서 결정됨 (위에서 maxHits를 clamp 했으므로)
                autoHitCount = Random.Range(minHits, maxHits + 1);
            }
            else if (minHits - currentHits > 0) 
            {
                autoHitCount = minHits - currentHits;
            }
            
            for (int i = 0; i < autoHitCount; i++)
            {
                List<GameObject> currentTargets = GetTargetsByScope(scope, action);
                if (currentTargets.Count == 0) break; 
                foreach (var target in currentTargets)
                {
                    SoundManager.Instance.PlaySFX(SfxID.Attack_Gun); 
                    yield return StartCoroutine(ProcessSingleHit(action, target));
                }
                hitsPerformed++; // 실제 발사 수 증가
                yield return wait01;
                if (scope == TargetScope.Front_Enemies || scope == TargetScope.All_Enemies) break;
            }

            // 탄환 차감 적용
            if (isGunAction)
            {
                pc.currentGunAmmo -= hitsPerformed;
                if (pc.currentGunAmmo < 0) pc.currentGunAmmo = 0;
                Debug.Log($"[Gun] 사격 종료. 남은 탄환: {pc.currentGunAmmo}");
            }

            // 3. 복귀
            if (isMonster)
                yield return action.actor.transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutQuad).WaitForCompletion();
            else
                yield return action.actor.transform.DOLocalMove(originalPos, 0.15f).SetEase(Ease.OutQuad).WaitForCompletion();

            yield return wait01;
        }

        IEnumerator ProcessSingleHit(CombatAction action, GameObject target)
        {
            // 위치 보정 계산 호출
            CombatPosition atkPos = GetUnitPosition(action.actor);
            CombatPosition defPos = GetUnitPosition(target);
            WeaponType wType = WeaponType.Melee;
            
            BattleEntity attackerEntity = action.actor.GetComponent<BattleEntity>();
            BattleEntity targetEntity = target.GetComponent<BattleEntity>();
            PlayerController pActor = action.actor.GetComponent<PlayerController>();
            
            if (action.type == ActionType.Shoot || (pActor?.currentWeapon?.type == WeaponType.Gun)) 
                wType = WeaponType.Gun;
            
            CombatCalculator.GetPositionalModifiers(atkPos, defPos, wType, out float posDmgMult, out float posEvaBonus);

            if (CombatCalculator.CheckEvasion(attackerEntity, targetEntity, posEvaBonus))
            {
                Debug.Log($"{target.name} 회피!");
                if (targetEntity is PlayerController pc)
                {
                    pc.SetMessage("어림없지!");
                    yield return wait05;
                    pc.SetMessage(string.Empty);
                } 
                yield return StartCoroutine(ProcessDodgeAnimation(target.transform));
                yield break; 
            }

            if (CombatCalculator.CheckReflection(targetEntity, action.type))
            {
                uiController.ShowLog("REFLECT!");
                visualController.SpawnVFX(VfxID.Reflect, target.transform.position);
                int reflectDmg = CombatCalculator.CalculateDamage(attackerEntity, attackerEntity, action, false, 1.0f);
                ApplyDamage(action.actor, reflectDmg, false);
                if (targetEntity is PlayerController pc)
                {
                    pc.SetMessage("반사다!");
                    yield return wait05;
                    pc.SetMessage(string.Empty);
                } 
                yield break;
            }

            if (CombatCalculator.CheckAbsorption(targetEntity, action.type))
            {
                uiController.ShowLog("ABSORB!");
                visualController.SpawnVFX(VfxID.Absorb, target.transform.position);
                int absorbAmount = CombatCalculator.CalculateDamage(attackerEntity, targetEntity, action, false, 1.0f);
                if (targetEntity is PlayerController pc)
                {
                    pc.Recover(absorbAmount, 0);
                    pc.SetMessage("흡수해주마!");
                    yield return wait05;
                    pc.SetMessage(string.Empty);
                } 
                else if (targetEntity is MonsterController mc) mc.currentHp = Mathf.Min(mc.currentHp + absorbAmount, mc.maxHp);
                yield break; 
            }

            if (isLastStandActive && target.GetComponent<PlayerController>() != null)
            {
                List<PlayerController> defenders = activePlayers.Where(p => p.currentHp > 0 && p.columnIndex < 3).Select(p => p as PlayerController).ToList();
                if (defenders.Count > 0)
                {
                    
                    bool isCrit = CombatCalculator.CheckCritical(attackerEntity, targetEntity, action);
                    int originalDamage = CombatCalculator.CalculateDamage(attackerEntity, targetEntity, action, isCrit, posDmgMult);
                    int splitDamage = Mathf.Max(1, originalDamage / defenders.Count);
                    uiController.ShowLog("DEFENSE!");
                    foreach (var defender in defenders)
                    {
                        defender.SetMessage("막아!");
                        ApplyDamage(defender.gameObject, splitDamage, false);
                        visualController.SpawnVFX(VfxID.Guard, defender.transform.position);
                    }
                    yield return wait01;
                    
                    foreach(var defender in defenders) defender.SetMessage(string.Empty);
                    yield break; 
                }
            }

            bool isCritical = CombatCalculator.CheckCritical(attackerEntity, targetEntity, action);
            int damage = 0;

            if (action.type == ActionType.Shoot && pActor != null)
                damage = CombatCalculator.CalculateGunDamage(pActor, targetEntity, isCritical);
            else
                damage = CombatCalculator.CalculateDamage(attackerEntity, targetEntity, action, isCritical, posDmgMult);

            BattleEntity defenderEntity = target.GetComponent<BattleEntity>();
            if (defenderEntity != null && defenderEntity.isGuarding)
            {
                PlayerController defender = null;
                if (defenderEntity is PlayerController pc) {
                    defender = pc; 
                    pc.SetMessage("윽!");
                }
                visualController.SpawnVFX(VfxID.Guard, target.transform.position);
                yield return wait01;
                
                if (defender) defender.SetMessage(string.Empty);
            }
            else
            {
                var sfxId = SfxID.None;
                VfxID vfxID = VfxID.None;
                if (action.type == ActionType.Attack) { sfxId = SfxID.Attack_Sword; vfxID = VfxID.Slash; }
                else if (action.type == ActionType.Shoot) { sfxId = SfxID.Attack_Gun; vfxID = VfxID.Gun; }
                else if (action.type == ActionType.Skill) { sfxId = SfxID.Attack_Magic; vfxID = VfxID.Magic; }
                else if (action.type == ActionType.Item)
                {
                    if (action.itemData.effectType == EffectType.Special_Atk || action.itemData.effectType == EffectType.Magic_Atk)
                    { sfxId = SfxID.Attack_Magic; vfxID = VfxID.Magic; }
                }

                if (sfxId != SfxID.None) SoundManager.Instance.PlaySFX(sfxId);
                if (vfxID != VfxID.None) visualController.SpawnVFX(vfxID, target.transform.position);
                yield return wait01;
            }

            ApplyDamage(target, damage, isCritical);
        }

        void ApplyDamage(GameObject target, int damage, bool isCritical)
        {
            if (target == null || !target.activeInHierarchy) return;

            var entity = target.GetComponent<BattleEntity>();
            if (entity == null) return;

            // 몬스터인 경우에만 데미지 팝업 표시
            if (entity is MonsterController)
            {
                if (damagePopupPrefab != null && damagePopupContainer != null)
                {
                    GameObject popupObj = Instantiate(damagePopupPrefab, damagePopupContainer);
                    
                    popupObj.transform.position = target.transform.position; 
                    popupObj.transform.localPosition += new Vector3(0, 50f, 0); 
                    
                    float randomX = Random.Range(-20f, 20f);
                    popupObj.transform.localPosition += new Vector3(randomX, 0, 0);

                    var popupScript = popupObj.GetComponent<DamagePopupController>();
                    if (popupScript != null)
                    {
                        popupScript.Setup(damage, isCritical);
                    }
                }
            }

            entity.TriggerHitShake(isCritical); 
            StartCoroutine(entity.OnDamageTaken(damage)); 
        }

        List<GameObject> GetTargetsByScope(TargetScope scope, CombatAction action)
        {
            List<GameObject> targets = new List<GameObject>();
            var livingMonsters = activeMonsters.Where(m => m.currentHp > 0).ToList();
            var livingPlayers = activePlayers.Where(p => p.currentHp > 0).ToList(); // 아군 생존자

            // 1. 단일 지정 (이미 타겟이 정해진 경우)
            if (scope == TargetScope.Front_Single_Enemy || scope == TargetScope.Single_Enemy || 
                scope == TargetScope.One_Ally || scope == TargetScope.Dead_Ally)
            {
                if (action.target != null) targets.Add(action.target);
            }
            // 2. 적 랜덤 / 전체
            else if (scope == TargetScope.Random_Front_Enemy || scope == TargetScope.Random_Enemy)
            {
                List<GameObject> candidates = new List<GameObject>();
                foreach(var m in livingMonsters) 
                {
                    bool isFront = (m.transform.parent.parent == enemyFrontRowContainer);
                    if (scope == TargetScope.Random_Front_Enemy && !isFront) continue;
                    candidates.Add(m.gameObject);
                }
                if (candidates.Count > 0) targets.Add(candidates[Random.Range(0, candidates.Count)]);
            }
            else if (scope == TargetScope.Front_Enemies || scope == TargetScope.All_Enemies)
            {
                foreach(var m in livingMonsters) 
                {
                    bool isFront = (m.transform.parent.parent == enemyFrontRowContainer);
                    if (scope == TargetScope.Front_Enemies && !isFront) continue;
                    targets.Add(m.gameObject);
                }
                if (scope == TargetScope.Front_Enemies && targets.Count == 0) targets.AddRange(livingMonsters.Select(m => m.gameObject));
            }
            // 3. 아군 관련 타겟 (All_Allies, Self 등)
            else if (scope == TargetScope.All_Allies)
            {
                // 아군 전체 추가
                foreach (var p in livingPlayers) targets.Add(p.gameObject);
            }
            else if (scope == TargetScope.Self)
            {
                // 사용자 자신
                if (action.actor != null) targets.Add(action.actor);
            }
            
            return targets;
        }

        // [Union 참가 가능 파트너 찾기]
        List<PlayerController> GetValidUnionPartners(PlayerController leader)
        {
            List<PlayerController> partners = new List<PlayerController>(6);
            partners.Add(leader); // 리더(자기 자신) 포함

            // 리더가 전열(0, 1, 2)이 아니면 불가
            if (leader.columnIndex >= 3) return partners;

            // 현재 캐릭터의 왼쪽(-1), 오른쪽(+1) 이웃만 검사
            int[] neighborIndices = { leader.columnIndex - 1, leader.columnIndex + 1 };

            foreach (int i in neighborIndices)
            {
                // 인덱스 범위 체크 (전열 0~2)
                if (i < 0 || i > 2) continue;

                PlayerController p = allSlotControllers[i];

                // 1. 기본 상태 체크 (존재함, 빈 슬롯 아님, 살아있음)
                if (p == null || p.IsEmpty || p.currentHp <= 0) continue;

                // 2. 성향(Align) 호환성 체크
                if (!CombatCalculator.IsAlignCompatible(leader.align, p.align)) continue;

                // 3. 행동 예약 상태 체크. 이미 행동 큐에 등록된 행동이 있는지 확인
                bool isBusy = false;
                foreach(var action in actionQueue) {
                    // 조건: 행동이 아직 예약되지 않았거나(미행동), 예약된 행동이 'Next'인 경우만 가능
                    if (action.actor == p.gameObject && action.type != ActionType.Next) {
                        isBusy = true; break; 
                    }
                }
                if (!isBusy) partners.Add(p);
            }

            return partners;
        }

        void ResetCharacterMessage() { foreach(PlayerController pc in activePlayers) pc.SetMessage(string.Empty); }
        void GetWeaponInfo(CombatAction action, out int min, out int max, out TargetScope scope)
        {
            min = 1; max = 1; scope = TargetScope.Front_Single_Enemy; 
            var pActor = action.actor.GetComponent<PlayerController>();
            WeaponData weapon = null;
            if (pActor != null) weapon = (action.type == ActionType.Shoot) ? pActor.currentGun : pActor.currentWeapon;
            if (weapon != null) { min = weapon.minHits; max = weapon.maxHits; scope = weapon.attackRange; }
        }

        bool IsAlive(GameObject obj) { return obj != null && obj.activeSelf && (obj.GetComponent<IBattleTarget>()?.IsAlive ?? false); }

        // 아군 위치 이동(Move) 애니메이션
        IEnumerator PerformMove(CombatAction action)
        {
            PlayerController actor = action.actor.GetComponent<PlayerController>();
            if (actor == null || actor.currentHp <= 0) yield break;

            Transform targetSlotTransform = action.target.transform; 
            Transform originSlotTransform = actor.transform.parent;

            // (중략: Last Stand 체크 및 이동 불가 조건 로직)

            if (targetSlotTransform == originSlotTransform) yield break;

            PlayerController targetChar = targetSlotTransform.GetComponentInChildren<PlayerController>();
            uiController.ShowMessage((targetChar != null && !targetChar.IsEmpty) ? "위치 교대!" : "자리 이동!");

            // 1. 리스트(allSlotControllers) 내의 순서 교체
            int actorListIndex = allSlotControllers.IndexOf(actor);
            int targetListIndex = (targetChar != null) ? allSlotControllers.IndexOf(targetChar) : -1;

            if (actorListIndex != -1 && targetListIndex != -1)
            {
                allSlotControllers[actorListIndex] = targetChar;
                allSlotControllers[targetListIndex] = actor;
            }

            // 2. 물리적 부모 변경 및 인덱스 갱신
            if (targetChar != null) 
            { 
                targetChar.transform.SetParent(originSlotTransform, true); 
                targetChar.columnIndex = GetPlayerSlotIndex(originSlotTransform); 
            }
            actor.transform.SetParent(targetSlotTransform, true);
            actor.columnIndex = GetPlayerSlotIndex(targetSlotTransform);

            // =========================================================
            // 실제 데이터(RuntimeCharacterData) 동기화
            // =========================================================
            for (int i = 0; i < allSlotControllers.Count; i++)
            {
                PlayerController pc = allSlotControllers[i];
                if (pc != null && !pc.IsEmpty && pc.sourceData != null)
                {
                    // 인덱스 0,1,2는 전열(Front), 3,4,5는 후열(Back)
                    bool isFront = (i < 3);
                    pc.sourceData.row = isFront ? RowType.Front : RowType.Back;
                    
                    // 컬럼 값 계산 (Left=0, Center=1, Right=2)
                    pc.sourceData.column = (ColumnType)(isFront ? i : i - 3);
                }
            }
            // =========================================================

            SoundManager.Instance.PlaySFX(SfxID.UI_Click); 

            Sequence seq = DOTween.Sequence();
            seq.Join(actor.transform.DOLocalMove(Vector3.zero, 0.5f).SetEase(Ease.OutQuad));
            if (targetChar != null) seq.Join(targetChar.transform.DOLocalMove(Vector3.zero, 0.5f).SetEase(Ease.OutQuad));
            
            yield return seq.WaitForCompletion();
            yield return wait05; 
            uiController.HideMessage();
        }

        void InitializeSlots()
        {
            // 파괴되거나 null인 슬롯 참조를 리스트에서 제거
            frontSlots.RemoveAll(slot => slot == null);
            backSlots.RemoveAll(slot => slot == null);
            playerFrontSlots.RemoveAll(slot => slot == null);
            playerBackSlots.RemoveAll(slot => slot == null);

            if (frontSlots.Count == 0) CreateSlotsFor(enemyFrontRowContainer, frontSlots);
            if (backSlots.Count == 0) CreateSlotsFor(enemyBackRowContainer, backSlots);
            ClearSlotContents(frontSlots); 
            ClearSlotContents(backSlots);

            if (playerFrontSlots.Count == 0) CreateSlotsFor(playerFrontRowContainer, playerFrontSlots);
            if (playerBackSlots.Count == 0) CreateSlotsFor(playerBackRowContainer, playerBackSlots);
            ClearSlotContents(playerFrontSlots); 
            ClearSlotContents(playerBackSlots);
        }

        void CreateSlotsFor(Transform container, List<Transform> slotList)
        {
            foreach (Transform child in container) Destroy(child.gameObject);
            slotList.Clear();
            for (int i = 0; i < 3; i++)
            {
                GameObject slot = new GameObject($"Slot_{i}");
                slot.transform.SetParent(container, false);
                slot.AddComponent<RectTransform>();
                slotList.Add(slot.transform);
            }
        }

        void ClearSlotContents(List<Transform> slotList)
        {
            foreach (var slot in slotList) foreach (Transform child in slot) Destroy(child.gameObject);
        }

        void SpawnMonster(string id)
        {
            SoundManager.Instance.PlaySFX(SfxID.Encounter);
            var entry = monsterDB.GetEntry(id);
            if (entry == null) return;
            // 생성된 몬스터의 데이터를 로그에 기록 (보상 계산용)
            encounterLog.Add(entry);

            // 1. 선호하는 열(Row) 선택
            List<Transform> targetSlots = (entry.preferredRow == RowType.Front) ? frontSlots : backSlots;
            
            // 꽉 찼으면 다른 열로
            if (IsRowFull(targetSlots))
            {
                targetSlots = (targetSlots == frontSlots) ? backSlots : frontSlots;
                if (IsRowFull(targetSlots)) return; // 자리 없음
            }

            // 2. 빈 자리 찾기 (랜덤 또는 순차)
            // ColumnType에 맞춰 배치하려면 여기서 특정 인덱스를 선호하게 할 수 있음
            // 예: "Center 우선" 로직 등. 지금은 랜덤 빈자리 유지.
            List<int> emptyIndices = new List<int>();
            for (int i = 0; i < targetSlots.Count; i++) 
                if (targetSlots[i].childCount == 0) emptyIndices.Add(i);

            int randomIndex = emptyIndices[Random.Range(0, emptyIndices.Count)];
            Transform selectedSlot = targetSlots[randomIndex];

            // 3. 생성
            GameObject prefabToUse = (entry.prefab != null) ? entry.prefab : defaultMonsterPrefab;
            if (prefabToUse == null) return;

            GameObject newMonsterObj = Instantiate(prefabToUse, selectedSlot);
            newMonsterObj.transform.localPosition = Vector3.zero;

            MonsterController controller = newMonsterObj.GetComponentInChildren<MonsterController>();
            if (controller == null) { Destroy(newMonsterObj); return; }

            controller.Initialize(entry, this);
            newMonsterObj.name = $"{controller.sourceData.race} {controller.sourceData.name}";

            if (controller.currentHp <= 0) { Destroy(newMonsterObj); return; }

            // 몬스터 버튼의 자동 내비게이션 비활성화
            if (controller.selectButton != null)
            {
                Navigation nav = new Navigation();
                nav.mode = Navigation.Mode.None;
                controller.selectButton.navigation = nav;
            }

            // 배치된 위치 정보를 컨트롤러에 주입
            bool isFront = (targetSlots == frontSlots);
            
            controller.SetPositionInfo(randomIndex); // 기존 인덱스 설정
            
            // Enum 정보 설정
            controller.currentRow = isFront ? RowType.Front : RowType.Back;
            controller.currentColumn = (ColumnType)randomIndex; // 0, 1, 2 -> Left, Center, Right 매핑

            controller.SetRowAppearance(isFront); 
            controller.SetAnaglyphDepth(isFront); 
            
            activeMonsters.Add(controller);
        }

        bool IsRowFull(List<Transform> slots)
        {
            foreach (var slot in slots) if (slot.childCount == 0) return false; 
            return true; 
        }

        // 몬스터 전진 연출
        IEnumerator CheckAndMoveForward(MonsterController monster)
        {
            if (frontSlots.Contains(monster.transform.parent)) yield break;

            Transform myFrontSlot = frontSlots[monster.columnIndex];
            bool isSlotEmpty = (myFrontSlot.childCount == 0);

            if (!isSlotEmpty)
            {
                var frontMonster = myFrontSlot.GetChild(0).GetComponent<MonsterController>();
                if (frontMonster != null && frontMonster.currentHp <= 0)
                {
                    activeMonsters.Remove(frontMonster);
                    Destroy(frontMonster.gameObject);
                    isSlotEmpty = true; 
                }
            }

            if (isSlotEmpty)
            {
                Debug.Log($"[전진] {monster.sourceData.name} -> 전열 이동");
                monster.transform.SetParent(myFrontSlot);
                monster.SetAnaglyphDepth(true);

                Sequence seq = DOTween.Sequence();
                seq.Join(monster.transform.DOLocalMove(Vector3.zero, 0.5f).SetEase(Ease.OutQuad));
                seq.Join(monster.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutQuad));
                // 색상 보간
                Color startColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                seq.Join(DOVirtual.Color(startColor, Color.white, 0.5f, (c) => monster.SetColor(c)));

                yield return seq.WaitForCompletion();
            }
        }

        private void ClearCombatField()
        {
            activeMonsters.Clear();
            
            ClearSlotContents(frontSlots);
            ClearSlotContents(backSlots);
            
        }

        IEnumerator ProcessEnemyRowShift()
        {
            var backRowMonsters = activeMonsters.Where(m => backSlots.Contains(m.transform.parent)).OrderBy(m => m.columnIndex).ToList();
            foreach (MonsterController monster in backRowMonsters) yield return StartCoroutine(CheckAndMoveForward(monster));
        }

        GameObject FindNearestLivingTarget(GameObject attacker)
        {
            GameObject bestTarget = null;
            float closestDistance = float.MaxValue;
            Vector3 attackerPos = attacker.transform.position;

            if (attacker.GetComponent<PlayerController>() != null)
            {
                foreach (var monster in activeMonsters)
                {
                    if (monster != null && monster.currentHp > 0 && monster.gameObject.activeSelf)
                    {
                        float dist = Vector3.Distance(attackerPos, monster.transform.position);
                        if (dist < closestDistance) { closestDistance = dist; bestTarget = monster.gameObject; }
                    }
                }
            }
            else if (attacker.GetComponent<MonsterController>() != null)
            {
                foreach (var player in activePlayers)
                {
                    if (player != null && player.currentHp > 0 && player.gameObject.activeSelf)
                    {
                        float dist = Vector3.Distance(attackerPos, player.transform.position);
                        if (dist < closestDistance) { closestDistance = dist; bestTarget = player.gameObject; }
                    }
                }
            }
            return bestTarget;
        }

        CombatPosition GetUnitPosition(GameObject unit)
        {
            CombatPosition pos = new CombatPosition();
            if (unit.TryGetComponent(out PlayerController pc))
            {
                pos.isFrontRow = (pc.transform.parent.parent == playerFrontRowContainer);
                pos.columnIndex = pc.transform.parent.GetSiblingIndex();
            }
            else if (unit.TryGetComponent(out MonsterController mc))
            {
                pos.isFrontRow = (mc.transform.parent.parent == enemyFrontRowContainer);
                pos.columnIndex = mc.columnIndex; 
            }
            return pos;
        }

        IEnumerator EndBattleRoutine(bool isWin)
        {
            state = isWin ? BattleState.Won : BattleState.Lost;
            uiController.SetCmdPanelVisible(false);

            if (isWin)
            {
                SoundManager.Instance.PlayBGM(BgmID.Victory);
                
                List<PlayerController> allPlayers = activePlayers.OfType<PlayerController>().ToList();
                BattleReward reward = CombatCalculator.CalculateRewards(allPlayers, encounterLog);

                // 경험치 반영 전 상태 스냅샷 저장
                Dictionary<PlayerController, (int oldLv, int oldExp, int oldMaxExp)> preBattleStates = new Dictionary<PlayerController, (int, int, int)>();
                
                foreach(var pc in allPlayers)
                {
                    if (pc != null && pc.currentHp > 0) 
                    {
                        int oldLevel = pc.sourceData.stats.level;
                        int maxExp = CombatCalculator.GetMaxExpForLevel(oldLevel); // Spirit의 영향이 없는 원본 데이터의 Level을 사용함
                        preBattleStates.Add(pc, (oldLevel, pc.sourceData.currentExp, maxExp));
                    }
                }

                // 실제 데이터 반영 (PartyManager 데이터 수정)
                foreach(var p in allPlayers)
                {
                    if (p != null && p.currentHp > 0) {
                        p.ApplyExperience(reward.expPerMember); 
                    }
                }
                
                InventoryManager.Instance.AddGold(reward.totalGold);
                foreach(var itemId in reward.dropItems) InventoryManager.Instance.AddItem(itemId, 1);

                // 결과 UI 표시
                bool isResultClosed = false;
                uiController.ShowResult(reward, allPlayers, preBattleStates, ()=> isResultClosed = true);

                yield return new WaitUntil(() => isResultClosed);
            }
            else 
            {
                uiController.ShowMessage("패배는 너의 것!");
                yield return wait05;
            }

            // 종료 처리
            uiController.ShowBattleEndAnimation(()=>{GameStateManager.Instance.ChangeState(GameState.Exploration);});
        }

        private void ClearParty()
        {
            activePlayers.Clear();
        }
    }
}