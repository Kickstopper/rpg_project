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
        // 인스펙터에서 할당
        public BattleUIController uiController;
        public BattleFieldController fieldController;
        public BattleVisualController visualController; 
        public UI.Battle.LevelUpUI levelUpUI;
        public Transform damagePopupContainer;
        
        [Header("Escape Settings")]
        public int guaranteedEscapeAttempts = 3; // 몇 번째 시도부터 무조건 성공할지 설정
        private int currentEscapeAttempts = 0;   // 현재 전투에서의 시도 횟수
        private int currentFightBtnIndex = 0; // fight 메뉴용 인덱스
        private int currentBaseBtnIndex = 0;  // Base 메뉴용 인덱스

        // 메뉴 계층 관리 변수
        private bool isSubMenuActive = false; // 현재 서브 메뉴가 열려있는지
        
        private List<Button> cachedMainMenuButtons = new List<Button>(); // 메인 메뉴 버튼들을 임시 저장할 리스트

        [Header("Prefabs")]
        public GameObject defaultMonsterPrefab;
        public GameObject playerPrefab;
        public GameObject damagePopupPrefab;
        
        [Header("First Focus Buttons")]
        public GameObject baseFirstButton;    // Base 메뉴의 첫 버튼. 인스펙터 할당
        public GameObject attackButton;    // Fight 메뉴의 첫 버튼. 인스펙터 할당

        public Vector3 cursorOffset = new Vector3(0, 50, 0); // 몬스터 머리 위 오프셋

        
        //이동 모드 관련 변수
        private bool isSelectingMoveTarget = false;
        private int currentMoveSlotIndex = 0; // 0~2: 전열, 3~5: 후열

        [Header("Button Colors")]
        private Color colorNormal = Color.white; // 일반 텍스트
        private Color colorGrayout = Color.gray; // 사용 불가 텍스트

        private BaseRootData currentSelectedItem; // 현재 사용하려는 아이템
        private bool isAutoMode = false; // 오토 모드 활성화 여부
        // 오토 모드 종료 예약 플래그
        private bool reserveAutoOff = false;
        
        // 각 캐릭터가 마지막으로 수행한 행동 타입 저장
        private Dictionary<int, (ActionType type, BaseRootData data, GameObject target)> lastPlayerActions = new();
       
        public BattleState state;
        private List<BattleAction> actionQueue = new(); // 이번 턴의 모든 행동

        // 입력 제어용 변수
        private ActionType currentSelectedAction;
        private bool isSelectingTarget = false;

        private GameObject lastSelectedObject; // 마지막으로 선택된 UI 오브젝트를 기억하는 변수
        
        // 입력 중복 방지용 쿨타임
        private float inputCooldown = 0f;

        // "싸우다"를 선택했는지 여부
        private bool isFightMode = false;
        // 배수진(Last Stand) 활성화 플래그
        private bool isLastStandActive = false;
        private bool isLastStandInputMode = false; // isLastStandActive는 실행/데미지용, 이건 입력 스킵용

        // 유니온 어택 참가자 목록 (턴 스킵 및 애니메이션용)
        private List<PlayerController> currentUnionParticipants = new List<PlayerController>();
        private bool isUnionAttackUsedThisTurn = false;
        
        // 자주 쓰는 딜레이 캐싱
        private WaitForSeconds wait01 = new WaitForSeconds(0.1f);
        private WaitForSeconds wait05 = new WaitForSeconds(0.5f);
        private WaitForSeconds wait10 = new WaitForSeconds(1f);

        private bool isBattleState = false; // 현재 전투 상태인지 아닌지

        public struct BattleReward
        {
            public int totalExp;      // 파티가 획득한 총 경험치
            public int expPerMember;  // 개인당 돌아가는 경험치
            public int totalMoney;     // 획득한 총 골드
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
            // 전투 진입 시 UI를 일단 모두 숨김
            uiController.Initialize();
            fieldController.SetEnemyVisualsActive(false);
            
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

            fieldController.InitializeSlots();

            if (monsterIds == null || monsterIds.Count == 0) return;
            
            int maxSpawnLimit = Mathf.Min(monsterIds.Count, 6);
            int spawnCount = Random.Range(1, maxSpawnLimit + 1); 

            Debug.Log($"[Encounter] 몬스터 {spawnCount}마리가 출현합니다!");

            for (int i = 0; i < spawnCount; i++)
            {
                int randomIndex = Random.Range(0, monsterIds.Count);
                fieldController.SpawnMonster(monsterIds[randomIndex]);
            }
            
            fieldController.SpawnParty();

            if (fieldController.ActivePlayerCount() == 0)
            {
                GameStateManager.Instance.ChangeState(GameState.Exploration);
                return;  
            }  
            
            // 인스턴트 윈 조건 체크 및 분기
            if (CheckInstantWinCondition())
            {
                Debug.Log("조건 만족: 인스턴트 전투 실행");
                
                // 유닛들의 모습(Sprite)을 숨김
                fieldController.SetPlayerVisualsActive(false);
                StartCoroutine(ProcessInstantWin());
            }
            else
            {
                fieldController.SetPlayerVisualsActive(true);
                uiController.ShowBattleStartAnimation(()=> { 
                    StartCoroutine(SetupBattle()); 
                });
            }
        }

        IEnumerator SetupBattle()
        {
            SoundManager.Instance.PlayBGM(BgmID.Encounter);
            
            yield return fieldController.Refresh();
            PreparePlayerTurn();
        }

        private void PrepareWeaponAction(WeaponData weapon, ActionType actionType)
        {
            BattleEntity currentActor = fieldController.GetCurrentCharacter();
            TargetScope scope = TargetScope.Front_Single_Enemy; 
            
            if (weapon != null) scope = weapon.attackRange;
            else if (actionType == ActionType.Shoot) return; 

            if (scope == TargetScope.Front_Single_Enemy || scope == TargetScope.Single_Enemy)
            {
                var validTargets = fieldController.GetLivingMonsters();

                if (scope == TargetScope.Front_Single_Enemy)
                {
                    validTargets = validTargets.Where(m => m.transform.parent.parent == fieldController.enemyFrontRowContainer).ToList();
                    if (validTargets.Count == 0) validTargets = fieldController.GetLivingMonsters();
                }
                
                validTargets = validTargets.OrderBy(m => m.transform.parent.parent == fieldController.enemyBackRowContainer)
                                            .ThenBy(m => m.transform.position.x).ToList();

                if (validTargets.Count == 0) return;

                fieldController.SetValidTargets(validTargets);
                fieldController.currentTargetIndex = 0;
                fieldController.UpdateValidTargetsHighlight();
                
                currentSelectedAction = actionType;
                isSelectingTarget = true;
                
                uiController.SetCmdPanelVisible(false);
                uiController.ShowLog("SELECT TARGET");
                
                inputCooldown = 0.2f;
            }
            else
            {
                currentSelectedAction = actionType;
                int speed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty;
                currentActor.nextTurnSpeedPenalty = 0; 

                BattleAction action = new BattleAction(currentActor.gameObject, null, actionType, speed);
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
                foreach (var p in fieldController.activePlayers)
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
            fieldController.ResetPartyStatus();
            fieldController.ResetMonstersStatus();

            yield return fieldController.ProcessEnemyRowShift();
            yield return fieldController.ProcessPlayerRowShift();

            state = BattleState.PlayerInput;
            actionQueue.Clear(); 
            fieldController.currentPlayerIndex = -1; 
            isFightMode = false;

            fieldController.CalculateAndShowTurnOrder();
            NextPlayerInput();
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

            // 내부 시뮬레이션
            SimulateAutoBattleLogic();

            // 결과 텍스트 구성
            List<PlayerController> allPlayers = fieldController.GetPlayerControllers();
            BattleReward reward = BattleCalculator.CalculateRewards(allPlayers, fieldController.encounterLog);
            foreach(var p in allPlayers)
            {
                if (p != null && p.currentHp > 0) {
                    p.ApplyExperience(reward.expPerMember); 
                }
            }
            InventoryManager.Instance.AddMoney(reward.totalMoney);
            foreach(var itemId in reward.dropItems) InventoryManager.Instance.AddItem(itemId, 1);
            
            SoundManager.Instance.PlaySFX(SfxID.Attack_Sword); // 타격음 한번 재생

            // 결과 표시
            yield return uiController.ShowInstantWinPanel(reward);

            uiController.HideInstantWinPanel();

            // 전투 종료 처리
            fieldController.ClearMonsterField(); 
            GameStateManager.Instance.ChangeState(GameState.Exploration);
        }

        // 인스턴트 킬 시뮬레이션
        void SimulateAutoBattleLogic()
        {
            bool battleEnded = false;
            int safetyBreak = 0; // 무한 루프 방지

            while (!battleEnded && safetyBreak < 100)
            {
                safetyBreak++;

                // 아군 선공으로 적이 전멸할 때까지 반복
                foreach (var player in fieldController.activePlayers)
                {
                    if (player.currentHp <= 0) continue;

                    // 살아있는 적 중 하나 랜덤 타겟
                    var target = fieldController.activeMonsters.FirstOrDefault(m => m.currentHp > 0);
                    if (target == null) 
                    {
                        battleEnded = true; 
                        break; 
                    }

                    // 데미지 계산
                    BattleAction fakeAction = new BattleAction(player.gameObject, target.gameObject, ActionType.Attack, 0);
                    BattleEntity pEntity = player.GetComponent<BattleEntity>();
                    BattleEntity tEntity = target.GetComponent<BattleEntity>();
                    int dmg = BattleCalculator.CalculateDamage(pEntity, tEntity, fakeAction, false, 1.0f);

                    // 애니메이션 없이 HP 즉시 차감
                    target.currentHp = Mathf.Max(0, target.currentHp - dmg);
                }

                if (battleEnded) break;

                // 적군 반격 턴
                foreach (var monster in fieldController.activeMonsters)
                {
                    if (monster.currentHp <= 0) continue;

                    var target = fieldController.activePlayers.FirstOrDefault(p => p.currentHp > 0);
                    if (target == null) break;

                    BattleAction fakeAction = new BattleAction(monster.gameObject, target.gameObject, ActionType.Attack, 0);
                    BattleEntity mEntity = monster.GetComponent<BattleEntity>();
                    BattleEntity ptEntity = target.GetComponent<BattleEntity>();
                    int dmg = BattleCalculator.CalculateDamage(mEntity, ptEntity, fakeAction, false, 1.0f);
                    target.currentHp = Mathf.Max(0, target.currentHp - dmg);
                }
            }
        }

        // 인스턴트 킬 조건 검사
        bool CheckInstantWinCondition()
        {
            // 앱이 설치되지 않았으면 패스
            if (!AppManager.Instance.IsInstalled(AppFeature.KillSwitch)) return false;
            // 아직 몬스터나 플레이어가 세팅되지 않았으면 패스
            int mCount = fieldController.GetLivingMonsters().Count;
            int pCount = fieldController.GetLivingParty().Count;
            if (mCount == 0 || pCount == 0) return false;

            // 적 그룹의 수가 아군보다 작아야 함
            if (mCount >= pCount) return false;

            // 적 평균 레벨 <= 아군 평균 레벨
            float pAvgLevel = (float)fieldController.activePlayers.Average(p => ((PlayerController)p).level);
            float mAvgLevel = (float)fieldController.activeMonsters.Average(m => m.level); 

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
            for (int i = 0; i < fieldController.currentPlayerIndex; i++) {
                 if (fieldController.activePlayers[i] is PlayerController prevPlayer) {
                    if (prevPlayer.currentHp > 0 && prevPlayer.columnIndex < 3) {
                        isFirstFrontRowInput = false; break;
                    }
                }
            }
            int frontLivingCount = fieldController.GetFrontLivingCharacterCount();
            
            return isFrontRow && isFirstFrontRowInput && (frontLivingCount == 3);
        }

        // Rolling Vulcan 발동 조건 검사
        public bool CheckRollingVulcanCondition(PlayerController leader)
        {
            if (fieldController.currentPlayerIndex != 0) return false;

            // 생존자 리스트 확인
            var livingPlayers = fieldController.GetLivingParty();
            int count = livingPlayers.Count;

            // 최소 인원 4명 이상이면 5명, 6명도 허용
            if (count < 4) return false;

            // 모든 참여자의 장비 및 탄환 상태 확인
            foreach (var p in livingPlayers)
            {
                var pc = p as PlayerController;
                if (pc.equippedGunId != "gun_001") return false;
                if (pc.currentGun == null || pc.currentGunAmmo < pc.currentGun.maxHits) return false;
            }

            // 인접한 두 열이 꽉 찼는지, 각각 전후열이 모두 찼는지 체크
            bool col0Full = fieldController.IsSlotActive(0) && fieldController.IsSlotActive(3); // 좌측 열 완성?
            bool col1Full = fieldController.IsSlotActive(1) && fieldController.IsSlotActive(4); // 중앙 열 완성?
            bool col2Full = fieldController.IsSlotActive(2) && fieldController.IsSlotActive(5); // 우측 열 완성?

            // 좌측 + 중앙 열이 꽉 참 (0열, 1열)
            bool isLeftSquare = col0Full && col1Full;

            // 중앙 + 우측 열이 꽉 참 (1열, 2열)
            bool isRightSquare = col1Full && col2Full;

            // 둘 중 하나라도 만족하면 조건 통과
            if (isLeftSquare || isRightSquare)
            {
                return true;
            }

            return false;
        }

        // 메인 메뉴 버튼 갱신 및 순서 정렬
        void RefreshCommandButtons(PlayerController actor)
        {
            uiController.InitCommandButtons();

            // Skill 조건. 배운 스킬이 있고, Silence 제약이 걸리지 않아야 함
            bool hasSilence = actor.activeEffects.Exists(e => e.data.restrictionType == RestrictionType.Silence);
            bool canSkill = actor.learnedSkillIds.Count > 0 && !hasSilence;

            // Item 조건
            bool canItem = (InventoryManager.Instance.GetAllItemIds().Count > 0);

            // Gun 메뉴 조건
            bool canShoot = actor.CanShootGun() && actor.currentGunAmmo > 0;
            bool canReload = (actor.currentGun != null) && (actor.currentGunAmmo < actor.currentGun.maxHits);
            bool showGunMenu = canShoot || canReload;

            // Tactics 메뉴 조건
            bool canUnion = CheckUnionAttackCondition(actor);
            bool canLastStand = CheckLastStandCondition(actor);
            bool canRollingVulcan = CheckRollingVulcanCondition(actor);

            bool showTacticsMenu = canUnion || canLastStand || canRollingVulcan;
            
            // 메인 메뉴 버튼 등록
            // Attack
            AddButtonToActiveList(ActionType.Attack, true);
            // Skill
            AddButtonToActiveList(ActionType.Skill, canSkill);
            // Item
            AddButtonToActiveList(ActionType.Item, canItem);

            // 서브 메뉴 버튼 등록
            // Gun Menu ▶ (Shoot, Reload)
            AddButtonToActiveList(ActionType.Menu_Gun, showGunMenu);
            // Extra Menu ▶ (Move, Guard, Next)
            AddButtonToActiveList(ActionType.Menu_Extra, true);
            // Tactics Menu ▶ (Union, LastStand, RollingVulcan)
            AddButtonToActiveList(ActionType.Menu_Tactics, showTacticsMenu);
            
            // Next
            AddButtonToActiveList(ActionType.Next, true);
            
            // UI 갱신 준비
            cachedMainMenuButtons = new List<Button>(uiController.activeFightButtons);
            uiController.currentMenuButtons = uiController.activeFightButtons;
            isSubMenuActive = false;

            uiController.SetSubMenuVisible(false);
            uiController.SetFightCmdInteractable(true);
            uiController.ResizeMenuButtonContainer(uiController.currentMenuButtons.Count);
            
            currentFightBtnIndex = 0;
        }
        
        // 버튼 추가
        void AddButtonToActiveList(ActionType type, bool isActive, string customLabel = null)
        {
            CommandButton cmdBtn = uiController.allFightButtons.Find(b => b.type == type);
            if (cmdBtn != null)
            {
                cmdBtn.gameObject.SetActive(isActive);
                if (isActive)
                {
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
                        if (fieldController.currentPlayerIndex == 0) ShowBaseMenu();
                        else GoToPreviousPlayer();
                    }
                    else
                    {
                        if (actionQueue.Count > 0 || fieldController.currentPlayerIndex > 0) GoToPreviousPlayer();
                    }
                }
                return;
            }
            
            // 서브 메뉴 닫기. 취소 키와 동일
            if (isSubMenuActive && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                CloseSubMenu();
                return;
            }

            if (isFightMode) 
            {
                HandleMenuNavigation(uiController.currentMenuButtons, ref currentFightBtnIndex);
                
                // 서브 메뉴 진입. 확인 키와 동일
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    Button currentBtn = uiController.currentMenuButtons[currentFightBtnIndex];
                    CommandButton cmdBtn = currentBtn.GetComponent<CommandButton>();
                    
                    // 현재 포커스된 버튼이 '메뉴 진입용' 버튼이면 서브 메뉴를 연다
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
            PlayerController actor = fieldController.GetCurrentCharacter() as PlayerController;

            // 메인 메뉴 인터랙션 비활성화
            uiController.SetFightCmdInteractable(false);

            // 서브 메뉴 리스트 구성
            List<Button> subButtons = new List<Button>();
            float posY = -112f;
            if (menuType == ActionType.Menu_Gun)
            {
                // 쏠 수 없으면 Shoot 버튼은 비활성화
                bool canShoot = actor.CanShootGun() && actor.currentGunAmmo > 0;
                AddSubButton(ActionType.Shoot, canShoot, subButtons); 

                // 총이 있다면 Reload 버튼 항상 표시
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

            // 서브 메뉴 버튼들을 별도 패널로 이동 및 활성화
            uiController.SetSubMenuVisible(true);
            uiController.SetSubMenuButtons(subButtons, posY);

            // 상태 전환
            uiController.currentMenuButtons = subButtons;
            isSubMenuActive = true;
            currentFightBtnIndex = 0;

            // 선택되지 않은 버튼들의 비활성화 처리
            RefreshButtonVisuals(uiController.currentMenuButtons);
            if (uiController.currentMenuButtons.Count > 0) StartCoroutine(SelectButtonDelayed(uiController.currentMenuButtons, 0));
        }

        // 리스트 내 모든 버튼의 텍스트 컬러 갱신
        void RefreshButtonVisuals(List<Button> buttons)
        {
            PlayerController actor = fieldController.GetCurrentCharacter() as PlayerController;
            
            foreach (var btn in buttons)
            {
                CommandButton cmdBtn = btn.GetComponent<CommandButton>();
                if (cmdBtn == null) continue;

                TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt == null) continue;

                // 버튼의 사용 가능 여부 판별
                bool isUsable = IsCommandUsable(actor, cmdBtn.type);

                // 현재 선택된 버튼인지 확인
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

                default:
                    return true;
            }
        }

        // 서브 메뉴 버튼 추가
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

            // 서브 메뉴 버튼들 정리
            uiController.HideSubMenu();

            // 메인 메뉴 활성화
            uiController.SetFightCmdInteractable(true);

            // 상태 복구
            uiController.currentMenuButtons = cachedMainMenuButtons;
            isSubMenuActive = false;
            
            // 메인 메뉴 컨테이너 사이즈 복구
            uiController.ResizeMenuButtonContainer(uiController.currentMenuButtons.Count);
            // 인덱스 복구 및 포커스
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
                if (currentList[currentIndex].interactable)
                {
                    currentList[currentIndex].onClick.Invoke();
                }
                else
                {
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
            PlayerController actor = fieldController.GetCurrentCharacter();
            PrepareWeaponAction(actor.currentWeapon, ActionType.Attack);
        }

        public void OnFightCommand_shoot()
        {
            PlayerController actor = fieldController.GetCurrentCharacter();
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
            PlayerController currentActor = fieldController.GetCurrentCharacter();

            // 이미 탄환이 가득 찬 경우
            if (currentActor.currentGun != null && currentActor.currentGunAmmo >= currentActor.currentGun.maxHits)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                uiController.ShowLog("NO NEED TO RELOAD");
                StartCoroutine(HideLogAfterDelay(1.0f));
                return;
            }

            int speed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty; 

            BattleAction action = new BattleAction(
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

            PlayerController currentActor = fieldController.GetCurrentCharacter();

            // 이번 턴의 속도는 평소대로 계산 (현재 턴 순서는 이미 정해져 있으므로)
            int currentSpeed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty;
            currentActor.nextTurnSpeedPenalty = 0; // 페널티 초기화 (이번 턴 소모)

            // Next 액션 생성
            BattleAction action = new BattleAction(
                currentActor.gameObject, 
                currentActor.gameObject, 
                ActionType.Next, 
                currentSpeed
            );

            actionQueue.Add(action);
            NextPlayerInput();
        }

        public void OnFightCommand_Skill()
        {
            PlayerController actor = fieldController.GetCurrentCharacter();

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
            if (scope == TargetScope.Single_Enemy || scope == TargetScope.One_Ally || scope == TargetScope.Dead_Ally || scope == TargetScope.Front_Single_Enemy)
            {
                // 대상을 직접 찍어야 하는 경우만 StartItemTargetSelection 호출
                StartItemTargetSelection(scope); 
            }
            else
            {
                inputCooldown = 0.2f; 
                // All_Allies, Self, Front_Enemies, All_Enemies 등은 대상 선택 없이 즉시 사용 예약
                // 이때 target은 null로 전달되지만, 수정한 HandleItemAction이 scope를 보고 대상을 찾음
                QueuePolymorphicAction(null); 
            }
        }

        void StartItemTargetSelection(TargetScope scope)
        {
            fieldController.SetValidTargetsByTargetScope(scope);
            if (fieldController.validTargets.Count == 0)
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
            
            fieldController.currentTargetIndex = 0; 
            fieldController.UpdateValidTargetsHighlight();
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
            PlayerController actor = fieldController.GetCurrentCharacter();
            BattleAction action = new BattleAction(actor.gameObject, target, currentSelectedAction, actor.GetTotalAgi());
            action.itemData = currentSelectedItem; 
            if (currentSelectedItem is SkillData skill) action.skillData = skill;

            // 즉시 실행되는 행동(All_Allies, Self 등)도 Auto 모드를 위해 저장
            if (lastPlayerActions.ContainsKey(fieldController.currentPlayerIndex))
                lastPlayerActions[fieldController.currentPlayerIndex] = (currentSelectedAction, currentSelectedItem, target);
            else
                lastPlayerActions.Add(fieldController.currentPlayerIndex, (currentSelectedAction, currentSelectedItem, target));

            actionQueue.Add(action);
            NextPlayerInput();
        }

        public void OnFightCommand_Guard()
        {
            inputCooldown = 0.2f;
            PlayerController currentActor = fieldController.GetCurrentCharacter();
            int guardSpeed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty + 2000;
            currentActor.nextTurnSpeedPenalty = 0; 

            BattleAction action = new BattleAction(currentActor.gameObject, currentActor.gameObject, ActionType.Guard, guardSpeed);
            actionQueue.Add(action);
            NextPlayerInput();
        }

        public void OnFightCommand_Union_Attack()
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            PlayerController leader = fieldController.GetCurrentCharacter();

            currentUnionParticipants = GetValidUnionPartners(leader);

            fieldController.ShowBlinkHighlight(currentUnionParticipants);

            currentSelectedAction = ActionType.Union_Attack;
            StartUnionTargetSelection();
        }

        void StartUnionTargetSelection()
        {
            fieldController.SetValidMonsterTargets();

            if (fieldController.validTargets.Count == 0)
            {
                uiController.ShowLog("NO ENEMIES IN THE FRONT LINE!");
                StartCoroutine(HideLogAfterDelay(1.0f));
                CancelUnionSelection();
                return;
            }

            isSelectingTarget = true;
            fieldController.currentTargetIndex = 0;
            fieldController.UpdateValidTargetsHighlight();
            
            // UI 숨기기
            uiController.SetCmdPanelVisible(false);
            uiController.ShowLog("SELECT TARGET");
            inputCooldown = 0.2f;
        }

        void CancelUnionSelection()
        {
            fieldController.StopBlinkEffects();
            currentUnionParticipants.Clear();
            CancelTargetSelection();
        }

        public void OnFightCommand_LastStand()
        {
            inputCooldown = 0.2f;
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            PlayerController leader = fieldController.GetCurrentCharacter();

            BattleAction leaderAction = new BattleAction(leader.gameObject, leader.gameObject, ActionType.Last_Stand, 9999);
            actionQueue.Add(leaderAction);

            isLastStandInputMode = true;
            NextPlayerInput();
        }

        public void OnFightCommand_Rolling_Vulcan()
        {
            inputCooldown = 0.2f;

            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            PlayerController leader = fieldController.GetCurrentCharacter();

            // 참가자 정하기
            currentUnionParticipants = fieldController.GetRollingVulcanParticipants();
            
            if (currentUnionParticipants.Count < 4) 
            {
                Debug.LogWarning("Rolling Vulcan 조건 불충족: 참가자 부족");
                return;
            }

            // 행동 생성 (속도를 9999로 해서 무조건 최초 발동시킴)
            BattleAction action = new BattleAction(leader.gameObject, null, ActionType.Rolling_Vulcan, 9999);
            
            actionQueue.Add(action);
            NextPlayerInput();
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
            if (actionQueue.Count == 0 && fieldController.currentPlayerIndex <= 0) return;

            bool keepRemoving = true;

            // 스킵된 행동들 삭제
            while (keepRemoving && actionQueue.Count > 0)
            {
                // 마지막 행동 확인
                int lastIndex = actionQueue.Count - 1;
                BattleAction lastAction = actionQueue[lastIndex];
                PlayerController actor = lastAction.actor.GetComponent<PlayerController>();

                // 행동 삭제
                actionQueue.RemoveAt(lastIndex);

                // Union Attack 취소
                if (lastAction.type == ActionType.Union_Attack)
                {
                    Debug.Log("Union Attack 원본 취소됨: 상태 초기화");
                    isUnionAttackUsedThisTurn = false;
                    currentUnionParticipants.Clear();
                    keepRemoving = false;
                }
                else if (lastAction.type == ActionType.Guard && currentUnionParticipants.Contains(actor))
                {
                    keepRemoving = true; 
                }
                
                // Last Stand 취소
                else if (lastAction.type == ActionType.Last_Stand)
                {
                    Debug.Log("Last Stand 원본 취소됨: 상태 초기화");
                    isLastStandInputMode = false; // 입력 스킵 모드 해제
                    keepRemoving = false;
                }
                else if (lastAction.type == ActionType.Guard && isLastStandInputMode && actor.columnIndex < 3)
                {
                    Debug.Log($"Last Stand로 스킵된 {actor.name}의 행동 삭제");
                    keepRemoving = true;
                }
                else
                {
                    keepRemoving = false; // 일반 행동 하나 지우고 정지
                }
            }

            // 인덱스 재조정
            // 현재 큐에 남은 행동 수 - 1 위치로 이동 (NextPlayerInput에서 ++ 되므로)
            fieldController.currentPlayerIndex = actionQueue.Count - 1;

            NextPlayerInput();
        }
        
        void NextPlayerInput()
        {
            fieldController.ResetPlayerSlotHighlights();

            fieldController.currentPlayerIndex++;
            if (fieldController.currentPlayerIndex >= fieldController.activePlayers.Count) { ProcessTurn(); return; }

            PlayerController currentPlayer = fieldController.GetCurrentCharacter();
            if (currentPlayer.currentHp <= 0) { NextPlayerInput(); return; }

            // 제약 조건 체크
            RestrictionType restriction = currentPlayer.CheckActionRestriction();

            if (restriction == RestrictionType.SkipTurn)
            {
                uiController.ShowLog($"{currentPlayer.name}은(는) 움직일 수 없다!");
                // 입력 없이 즉시 다음 턴으로 넘김
                BattleAction skipAction = new BattleAction(currentPlayer.gameObject, currentPlayer.gameObject, ActionType.Next, 0);
                actionQueue.Add(skipAction);
                NextPlayerInput();
                return;
            }
            else if (restriction == RestrictionType.Confusion || restriction == RestrictionType.Charm)
            {
                uiController.ShowLog($"{currentPlayer.name}은(는) 혼란에 빠졌다!");
                // 플레이어 조작을 막고, 랜덤 타겟 자동 액션(Attack, Guard, Next)을 큐에 넣음.
                ProcessRandomAction(currentPlayer);
                return;
            }

            // Union Attack / Rolling Vulcan 참가자 스킵 처리
            if (currentUnionParticipants.Contains(currentPlayer))
            {
                Debug.Log($"Union Attack 또는 Rolling Vulcan 참가로 {currentPlayer.name}의 턴 스킵");
                
                /* BattleAction skipAction = new BattleAction(currentPlayer.gameObject, currentPlayer.gameObject, ActionType.Guard, 0);
                actionQueue.Add(skipAction);
                */
                
                NextPlayerInput();
                return;
            }

            if (isLastStandInputMode && currentPlayer.columnIndex < 3)
            {
                BattleAction supportAction = new BattleAction(currentPlayer.gameObject, currentPlayer.gameObject, ActionType.Guard, currentPlayer.GetTotalAgi() + 2000);
                actionQueue.Add(supportAction);
                NextPlayerInput(); 
                return; 
            }

            RefreshCommandButtons(currentPlayer);
            fieldController.HighlightToCurrentCharacter();

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

        void ProcessRandomAction(PlayerController actor)
        {
            List<ActionType> randAction = new(){ ActionType.Attack, ActionType.Guard, ActionType.Next };
            ActionType actionType = randAction[Random.Range(0, randAction.Count)] ;
            GameObject finalTarget = null;
            if (actionType == ActionType.Attack)
            {
                List<BattleEntity> candidates = new List<BattleEntity>();
                var livingMonsters = fieldController.GetLivingMonsters();
                foreach (var m in livingMonsters)
                {
                    bool isFront = (m.transform.parent.parent == fieldController.enemyFrontRowContainer);
                    if (!isFront) continue;
                    candidates.Add(m);
                }
                
                if (candidates.Count > 0)
                    finalTarget = candidates[Random.Range(0, candidates.Count)].gameObject;
            }
            int speed = actor.GetTotalAgi() - actor.nextTurnSpeedPenalty;
            actor.nextTurnSpeedPenalty = 0;
            BattleAction action = new BattleAction(actor.gameObject, finalTarget, actionType, speed);
            actionQueue.Add(action);

            NextPlayerInput();
        }

        void ProcessAutoAction(PlayerController actor)
        {
            ActionType actionType = ActionType.Attack;
            BaseRootData autoData = null;
            GameObject autoTarget = null; // 저장된 타겟

            // 저장된 행동 불러오기
            if (lastPlayerActions.ContainsKey(fieldController.currentPlayerIndex))
            {
                var info = lastPlayerActions[fieldController.currentPlayerIndex];
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
                // 아군 대상인 경우 무조건 저장된 타겟 사용
                finalTarget = autoTarget;
            }
            else
            {
                // 적 대상인 경우 살아있는 몬스터 중 랜덤 선택
                List<BattleEntity> candidates = new List<BattleEntity>();
                var livingMonsters = fieldController.GetLivingMonsters();
                bool targetFrontOnly = (scope == TargetScope.Front_Single_Enemy || scope == TargetScope.Random_Front_Enemy || scope == TargetScope.Front_Enemies);

                foreach (var m in livingMonsters)
                {
                    bool isFront = (m.transform.parent.parent == fieldController.enemyFrontRowContainer);
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

            BattleAction action = new BattleAction(actor.gameObject, finalTarget, actionType, speed);
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
            BattleEntity currentActor = fieldController.GetCurrentCharacter();
            isSelectingMoveTarget = true;
            
            uiController.SetCmdPanelVisible(false);
            uiController.ShowLog("CHOOSE YOUR PLACE");

            currentMoveSlotIndex = fieldController.GetPlayerSlotIndex(currentActor.transform.parent);
            UpdateMoveCursor();
            fieldController.RefreshMoveHighlights(currentMoveSlotIndex);
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
                fieldController.RefreshMoveHighlights(currentMoveSlotIndex); 
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
                int myCurrentIndex = fieldController.GetCurrentChracterIndex();

                if (currentMoveSlotIndex == myCurrentIndex) { CancelMoveSelection(); return; }

                Transform targetSlot = fieldController.GetPlayerSlotByIndex(currentMoveSlotIndex);

                BattleEntity currentActor = fieldController.GetCurrentCharacter();
                int moveSpeed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty + 2000;
                currentActor.nextTurnSpeedPenalty = 0; 

                BattleAction action = new BattleAction(currentActor.gameObject, targetSlot.gameObject, ActionType.Move, moveSpeed);
                actionQueue.Add(action);

                isSelectingMoveTarget = false;
                
                uiController.SetTargetCursorVisible(false);
                fieldController.ResetPlayerSlotHighlights();
                NextPlayerInput();
            }
        }

        void CancelMoveSelection()
        {
            isSelectingMoveTarget = false;
            currentFightBtnIndex = 0;
            fieldController.ResetPlayerSlotHighlights();
            uiController.SetCmdPanelVisible(true);
            uiController.SetBaseCmdVisible(false);
            uiController.SetFightCmdVisible(true);

            uiController.ShowLog("WAITING...");

            fieldController.HighlightToCurrentCharacter();

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
            uiController.SetFightCmdInteractable(true);
            uiController.ShowLog("WAITING...");

            fieldController.HighlightToCurrentCharacter();
            
            inputCooldown = 0.2f; 
            StartCoroutine(SelectButton(attackButton));
        }

        void UpdateMoveCursor()
        {
            Transform slot = fieldController.GetPlayerSlotByIndex(currentMoveSlotIndex);
            uiController.SetTargetCursorVisible(true);
            uiController.SetTargetCursorPosition(slot.position + cursorOffset);
        }

        // 회피 애니메이션
        IEnumerator ProcessDodgeAnimation(Transform targetTransform)
        {
            float direction = (Random.value > 0.5f) ? 1f : -1f;
            yield return targetTransform.DOPunchPosition(new Vector3(10.5f * direction, 0, 0), 0.3f, 1, 0).WaitForCompletion();
        }

        IEnumerator ProcessRunAttempt()
        {
            state = BattleState.Processing; 
            
            uiController.SetCmdPanelVisible(false);
            uiController.SetTargetCursorVisible(false);
            uiController.ShowLog("ESCAPE!");

            yield return wait10;

            if (BattleCalculator.CalculateEscapeSuccess(fieldController.activePlayers, fieldController.activeMonsters, currentEscapeAttempts, guaranteedEscapeAttempts))
            {
                fieldController.SetEnemyVisualsActive(false);
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
            // 취소 및 확정 입력 처리
            bool isCancel = (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape));
            if (isCancel || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (fieldController.validTargets.Count > fieldController.currentTargetIndex)
                {
                    var validTarget = fieldController.GetCurrentValidTarget();
                    validTarget.SetSelectionState(false);
                    if (isCancel) 
                    {
                        if (currentSelectedAction == ActionType.Union_Attack)
                            CancelUnionSelection();
                        else
                            CancelTargetSelection();   
                    }
                    else 
                    {
                        OnTargetSelected(validTarget);
                    }
                }
                return;
            }

            // 현재 타겟이 전열에 있는지 확인
            bool isCurrentInFront = fieldController.IsCurrentTargetInFront();

            // 현재 행과 다른 행의 타겟 리스트 분리
            var currentRowTargets = fieldController.validTargets.Where(m => (m.transform.parent.parent == fieldController.GetTargetFrontContainer()) == isCurrentInFront)
                                                .OrderBy(m => m.columnIndex).ToList();
            
            var otherRowTargets = fieldController.validTargets.Where(m => (m.transform.parent.parent == fieldController.GetTargetFrontContainer()) != isCurrentInFront)
                                              .OrderBy(m => m.columnIndex).ToList();

            BattleEntity nextEntity = null;
            bool moved = false;
            BattleEntity currentEntity = fieldController.GetCurrentValidTarget();
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                // 같은 행 내에서 순환
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
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || 
                     Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                if (otherRowTargets.Count > 0)
                {
                    // 인덱스를 3으로 나눈 나머지를 비교하여 같은 열을 우선적으로 찾음
                    int currentNormalizedCol = currentEntity.columnIndex % 3;

                    nextEntity = otherRowTargets
                        .OrderBy(t => Mathf.Abs((t.columnIndex % 3) - currentNormalizedCol))
                        .First();
                        
                    moved = true;
                }
            }
            
            // 포커스 변경 적용
            if (moved && nextEntity != null)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                fieldController.SetCurrentValidTargetIndex(nextEntity);
                fieldController.UpdateValidTargetsHighlight();
            }
        }

        public void OnTargetSelected(BattleEntity targetEntity)
        {
            if (!isSelectingTarget) return;

            if (currentSelectedAction == ActionType.Union_Attack)
            {
                fieldController.StopBlinkEffects();
            }

            PlayerController actor = fieldController.GetCurrentCharacter();

            // 타겟 정보까지 함께 저장
            if (lastPlayerActions.ContainsKey(fieldController.currentPlayerIndex))
                lastPlayerActions[fieldController.currentPlayerIndex] = (currentSelectedAction, currentSelectedItem, targetEntity.gameObject);
            else
                lastPlayerActions.Add(fieldController.currentPlayerIndex, (currentSelectedAction, currentSelectedItem, targetEntity.gameObject));
            
            int finalSpeed = actor.GetTotalAgi() - actor.nextTurnSpeedPenalty;
            actor.nextTurnSpeedPenalty = 0;

            BattleAction action = new BattleAction(actor.gameObject, targetEntity.gameObject, currentSelectedAction, finalSpeed); 
            
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
            fieldController.HideTurnOrderUI();

            actionQueue = actionQueue.OrderByDescending(x => x.speed).ToList();
            StartCoroutine(ExecuteActions());
        }

        void ProcessEnemyTurn()
        {
            if (CheckBattleEnd(out bool isWin)) { StartCoroutine(EndBattleRoutine(isWin)); return; }

            state = BattleState.EnemyInput; 
            actionQueue.Clear(); 

            var activePlayers = fieldController.activePlayers;
            var activeMonsters = fieldController.activeMonsters;
            BattleContext battleContext = new BattleContext(activePlayers, fieldController.activeMonsters);
            
            var livingMonsters = fieldController.GetLivingMonsters();
            foreach (MonsterController monster in livingMonsters)
            {
                if (monster.currentHp <= 0) continue;
                
                BattleAction enemyAction = monster.ChooseAction(battleContext);
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

        int CalculateActionDelay(BattleAction action)
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
            if (fieldController.IsAllEnemiesDead()) { isWin = true; return true; }
            if (fieldController.IsAllPartyDead()) { isWin = false; return true; }
            return false;
        }

        IEnumerator PerformAction(BattleAction action)
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
                    // 별도의 애니메이션 없이 대기
                    yield return wait05; 
                    break;
            }
            yield return wait01;
            uiController.HideLog();
        }

        IEnumerator HandleItemAction(BattleAction action)
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
            List<GameObject> targets = fieldController.GetTargetsByScope(scope, action.actor, action.target);

            uiController.ShowLog($"USE {item.dataName}");

            foreach (var targetObj in targets)
            {
                // 공격 계열 vs 보조 계열 분기 처리
                bool isAttack = item.effectType == EffectType.Special_Atk || item.effectType == EffectType.Magic_Atk;

                if (isAttack)
                {
                    // 공격
                    SoundManager.Instance.PlaySFX(SfxID.Attack_Magic);
                    visualController.SpawnVFX(VfxID.Magic, targetObj.transform.position);
                    
                    // 아이템의 고정 데미지(effectValue)를 그대로 줄지, 계산식을 탈지는 기획이 확정되면 수정하자
                    // 일단 ApplyDamage를 통해 피격 연출(OnDamageTaken)까지 연결함
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
                            SoundManager.Instance.PlaySFX(SfxID.Attack_Magic); // TODO: 회복 사운드로 교체 필요
                            visualController.SpawnVFX(VfxID.Magic, targetObj.transform.position); // TODO: 회복 이펙트로 교체 필요
                        }
                    }
                }
            }
            
            yield return wait05;
        }

        IEnumerator HandleSkillAction(BattleAction action)
        {
            // 스킬 타겟 자동 변경 로직
            if (action.target == null || !IsAlive(action.target))
            {
                action.target = fieldController.FindNearestLivingTarget(action.actor);
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
            List<GameObject> targets = fieldController.GetTargetsByScope(scope, action.actor, action.target);

            uiController.ShowLog($"{action.actor.name}'S SKILL: {skill.dataName}");

            foreach (var targetObj in targets)
            {
                // 공격 계열 vs 보조 계열 분기
                bool isAttack = skill.effectType == EffectType.Special_Atk || skill.effectType == EffectType.Magic_Atk;

                if (isAttack)
                {
                    // 공격
                    SoundManager.Instance.PlaySFX(SfxID.Attack_Magic);
                    visualController.SpawnVFX(VfxID.Magic, targetObj.transform.position);

                    // CalculateDamage를 통해 상성/방어력 계산 적용
                    // 스킬 위력은 skill.effectValue가 CalculateDamage 내부에서 참조됨
                    BattleEntity attackerEntity = action.actor.GetComponent<BattleEntity>();
                    BattleEntity targetEntity = targetObj.GetComponent<BattleEntity>();

                    bool isCrit = BattleCalculator.CheckCritical(attackerEntity, targetEntity, action);
                    int dmg = BattleCalculator.CalculateDamage(attackerEntity, targetEntity, action, isCrit, 1.0f);
                    
                    ApplyDamage(targetObj, dmg, isCrit);
                    BattleCalculator.ProcessSkillStatusEffect(attackerEntity, targetEntity, skill);
                }
                else
                {
                    // 회복, 부활, 보조
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

        IEnumerator HandleGuardAction(BattleAction action)
        {
            SetGuardState(action.actor, true);
            uiController.ShowLog($"{action.actor.name} IS GUARDING...");
            yield return wait05;
            uiController.HideLog();
        }

        IEnumerator HandleUnionAttack(BattleAction action)
        {
            if (action.target == null || !IsAlive(action.target))
            {
                action.target = fieldController.FindNearestLivingTarget(action.actor);
                if (action.target == null)
                {
                    Debug.Log("Union Attack 취소: 유효한 타겟 없음");
                    uiController.ShowLog("NO VALID TARGET!");
                    currentUnionParticipants.Clear(); 
                    yield return wait05;
                    yield break;
                }
            }

            // 참가자 데이터 복원
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

            // 리더가 목록에 없으면 추가 (리더는 반드시 포함)
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

            
            // Next 대기 중인 파트너의 속도 보너스 상쇄 로직
            
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
            

            uiController.ShowLog("UNION ATTACK!");
            SoundManager.Instance.PlaySFX(SfxID.Attack_Sword);

            // 애니메이션: 타겟 앞으로 모이기
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

            // 타격 및 데미지 계산
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
            int dmg = BattleCalculator.CalculateDamage(leaderEntity, targetEntity, action, isCrit, dmgMultiplier);
            dmg = Mathf.RoundToInt(dmg * (float)totalStr / leader.GetTotalStr()); 
            
            ApplyDamage(target, dmg, isCrit);
            
            yield return wait05;

            // 복귀 (원래 자리로)
            Sequence returnSeq = DOTween.Sequence();
            foreach (var p in partners)
            {
                returnSeq.Join(p.transform.DOMove(originPositions[p], 0.3f).SetEase(Ease.OutQuad));
            }
            yield return returnSeq.WaitForCompletion();

            
            // 타겟 위치에 따른 파티 포메이션 변경
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
            yield return fieldController.ApplyFormationChange(targetCol);
            
            // 정상 종료 시 목록 초기화
            currentUnionParticipants.Clear();
        }

        // Last Stand 집결 애니메이션
        IEnumerator HandleLastStandAction(BattleAction action)
        {
            isLastStandActive = true; 
            uiController.ShowLog("LAST STAND!!");

            List<PlayerController> frontRowMembers = fieldController.GetCharactersInFrontRow();

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
        IEnumerator HandleRollingVulcan(BattleAction action)
        {
            var leader = action.actor.GetComponent<PlayerController>();
            int index = leader.columnIndex;
            leader.SetMessage("안되겠다! 롤링 발칸이다!");
            yield return wait10;
            foreach(PlayerController pc in fieldController.activePlayers)
            {
                if (pc.columnIndex == index) pc.SetMessage(string.Empty);
                else pc.SetMessage("알았어! OK!!");
            }
            yield return wait10;
            
            fieldController.ResetCharacterMessage();
            
            uiController.ShowMessage("롤링 발칸~~~!!");
            // SoundManager.Instance.PlaySFX(SfxID.Skill_Ultimate); 
            Color bgColor = uiController.GetBackgroundColor();
            
            // 데이터 준비
            List<PlayerController> participants = currentUnionParticipants;
            int totalAmmo = participants.Sum(p => p.currentGunAmmo);

            // 무지개 빛 효과 시작
            Coroutine rainbowRoutine = StartCoroutine(ProcessRainbowEffect(participants));

            // 난사 시작
            float shotInterval = 0.08f; 
            
            for (int i = 0; i < totalAmmo; i++)
            {
                // 매 발사마다 살아있는 적 확인
                List<BattleEntity> enemies = fieldController.GetLivingMonsters();

                // 적이 살아있을 때만 데미지 처리
                if (enemies.Count > 0)
                {
                    foreach (var enemy in enemies)
                    {
                        if (enemy.currentHp <= 0) continue;
                        
                        PlayerController shooter = participants[i % participants.Count];
                        BattleEntity enemyEntity = enemy.gameObject.GetComponent<BattleEntity>();
                        int dmg = BattleCalculator.CalculateGunDamage(shooter, enemyEntity, false);
                        
                        ApplyDamage(enemy.gameObject, dmg, false);
                        visualController.SpawnVFX(VfxID.Gun, enemy.transform.position);
                    }
                }
                
                // 효과음 및 애니메이션은 적 생존 여부와 무관하게 무조건 실행
                SoundManager.Instance.PlaySFX(SfxID.Attack_Gun);
                
                // 회전 대기
                yield return fieldController.FastRotateParticipants(participants, true, shotInterval);
            }

            // 마무리
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
        IEnumerator HandleReloadAction(BattleAction action)
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

        IEnumerator HandleAttackAction(BattleAction action)
        {
            // 타겟이 없거나 이미 죽은 상태라면?
            if (action.target == null || !IsAlive(action.target))
            {
                // 가장 가까운 살아있는 적을 찾는다
                GameObject newTarget = fieldController.FindNearestLivingTarget(action.actor);

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
                pc?.SetMessage(Random.Range(0f, 1f) < 0.5f ? "얍!" : "하이얍!");
            }

            string actStr = (action.type == ActionType.Shoot) ? "'S SHOOT!" : "'S SMASH!";
            uiController.ShowLog($"{action.actor.name}{actStr}");
            yield return wait10;

            pc?.SetMessage(string.Empty);
            
            // 등장 및 공격 모션 통합
            Vector3 originalPos = action.actor.transform.localPosition;
            Vector3 originalScale = action.actor.transform.localScale;

            // 앞으로 나오기 / 커지기
            if (isMonster)
                yield return action.actor.transform.DOScale(originalScale * 1.2f, 0.15f).SetEase(Ease.OutQuad).WaitForCompletion();
            else
                yield return action.actor.transform.DOLocalMove(originalPos + new Vector3(0, 20f, 0), 0.15f).SetEase(Ease.OutQuad).WaitForCompletion();

            // 타격 처리 (QTE or Auto)
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
                        List<GameObject> currentTargets = fieldController.GetTargetsByScope(scope, action.actor, action.target);
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
                List<GameObject> currentTargets = fieldController.GetTargetsByScope(scope, action.actor, action.target);
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
            
            // 복귀
            if (isMonster)
                yield return action.actor.transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutQuad).WaitForCompletion();
            else
                yield return action.actor.transform.DOLocalMove(originalPos, 0.15f).SetEase(Ease.OutQuad).WaitForCompletion();

            yield return wait01;
        }

        IEnumerator ProcessSingleHit(BattleAction action, GameObject target)
        {
            // 위치 보정 계산 호출
            BattleFieldController.BattlePosition atkPos = fieldController.GetUnitPosition(action.actor);
            BattleFieldController.BattlePosition defPos = fieldController.GetUnitPosition(target);
            WeaponType wType = WeaponType.Melee;
            
            BattleEntity attackerEntity = action.actor.GetComponent<BattleEntity>();
            BattleEntity targetEntity = target.GetComponent<BattleEntity>();
            PlayerController pActor = action.actor.GetComponent<PlayerController>();
            
            if (action.type == ActionType.Shoot || (pActor?.currentWeapon?.type == WeaponType.Gun)) 
                wType = WeaponType.Gun;
            
            BattleCalculator.GetPositionalModifiers(atkPos, defPos, wType, out float posDmgMult, out float posEvaBonus);

            if (BattleCalculator.CheckEvasion(attackerEntity, targetEntity, posEvaBonus))
            {
                Debug.Log($"{target.name} 회피!");
                yield return StartCoroutine(ProcessDodgeAnimation(target.transform));
                if (targetEntity is PlayerController pc)
                {
                    pc.SetMessage("어림없지!");
                    yield return wait05;
                    pc.SetMessage(string.Empty);
                } 
                yield break; 
            }

            if (BattleCalculator.CheckReflection(targetEntity, action.type))
            {
                uiController.ShowLog("REFLECT!");
                visualController.SpawnVFX(VfxID.Reflect, target.transform.position);
                int reflectDmg = BattleCalculator.CalculateDamage(attackerEntity, attackerEntity, action, false, 1.0f);
                ApplyDamage(action.actor, reflectDmg, false);
                if (targetEntity is PlayerController pc)
                {
                    pc.SetMessage("반사다!");
                    yield return wait05;
                    pc.SetMessage(string.Empty);
                } 
                yield break;
            }

            if (BattleCalculator.CheckAbsorption(targetEntity, action.type))
            {
                uiController.ShowLog("ABSORB!");
                visualController.SpawnVFX(VfxID.Absorb, target.transform.position);
                int absorbAmount = BattleCalculator.CalculateDamage(attackerEntity, targetEntity, action, false, 1.0f);
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
                List<PlayerController> defenders = fieldController.GetCharactersInFrontRow();
                if (defenders.Count > 0)
                {
                    
                    bool isCrit = BattleCalculator.CheckCritical(attackerEntity, targetEntity, action);
                    int originalDamage = BattleCalculator.CalculateDamage(attackerEntity, targetEntity, action, isCrit, posDmgMult);
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

            bool isCritical = BattleCalculator.CheckCritical(attackerEntity, targetEntity, action);
            int damage = 0;

            if (action.type == ActionType.Shoot && pActor != null)
                damage = BattleCalculator.CalculateGunDamage(pActor, targetEntity, isCritical);
            else
                damage = BattleCalculator.CalculateDamage(attackerEntity, targetEntity, action, isCritical, posDmgMult);

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

        // 유니온 어택 참가 가능한 파티 찾기
        List<PlayerController> GetValidUnionPartners(PlayerController leader)
        {
            List<PlayerController> partners = new List<PlayerController>(6);
            partners.Add(leader); // 리더 포함

            // 리더가 전열(0, 1, 2)이 아니면 불가
            if (leader.columnIndex >= 3) return partners;

            // 현재 캐릭터의 왼쪽(-1), 오른쪽(+1) 이웃만 검사
            int[] neighborIndices = { leader.columnIndex - 1, leader.columnIndex + 1 };

            foreach (int i in neighborIndices)
            {
                // 인덱스 범위 체크 (전열 0~2)
                if (i < 0 || i > 2) continue;

                PlayerController p = fieldController.allSlotControllers[i];

                // 기본 상태 체크 (존재함, 빈 슬롯 아님, 살아있음)
                if (p == null || p.IsEmpty || p.currentHp <= 0) continue;

                // Align 호환성 체크
                if (!BattleCalculator.IsAlignCompatible(leader.align, p.align)) continue;

                // 행동 예약 상태 체크. 이미 행동 큐에 등록된 행동이 있는지 확인
                bool isBusy = false;
                foreach(var action in actionQueue) {
                    // 행동이 아직 예약되지 않았거나(미행동), 예약된 행동이 'Next'인 경우만 가능
                    if (action.actor == p.gameObject && action.type != ActionType.Next) {
                        isBusy = true; break; 
                    }
                }
                if (!isBusy) partners.Add(p);
            }

            return partners;
        }

        void GetWeaponInfo(BattleAction action, out int min, out int max, out TargetScope scope)
        {
            min = 1; max = 1; scope = TargetScope.Front_Single_Enemy; 
            var pActor = action.actor.GetComponent<PlayerController>();
            WeaponData weapon = null;
            if (pActor != null) weapon = (action.type == ActionType.Shoot) ? pActor.currentGun : pActor.currentWeapon;
            if (weapon != null) { min = weapon.minHits; max = weapon.maxHits; scope = weapon.attackRange; }
        }

        bool IsAlive(GameObject obj) { return obj != null && obj.activeSelf && (obj.GetComponent<IBattleTarget>()?.IsAlive ?? false); }

        // 아군 위치 이동 애니메이션
        IEnumerator PerformMove(BattleAction action)
        {
            PlayerController actor = action.actor.GetComponent<PlayerController>();
            if (actor == null || actor.currentHp <= 0) yield break;

            Transform targetSlotTransform = action.target.transform; 
            Transform originSlotTransform = actor.transform.parent;

            if (targetSlotTransform == originSlotTransform) yield break;

            PlayerController targetChar = targetSlotTransform.GetComponentInChildren<PlayerController>();
            uiController.ShowMessage((targetChar != null && !targetChar.IsEmpty) ? "위치 교대!" : "자리 이동!");
            fieldController.SwapPosition(actor, targetChar, targetSlotTransform);

            SoundManager.Instance.PlaySFX(SfxID.UI_Click); 

            Sequence seq = DOTween.Sequence();
            seq.Join(actor.transform.DOLocalMove(Vector3.zero, 0.5f).SetEase(Ease.OutQuad));
            if (targetChar != null) seq.Join(targetChar.transform.DOLocalMove(Vector3.zero, 0.5f).SetEase(Ease.OutQuad));
            
            yield return seq.WaitForCompletion();
            yield return wait05; 
            uiController.HideMessage();
        }

        IEnumerator EndBattleRoutine(bool isWin)
        {
            state = isWin ? BattleState.Won : BattleState.Lost;
            uiController.SetCmdPanelVisible(false);

            if (isWin)
            {
                SoundManager.Instance.PlayBGM(BgmID.Victory);
                
                List<PlayerController> allPlayers = fieldController.GetPlayerControllers();
                BattleReward reward = BattleCalculator.CalculateRewards(allPlayers, fieldController.encounterLog);

                // 경험치 반영 전 상태 스냅샷 저장
                Dictionary<PlayerController, (int oldLv, int oldExp, int oldMaxExp)> preBattleStates = new Dictionary<PlayerController, (int, int, int)>();
                
                foreach(var pc in allPlayers)
                {
                    if (pc != null && pc.currentHp > 0) 
                    {
                        int oldLevel = pc.sourceData.stats.level;
                        int maxExp = BattleCalculator.GetMaxExpForLevel(oldLevel); // Spirit의 영향이 없는 원본 데이터의 Level을 사용함
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
                
                InventoryManager.Instance.AddMoney(reward.totalMoney);
                foreach(var itemId in reward.dropItems) InventoryManager.Instance.AddItem(itemId, 1);

                // 결과 UI 표시
                bool isResultClosed = false;
                uiController.ShowResult(reward, allPlayers, preBattleStates, ()=> isResultClosed = true);

                yield return new WaitUntil(() => isResultClosed);

                // 레벨업 판별 및 분기 로직
                List<PlayerController> leveledUpPlayers = new List<PlayerController>();
                Dictionary<PlayerController, int> oldLevelsDict = new Dictionary<PlayerController, int>();

                foreach(var pc in allPlayers)
                {
                    if (pc != null && pc.currentHp > 0)
                    {
                        int oldLv = preBattleStates[pc].oldLv;
                        int newLv = pc.sourceData.stats.level; // ApplyExperience 후의 현재 본체 레벨
                        
                        if (newLv > oldLv)
                        {
                            leveledUpPlayers.Add(pc);
                            oldLevelsDict.Add(pc, oldLv);
                        }
                    }
                }

                if (levelUpUI != null && leveledUpPlayers.Count > 0)
                {
                    bool isLevelUpClosed = false;
                    
                    // ResultUI가 닫힌 뒤에 LevelUpUI 호출
                    levelUpUI.Show(leveledUpPlayers, oldLevelsDict, () => {
                        isLevelUpClosed = true;
                    });

                    yield return new WaitUntil(() => isLevelUpClosed);
                }
            }
            else 
            {
                uiController.ShowMessage("패배는 너의 것!");
                yield return wait05;
            }

            // 전투 종료
            uiController.ShowBattleEndAnimation(()=>{GameStateManager.Instance.ChangeState(GameState.Exploration);});
        }
        
    }
}