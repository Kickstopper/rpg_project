using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UI.DungeonMapScene;
using TMPro;
using Controller;
using UnityEngine.EventSystems;
using Data;
using UI;
using DG.Tweening;
using Helper;

namespace Manager
{
    public enum BattleState { Start, PlayerInput, EnemyInput, Processing, Won, Lost }
    
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance;

        [Header("UI References")]
        public GameObject baseCmdContainer;   // 1단계 메뉴 (Fight, Talk...)
        public GameObject fightCmdContainer;  // 2단계 메뉴 (Attack, Move...)
        public RectTransform btnContainer; //fightCmdContainer의 버튼이 붙는 트랜스폼 (Inspector 할당)
        public GameObject subMenuContainer; // 서브 메뉴 패널 오브젝트 (Inspector 할당)
        public List<Button> baseButtons; 
        public List<CommandButton> allFightButtons;
        public BattleSkillUIController battleSkillUI; // 인스펙터에서 할당
        public BattleItemUIController battleItemUI; // 인스펙터에서 할당
        public GameObject commandPanel;     // 커맨드 버튼들
        public GameObject logPanel;         // 시스템 로그 패널
        public TextMeshProUGUI logText;     // 시스템 안내 메시지 텍스트
        public GameObject messagePanel;     // 전투 중 캐릭터의 메시지 패널
        public TextMeshProUGUI messageText; // 전투 중 캐릭터의 메시지 텍스트
        public Slider qteTimingSlider; // 타이머 슬라이더. 인스펙터에서 할당
        public Button autoModeButton; // 오토 모드의 트리거
        // 에디터에서 모든 버튼(Attack, Gun, Skill, Item, Move, Guard)을 순서대로 넣으세요.
        
        
        private List<Button> activeFightButtons = new List<Button>();
        private int currentFightBtnIndex = 0; // fight 메뉴용 인덱스
        private int currentBaseBtnIndex = 0;  // Base 메뉴용 인덱스

        // 메뉴 계층 관리 변수
        private bool isSubMenuActive = false; // 현재 서브 메뉴가 열려있는지
        private List<Button> currentMenuButtons = new List<Button>(); // 현재 화면에 표시/조작 중인 버튼 리스트
        
        private List<Button> cachedMainMenuButtons = new List<Button>(); // 메인 메뉴 버튼들을 임시 저장할 리스트 (서브 메뉴에서 돌아올 때 복구용)

        [Header("Prefabs")]
        public GameObject defaultMonsterPrefab;
        public GameObject playerPrefab;
        public GameObject vfxSlashPrefab;  // 물리 공격용
        public GameObject vfxGunPrefab;  // 총 공격용
        public GameObject vfxMagicPrefab;  // 마법 공격용
        public GameObject vfxGuardHitPrefab;   // 방어 상태에서 맞았을 때 (방패 모양 등)
        public GameObject vfxReflectPrefab;    // 반사 발동 시 (배리어 등)
        public GameObject vfxAbsorbPrefab;     // 흡수 발동 시 (녹색 회복 이펙트 등)

        [Header("First Focus Buttons")]
        public GameObject baseFirstButton;    // Base 메뉴의 첫 버튼 (Fight 버튼)
        public GameObject attackButton;    // Fight 메뉴의 첫 버튼 (Attack 버튼)

        public RectTransform targetCursor; // 손가락 커서 이미지
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
        private PlayerController lastHighlightedPlayer; // 마지막으로 하이라이트된 플레이어를 기억할 변수
        private bool isSelectingMoveTarget = false;
        private int currentMoveSlotIndex = 0; // 0~2: 전열, 3~5: 후열

        [Header("Highlight Colors")]
        public Color currentCmdTargetColor = Color.gray;
        public Color moveSourceColor = Color.gray;   // 이동하려는 내 캐릭터 색상
        public Color moveTargetColor = Color.cyan;  // 커서가 가리키는 대상 색상

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
        public List<BattleEntity> activeMonsters = new();
        // 전투 로직용 리스트 (데이터가 있는 캐릭터만)
        public List<BattleEntity> activePlayers = new(); 

        // 렌더링 및 그리드 관리용 리스트 (Empty 포함, 총 6개 고정)
        public List<PlayerController> allSlotControllers = new();

        public List<CombatAction> actionQueue = new(); // 이번 턴의 모든 행동

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
        
        void Awake() { if (Instance == null) Instance = this; }

        public void Initialize(List<string> monsterIds)
        {
            isAutoMode = false;         
            reserveAutoOff = false;     
            autoModeButton.gameObject.SetActive(false);

            isFightMode = false;        
            Time.timeScale = 1.0f;      

            currentBaseBtnIndex = 0;
            currentFightBtnIndex = 0;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            
            state = BattleState.Start;

            if (logPanel) { logPanel.SetActive(false); logText.SetText(string.Empty); }
            if (messagePanel) { messagePanel.SetActive(false); messageText.SetText(string.Empty); }
            
            activeMonsters.Clear(); 
            ClearParty();           
            InitializeSlots();

            if (monsterIds == null || monsterIds.Count == 0) return;

            SoundManager.Instance.PlayBGM(BgmID.Encounter);

            int maxSpawnLimit = Mathf.Min(monsterIds.Count, 6);
            int spawnCount = Random.Range(1, maxSpawnLimit + 1); 

            Debug.Log($"[Encounter] 몬스터 {spawnCount}마리가 출현합니다!");

            for (int i = 0; i < spawnCount; i++)
            {
                int randomIndex = Random.Range(0, monsterIds.Count);
                SpawnMonster(monsterIds[randomIndex]);
            }
            
            SpawnParty();

            if (activePlayers.Count == 0) return; 

            LayoutRebuilder.ForceRebuildLayoutImmediate(enemyFrontRowContainer as RectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(playerFrontRowContainer as RectTransform);

            StartCoroutine(SetupBattle());
        }

        void SpawnParty()
        {
            activePlayers.Clear();
            allSlotControllers.Clear();

            for (int i = 0; i < 6; i++)
            {
                bool isFront = (i < 3);
                List<Transform> targetSlots = isFront ? playerFrontSlots : playerBackSlots;
                int slotIndex = isFront ? i : (i - 3);
                Transform targetSlot = targetSlots[slotIndex];

                GameObject go = Instantiate(playerPrefab, targetSlot);
                go.transform.localPosition = Vector3.zero;

                PlayerController pc = go.GetComponent<PlayerController>();
                allSlotControllers.Add(pc);

                var data = PartyManager.Instance.GetMemberData(i);
                if (data != null)
                {
                    pc.Initialize(data, isFront ? RowType.Front : RowType.Back);
                    pc.columnIndex = i; 
                    pc.gameObject.name = pc.sourceData.name;
                    activePlayers.Add(pc);
                }
                else
                {
                    pc.InitializeEmpty(i);
                }
            }
        }

        IEnumerator SetupBattle()
        {
            yield return wait10;
            PreparePlayerTurn();
        }

        private void PrepareWeaponAction(WeaponData weapon, ActionType actionType)
        {
            BattleEntity currentActor = activePlayers[currentPlayerIndex];
            TargetScope scope = TargetScope.FrontSingle; 
            
            if (weapon != null) scope = weapon.attackRange;
            else if (actionType == ActionType.Shoot) return; 

            if (scope == TargetScope.FrontSingle || scope == TargetScope.AnySingle)
            {
                validTargets = activeMonsters.Where(m => m.currentHp > 0).ToList();

                if (scope == TargetScope.FrontSingle)
                {
                    validTargets = validTargets.Where(m => m.transform.parent.parent == enemyFrontRowContainer).ToList();
                    if (validTargets.Count == 0) validTargets = activeMonsters.Where(m => m.currentHp > 0).ToList();
                }
                
                validTargets = validTargets.OrderBy(m => m.transform.parent.parent == enemyBackRowContainer)
                                            .ThenBy(m => m.transform.position.x).ToList();

                if (validTargets.Count == 0) return; 

                currentSelectedAction = actionType;
                isSelectingTarget = true;
                
                commandPanel.SetActive(false);
                if (logPanel) { logPanel.SetActive(true); logText.text = "SELECT TARGET"; }
                
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
                autoModeButton.gameObject.SetActive(false);
                Time.timeScale = 1.0f; 
                if (logPanel) logPanel.SetActive(false); 
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
            if (isAutoMode)
            {
                if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.LeftShift))
                {
                    if (!reserveAutoOff)
                    {
                        autoModeButton.Select();
                        autoModeButton.GetComponent<Image>().color = Color.white;
                        reserveAutoOff = true;
                    }
                }
            }

            if (inputCooldown > 0) inputCooldown -= Time.deltaTime;

            if (state == BattleState.PlayerInput)
            {
                if (isAutoMode) return;
                Time.timeScale = 1.0f;

                if (battleItemUI != null && battleItemUI.gameObject.activeSelf) return; 
                if (battleSkillUI != null && battleSkillUI.gameObject.activeSelf) return;

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

                bool isPopupActive = (battleItemUI != null && battleItemUI.gameObject.activeSelf) || 
                                     (battleSkillUI != null && battleSkillUI.gameObject.activeSelf);
                
                if (!isSelectingTarget && !isSelectingMoveTarget && !isPopupActive && commandPanel.activeSelf)
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

        // 메인 메뉴 버튼 갱신 및 순서 정렬
        void RefreshCommandButtons(PlayerController actor)
        {
            foreach (var btn in allFightButtons)
            {
                btn.transform.SetParent(btnContainer, false); 
                btn.gameObject.SetActive(false);
            }

            // 1. 기존 리스트 초기화
            allFightButtons.ForEach(btn => btn.gameObject.SetActive(false));
            activeFightButtons.Clear(); 
            
            // 2. 각 버튼의 활성화 조건 계산
            
            // Skill 조건: 배운 스킬이 있어야 함
            bool canSkill = actor.learnedSkillIds.Count > 0;

            // Gun 메뉴 조건: 쏘거나 장전할 수 있어야 함
            bool canShoot = actor.CanShootGun() && actor.currentGunAmmo > 0;
            bool canReload = (actor.currentGun != null) && (actor.currentGunAmmo < actor.currentGun.maxHits);
            bool showGunMenu = canShoot || canReload;

            // Extra 메뉴 조건: 아이템이 있거나, 이동/방어/대기(항상 가능) 중 하나라도 가능하면
            bool canItem = (InventoryManager.Instance.GetAllItemIds().Count > 0);
            bool showExtraMenu = canItem || true; // Move, Guard, Next는 항상 가능하므로 true

            // Tactics 메뉴 조건: 협동 공격이나 배수진이 가능해야 함
            bool canUnion = CheckUnionAttackCondition(actor);
            bool canLastStand = CheckLastStandCondition(actor);
            bool showTacticsMenu = canUnion || canLastStand;

            // ---------------------------------------------------------
            // 3. 메인 메뉴 버튼 등록 (순서 중요: Attack > Skill > Gun > Extra > Tactics)
            // ---------------------------------------------------------
            
            // 1. Attack
            AddButtonToActiveList(ActionType.Attack, true);

            // 2. Skill (Main Menu에 표시)
            AddButtonToActiveList(ActionType.Skill, canSkill);

            // 3. Gun Menu ▶ (Shoot, Reload)
            AddButtonToActiveList(ActionType.Menu_Gun, showGunMenu);//, "Gun 〉"); 

            // 4. Extra Menu ▶ (Item, Move, Guard, Next)
            AddButtonToActiveList(ActionType.Menu_Extra, showExtraMenu);//, "Extra 〉"); 

            // 5. Tactics Menu ▶ (Union, LastStand)
            AddButtonToActiveList(ActionType.Menu_Tactics, showTacticsMenu);//, "Tactics 〉"); 

            // ---------------------------------------------------------
            // 4. UI 갱신 준비
            // ---------------------------------------------------------
            cachedMainMenuButtons = new List<Button>(activeFightButtons);
            currentMenuButtons = activeFightButtons;
            isSubMenuActive = false;

            if (subMenuContainer) 
            {
                subMenuContainer.SetActive(false);
                // 초기화 시점에는 부모 무시 옵션 꺼둠
                SetContainerInteractable(subMenuContainer, false, false); 
            }
            
            SetContainerInteractable(fightCmdContainer, true);

            ResizeContainer(btnContainer, currentMenuButtons.Count);
            
            currentFightBtnIndex = 0;
        }
        
        // 버튼 추가 헬퍼 함수
        void AddButtonToActiveList(ActionType type, bool isActive, string customLabel = null)
        {
            CommandButton cmdBtn = allFightButtons.Find(b => b.type == type);
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

                    activeFightButtons.Add(btn);
                }
            }
        }
        
        // 공식: (버튼 개수 * 30) + 10
        void ResizeContainer(RectTransform container, int count)
        {
            if (container != null)
            {
                float newHeight = (count * 30f) + 10f;
                container.sizeDelta = new Vector2(container.sizeDelta.x, newHeight);
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
                HandleMenuNavigation(currentMenuButtons, ref currentFightBtnIndex);
                
                // 오른쪽 키: 서브 메뉴 진입 (확인 키와 동일 효과 - 단, 메뉴 타입일 때만)
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    Button currentBtn = currentMenuButtons[currentFightBtnIndex];
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
                HandleMenuNavigation(baseButtons, ref currentBaseBtnIndex);
            }
        }

        // 서브 메뉴 진입
        void OpenSubMenu(ActionType menuType)
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;

            // 1. 메인 메뉴 인터랙션 비활성화
            SetContainerInteractable(fightCmdContainer, false);

            // 2. 서브 메뉴 리스트 구성
            List<Button> subButtons = new List<Button>();
            
            if (menuType == ActionType.Menu_Gun)
            {
                bool canShoot = actor.CanShootGun() && actor.currentGunAmmo > 0;
                bool canReload = (actor.currentGun != null) && (actor.currentGunAmmo < actor.currentGun.maxHits);
                AddSubButton(ActionType.Shoot, canShoot, subButtons); 
                AddSubButton(ActionType.Reload, canReload, subButtons);
            }
            else if (menuType == ActionType.Menu_Extra)
            {
                bool canItem = (InventoryManager.Instance.GetAllItemIds().Count > 0);
                AddSubButton(ActionType.Item, canItem, subButtons);
                AddSubButton(ActionType.Move, true, subButtons);
                AddSubButton(ActionType.Guard, true, subButtons);
                AddSubButton(ActionType.Next, true, subButtons);
            }
            else if (menuType == ActionType.Menu_Tactics)
            {
                bool canUnion = CheckUnionAttackCondition(actor);
                bool canLastStand = CheckLastStandCondition(actor); 
                AddSubButton(ActionType.Union_Attack, canUnion, subButtons);
                AddSubButton(ActionType.Last_Stand, canLastStand, subButtons);
            }

            // 3. 서브 메뉴 버튼들을 별도 패널로 이동 및 활성화
            if (subMenuContainer != null)
            {
                subMenuContainer.SetActive(true);
                
                // 서브 메뉴 패널을 강제로 인터랙션 가능하게 설정 (부모 무시)
                SetContainerInteractable(subMenuContainer, true, true);

                foreach (var btn in subButtons)
                {
                    btn.transform.SetParent(subMenuContainer.transform, false);
                    btn.gameObject.SetActive(true);
                }
                
                ResizeContainer(subMenuContainer.GetComponent<RectTransform>(), subButtons.Count);
            }

            // 4. 상태 전환
            currentMenuButtons = subButtons;
            isSubMenuActive = true;
            currentFightBtnIndex = 0;
            
            if (currentMenuButtons.Count > 0) StartCoroutine(SelectButtonDelayed(currentMenuButtons, 0));
        }

        // 서브 메뉴 버튼 추가 헬퍼
        void AddSubButton(ActionType type, bool isActive, List<Button> list)
        {
            CommandButton cmdBtn = allFightButtons.Find(b => b.type == type);
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
            if (subMenuContainer != null)
            {
                foreach (var btn in currentMenuButtons)
                {
                    btn.transform.SetParent(btnContainer, false);
                    btn.gameObject.SetActive(false);
                }
                subMenuContainer.SetActive(false);
            }

            // 2. 메인 메뉴 활성화
            SetContainerInteractable(fightCmdContainer, true);

            // 3. 상태 복구
            currentMenuButtons = cachedMainMenuButtons;
            isSubMenuActive = false;
            
            // 메인 메뉴 컨테이너(btnContainer) 리사이징 (복구)
            ResizeContainer(btnContainer, currentMenuButtons.Count);
            
            // 4. 인덱스 복구 및 포커스
            currentFightBtnIndex = lastMainIndex; 
            StartCoroutine(SelectButtonDelayed(currentMenuButtons, currentFightBtnIndex));
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
                currentList[currentIndex].onClick.Invoke();
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
            baseCmdContainer.SetActive(false);
            fightCmdContainer.SetActive(true);
            inputCooldown = 0.2f;
            currentFightBtnIndex = 0;
            StartCoroutine(SelectButtonDelayed(activeFightButtons, currentFightBtnIndex));
        }

        public void OnBaseCommand_Escape() { OnCommandButton_Escape(); }
        public void OnBaseCommand_Talk() { Debug.Log("대화하기 (미구현)"); }

        public void OnBaseCommand_Auto()
        {
            SoundManager.Instance.PlayBGM(BgmID.Normal_Battle);
            isAutoMode = true;
            reserveAutoOff = false; 
            autoModeButton.gameObject.SetActive(true);
            autoModeButton.GetComponent<Image>().color = Color.red;
            Time.timeScale = 2.0f;

            if (baseCmdContainer) baseCmdContainer.SetActive(false);
            if (fightCmdContainer) fightCmdContainer.SetActive(false);
            commandPanel.SetActive(false);
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
                if (logPanel) { logPanel.SetActive(true); logText.text = "CANNOT USE GUN\n(Need Gun & Ammo)"; }
                return;
            }
            PrepareWeaponAction(actor.currentGun, ActionType.Shoot);
        }

        public void OnFightCommand_Reload()
        {
            inputCooldown = 0.2f;

            PlayerController currentActor = activePlayers[currentPlayerIndex] as PlayerController;
            int speed = currentActor.GetTotalAgi() - currentActor.nextTurnSpeedPenalty; // 일반 속도

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
            if (battleSkillUI == null) return; 
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;
            if (actor.learnedSkillIds.Count == 0)
            {
                if (logPanel) { logPanel.SetActive(true); logText.text = "사용할 수 있는 스킬이 없습니다"; }
            }
            else
            {
                if (logPanel) { logPanel.SetActive(true); logText.text = "사용할 스킬을 선택하세요"; }
                SetContainerInteractable(fightCmdContainer, false);
                battleSkillUI.Show(actor.learnedSkillIds);
            } 
        }

        public void OnFightCommand_Item()
        {
            if (battleItemUI == null) return;
            if (InventoryManager.Instance.GetAllItemIds().Count == 0)
            {
                if (logPanel) { logPanel.SetActive(true); logText.text = "사용할 수 있는 아이템이 없습니다"; }
            }
            else
            {
                if (logPanel) { logPanel.SetActive(true); logText.text = "사용할 아이템을 선택하세요"; }
                SetContainerInteractable(fightCmdContainer, false);
                battleItemUI.Show();
            }
        }

        public void OnPopupMenuClosed()
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
            currentFightBtnIndex = 0;
            SetContainerInteractable(fightCmdContainer, true);
            StartCoroutine(SelectButton(attackButton)); 
        }

        void SetContainerInteractable(GameObject container, bool isInteractable, bool ignoreParent = false)
        {
            if (container == null) return;
            
            CanvasGroup group = container.GetComponent<CanvasGroup>();
            // CanvasGroup이 없으면 자동으로 추가
            if (group == null) group = container.AddComponent<CanvasGroup>();

            group.interactable = isInteractable;
            group.blocksRaycasts = isInteractable;
            group.ignoreParentGroups = ignoreParent; // 부모의 설정 무시 여부
        }

        public void OnPopupItemSelected(BaseRootData item)
        {
            currentSelectedItem = item;
            if (item is SkillData) currentSelectedAction = ActionType.Skill;
            else if (item is ConsumableItemData) currentSelectedAction = ActionType.Item;

            TargetScope scope = item.targetScope;

            // 대상을 직접 찍어야 하는 경우만 StartItemTargetSelection 호출
            if (scope == TargetScope.AnySingle || scope == TargetScope.OneAlly || scope == TargetScope.DeadAlly || scope == TargetScope.FrontSingle)
            {
                StartItemTargetSelection(scope); 
            }
            else
            {
                // AllAllies, Self, FrontAll, AnyAll 등은 대상 선택 없이 즉시 사용 예약
                
                // 아이템 선택 키 입력이 다음 턴의 명령 선택(Attack 등)으로 이어지지 않도록 쿨타임 부여
                inputCooldown = 0.2f; 

                // 이때 target은 null로 전달되지만, 수정한 HandleItemAction이 scope를 보고 대상을 찾음
                QueuePolymorphicAction(null); 
            }
        }

        void StartItemTargetSelection(TargetScope scope)
        {
            validTargets.Clear();
            if (scope == TargetScope.AnySingle)
                validTargets.AddRange(activeMonsters.Where(m => m != null && m.currentHp > 0));
            else if (scope == TargetScope.OneAlly) 
                validTargets.AddRange(activePlayers.Where(p => p != null && p.currentHp > 0));
            else if (scope == TargetScope.DeadAlly)
                validTargets.AddRange(activePlayers.Where(p => p != null && p.currentHp <= 0));
            
            if (validTargets.Count == 0)
            {
                if (logPanel) { logPanel.SetActive(true); logText.text = "No Target!"; StartCoroutine(HideLogAfterDelay(1.0f)); }
                return; 
            }
            
            isSelectingTarget = true;
            currentSelectedAction = ActionType.Item;
            currentTargetIndex = 0; 
            UpdateTargetHighlight();
            inputCooldown = 0.2f;

            if (fightCmdContainer != null && fightCmdContainer.activeSelf)
            {
                SetContainerInteractable(fightCmdContainer, false);
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        IEnumerator HideLogAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if(logPanel) logPanel.SetActive(false);
        }

        void QueuePolymorphicAction(GameObject target)
        {
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;
            CombatAction action = new CombatAction(actor.gameObject, target, currentSelectedAction, actor.GetTotalAgi());
            action.itemData = currentSelectedItem; 
            if (currentSelectedItem is SkillData skill) action.skillData = skill;

            // [추가] 즉시 실행되는 행동(AllAllies, Self 등)도 Auto 모드를 위해 저장
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
                p.SetHighlightColor(currentCmdTargetColor);
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
                if (logPanel) { logPanel.SetActive(true); logText.text = "전열에 적이 없습니다!"; StartCoroutine(HideLogAfterDelay(1.0f)); }
                CancelUnionSelection(); // 취소 처리
                return;
            }

            isSelectingTarget = true;
            currentTargetIndex = 0;
            UpdateTargetHighlight();
            
            // UI 숨기기
            commandPanel.SetActive(false);
            if (logPanel) { logPanel.SetActive(true); logText.text = "SELECT TARGET (Union)"; }
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

        void ShowBaseMenu()
        {
            isFightMode = false; 
            fightCmdContainer.SetActive(false);
            baseCmdContainer.SetActive(true);
            currentBaseBtnIndex = 0; 
            UpdateSelection(baseButtons, currentBaseBtnIndex);
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

            // Union Attack 참가자는 행동 선택 불가 (리더 제외)
            // currentUnionParticipants 리스트에 있고, 현재 리더(방금 명령 내린 사람)가 아니라면 스킵
            // (주의: currentPlayerIndex가 리더보다 높은 경우에만 해당. 이미 지나간 사람은 고려 X)
            if (currentUnionParticipants.Contains(currentPlayer))
            {
                // 리더 본인이 아니라면 스킵 (리더는 이미 명령을 내렸으므로 이 함수에 다시 들어올 일 없음)
                Debug.Log($"Union Attack 참가로 {currentPlayer.name}의 턴 스킵");
                
                // 대기 행동 추가 (방어 아님, 그냥 대기)
                CombatAction skipAction = new CombatAction(currentPlayer.gameObject, currentPlayer.gameObject, ActionType.Guard, 0);
                actionQueue.Add(skipAction);
                
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
            currentPlayer.SetHighlightColor(currentCmdTargetColor);

            if (isAutoMode)
            {
                ProcessAutoAction(currentPlayer);
                return; 
            }

            isSelectingTarget = false;
            commandPanel.SetActive(true);
            
            if (logPanel) logPanel.SetActive(true);
            logText.text = $"명령 대기: {currentPlayer.sourceData.name}";
            if (targetCursor) targetCursor.gameObject.SetActive(false);

            if (isFightMode)
            {
                if (baseCmdContainer) baseCmdContainer.SetActive(false);
                if (fightCmdContainer) fightCmdContainer.SetActive(true);
                currentFightBtnIndex = 0;
                StartCoroutine(SelectButtonDelayed(activeFightButtons, currentFightBtnIndex));
            }
            else
            {
                if (baseCmdContainer) baseCmdContainer.SetActive(true);
                if (fightCmdContainer) fightCmdContainer.SetActive(false);
                currentBaseBtnIndex = 0;
                StartCoroutine(SelectButtonDelayed(baseButtons, currentBaseBtnIndex));
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
            TargetScope scope = TargetScope.FrontSingle; 
            switch (actionType)
            {
                case ActionType.Attack: scope = (actor.currentWeapon != null) ? actor.currentWeapon.attackRange : TargetScope.FrontSingle; break;
                case ActionType.Shoot: scope = (actor.currentGun != null) ? actor.currentGun.attackRange : TargetScope.FrontSingle; break;
                case ActionType.Skill:
                case ActionType.Item: if (autoData != null) scope = autoData.targetScope; break;
            }

            // 타겟 결정
            GameObject finalTarget = null;

            // 아군 대상(회복/버프) 스코프인지 확인
            bool isAllyScope = (scope == TargetScope.OneAlly || scope == TargetScope.AllAllies || 
                                scope == TargetScope.Self || scope == TargetScope.DeadAlly);

            if (isAllyScope)
            {
                // 아군 대상인 경우: 무조건 저장된 타겟(autoTarget) 사용
                // (OneAlly인 경우 지정했던 아군, AllAllies/Self인 경우 null 혹은 본인이 들어있음)
                finalTarget = autoTarget;
            }
            else
            {
                // 적 대상인 경우: 기존 로직대로 살아있는 몬스터 중 랜덤 선택
                // (공격 대상은 매번 바뀌거나 죽을 수 있으므로 랜덤이 일반적)
                List<BattleEntity> candidates = new List<BattleEntity>();
                var livingMonsters = activeMonsters.Where(m => m.currentHp > 0).ToList();
                bool targetFrontOnly = (scope == TargetScope.FrontSingle || scope == TargetScope.FrontRandom || scope == TargetScope.FrontAll);

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
            
            commandPanel.SetActive(false);
            if (logPanel) { logPanel.SetActive(true); logText.text = "이동할 위치를 선택하세요."; }

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
                if (targetCursor) targetCursor.gameObject.SetActive(false);
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
            commandPanel.SetActive(true);
            baseCmdContainer.SetActive(false);
            fightCmdContainer.SetActive(true);

            if (logPanel) logPanel.SetActive(true); 
            logText.SetText($"명령 대기: {activePlayers[currentPlayerIndex].entityName}");
            if (targetCursor) targetCursor.gameObject.SetActive(false);
            
            inputCooldown = 0.2f;
            StartCoroutine(SelectButton(attackButton)); 
        }

        void CancelTargetSelection()
        {
            isSelectingTarget = false;
            currentFightBtnIndex = 0;
            commandPanel.SetActive(true);
            baseCmdContainer.SetActive(false);
            fightCmdContainer.SetActive(true);
            
            if (targetCursor) targetCursor.gameObject.SetActive(false);
            if (logPanel) logPanel.SetActive(true); 
            logText.SetText($"명령 대기: {activePlayers[currentPlayerIndex].entityName}");

            SetContainerInteractable(fightCmdContainer, true);
            inputCooldown = 0.2f; 
            StartCoroutine(SelectButton(attackButton));
        }

        void UpdateMoveCursor()
        {
            Transform slot = GetPlayerSlotByIndex(currentMoveSlotIndex);
            if (targetCursor)
            {
                targetCursor.gameObject.SetActive(true);
                targetCursor.position = slot.position + cursorOffset; 
            }
        }

        void ResetPlayerSlotHighlights()
        {
            foreach (PlayerController player in allSlotControllers) player.ResetHighlightColor();
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
                if (targetChar != null) targetChar.SetHighlightColor(moveTargetColor);
            }
        }

        public void OnCommandButton_Escape() { StartCoroutine(ProcessRunAttempt()); }

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
            commandPanel.SetActive(false);
            if (targetCursor) targetCursor.gameObject.SetActive(false);
            if (logPanel) { logPanel.SetActive(true); logText.text = "도망치는 중..."; }

            yield return wait10;

            if (CalculateEscapeSuccess())
            {
                logText.text = "무사히 도망쳤다!";
                yield return wait10;
                DungeonStateManager.Instance.ChangeState(GameState.Exploration);
            }
            else
            {
                logText.text = "도망치지 못했다!\n적에게 틈을 보이고 말았다.";
                yield return wait10;
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

        BattleEntity FindEntityInRow(Transform frontContainer, bool isTargetFront, int startCol, int direction)
        {
            var rowEntities = validTargets.Where(m => (m.transform.parent.parent == frontContainer) == isTargetFront).OrderBy(m => m.columnIndex).ToList();
            if (rowEntities.Count == 0) return null;

            BattleEntity current = validTargets[currentTargetIndex];
            int currentIndexInRow = rowEntities.IndexOf(current);
            if (currentIndexInRow == -1) return null;

            int nextIndex = currentIndexInRow + direction;
            if (nextIndex >= 0 && nextIndex < rowEntities.Count) return rowEntities[nextIndex];
            return null; 
        }

        BattleEntity FindClosestEntityInRow(Transform frontContainer, bool isTargetFront, int targetCol)
        {
            var targetRowEntities = validTargets.Where(m => (m.transform.parent.parent == frontContainer) == isTargetFront).ToList();
            if (targetRowEntities.Count == 0) return null;
            return targetRowEntities.OrderBy(m => Mathf.Abs(m.columnIndex - targetCol)).First();
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
            if (currentSelectedAction == ActionType.Item)
            {
                action.itemData = currentSelectedItem; 
                action.speed += 500; 
            }

            actionQueue.Add(action);
            isSelectingTarget = false;
            if (targetCursor) targetCursor.gameObject.SetActive(false);
            
            targetEntity.SetSelectionState(false);
            SetContainerInteractable(fightCmdContainer, true);
            NextPlayerInput();
        }

        void ProcessTurn()
        {
            SoundManager.Instance.PlayBGM(BgmID.Normal_Battle);
            state = BattleState.Processing; 
            
            commandPanel.SetActive(false);
            if (logPanel) logPanel.SetActive(false);
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
                else if (action.actor.TryGetComponent(out BattleEntity be) && !be.IsAlive) isActorDead = true;
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
                case ActionType.Next:
                    ShowLog($"{action.actor.name}은(는) 기회를 엿보고 있다...");
                    // 별도의 애니메이션 없이 짧게 대기
                    yield return wait05; 
                    break;
            }
            yield return wait01;
            if (logPanel) logPanel.SetActive(false);
            logText.SetText(string.Empty);
        }

        IEnumerator HandleItemAction(CombatAction action)
        {
            BaseRootData item = action.itemData;
            
            // TargetScope에 따라 다중 타겟 가져오기
            // (QueuePolymorphicAction에서 target을 null로 보냈어도 여기서 찾음)
            TargetScope scope = (item != null) ? item.targetScope : TargetScope.OneAlly;
            List<GameObject> targets = GetTargetsByScope(scope, action);

            ShowLog($"{action.actor.name}의 아이템 사용: {item.dataName} (대상: {targets.Count}명)");

            // 모든 대상에게 효과 적용
            foreach (var target in targets)
            {
                ApplyItemEffect(target, item);
                // 공격 스킬인 경우 피격 모션/이펙트 처리
                if (item.effectType == EffectType.Special_Atk || item.effectType == EffectType.Magic_Atk)
                {
                    // 데미지/이펙트 처리는 ApplyItemEffect 내부 혹은 별도 로직이 필요할 수 있음
                    // 여기서는 ApplyItemEffect가 데미지를 주는 경우(Special_Atk 등)를 포함한다고 가정
                    SoundManager.Instance.PlaySFX(SfxID.Attack_Magic);
                    SpawnVFX(vfxMagicPrefab, target.transform.position);
                }
                else if (item.effectType == EffectType.Recover_HP || item.effectType == EffectType.Recover_MP ||
                         item.effectType == EffectType.Revive_Empty ||item.effectType == EffectType.Revive_Fully)
                {
                    SoundManager.Instance.PlaySFX(SfxID.Attack_Magic);
                    SpawnVFX(vfxMagicPrefab, target.transform.position);
                }
            }
            
            yield return wait05;
        }

        IEnumerator HandleSkillAction(CombatAction action)
        {
            // [스킬 타겟 자동 변경 로직]
            if (action.target == null || !IsAlive(action.target))
            {
                action.target = FindNearestLivingTarget(action.actor);
                if (action.target == null) yield break; // 대상 없으면 취소
            }

            SkillData skill = action.itemData as SkillData; 
            PlayerController actor = action.actor.GetComponent<PlayerController>();

            // 비용 지불 (MP)
            if (actor != null && skill != null && !skill.useHpCost) actor.currentMp -= skill.costValue;
            
            // 다중 타겟 처리
            TargetScope scope = (skill != null) ? skill.targetScope : TargetScope.FrontSingle;
            List<GameObject> targets = GetTargetsByScope(scope, action);

            ShowLog($"{action.actor.name}의 스킬 발동: {skill.dataName} (대상: {targets.Count}명)");

            foreach (var target in targets)
            {
                ApplyItemEffect(target, skill);
                
                // 공격 스킬인 경우 피격 모션/이펙트 처리
                if (skill.effectType == EffectType.Special_Atk || skill.effectType == EffectType.Magic_Atk)
                {
                    // 데미지/이펙트 처리는 ApplyItemEffect 내부 혹은 별도 로직이 필요할 수 있음
                    // 여기서는 ApplyItemEffect가 데미지를 주는 경우(Special_Atk 등)를 포함한다고 가정
                    SoundManager.Instance.PlaySFX(SfxID.Attack_Magic);
                    SpawnVFX(vfxMagicPrefab, target.transform.position);
                }
                else if (skill.effectType == EffectType.Recover_HP || skill.effectType == EffectType.Recover_MP ||
                         skill.effectType == EffectType.Revive_Empty ||skill.effectType == EffectType.Revive_Fully)
                {
                    SoundManager.Instance.PlaySFX(SfxID.Attack_Magic);
                    SpawnVFX(vfxMagicPrefab, target.transform.position);
                }
            }

            yield return wait05;
        }

        void ApplyItemEffect(GameObject target, BaseRootData item)
        {
            var pTarget = target.GetComponent<PlayerController>();
            switch (item.effectType)
            {
                case EffectType.Recover_HP: if (pTarget) pTarget.Recover(item.effectValue, 0); break;
                case EffectType.Recover_MP: if (pTarget) pTarget.Recover(0, item.effectValue); break;
                case EffectType.Revive_Empty:
                case EffectType.Revive_Fully: if (pTarget && pTarget.currentHp <= 0) pTarget.Revive(item.effectValue); break;
                case EffectType.Special_Atk:
                case EffectType.Magic_Atk: ApplyDamage(target, item.effectValue, false); break;
                case EffectType.Reflect_Phys: if (pTarget) pTarget.isPhysicalReflect = true; break;
                case EffectType.Reflect_Magic: if (pTarget) pTarget.isMagicReflect = true; break;
            }
        }

        IEnumerator HandleGuardAction(CombatAction action)
        {
            SetGuardState(action.actor, true);
            ShowLog($"{action.actor.name}의 방어 태세!");
            yield return wait05;
            if (logPanel) logPanel.SetActive(false);
        }

        IEnumerator HandleUnionAttack(CombatAction action)
        {
            // 타겟 유효성 검사
            if (action.target == null || !IsAlive(action.target))
            {
                action.target = FindNearestLivingTarget(action.actor);
                if (action.target == null)
                {
                    Debug.Log("Union Attack 취소: 유효한 타겟 없음");
                    ShowLog("유효한 타겟 없음");
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
                ShowLog("협동 공격 실패!");
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

            ShowLog("UNION ATTACK!");
            SoundManager.Instance.PlaySFX(SfxID.Attack_Sword);

            // 2. 애니메이션: 타겟 앞으로 모이기
            GameObject target = action.target;
            Vector3 targetBasePos = target.transform.position;
            Vector3 rallyPoint = targetBasePos + new Vector3(0, -2.0f, 0); 

            Dictionary<PlayerController, Vector3> originPositions = new Dictionary<PlayerController, Vector3>();
            Sequence moveSeq = DOTween.Sequence();

            foreach (var p in partners)
            {
                originPositions[p] = p.transform.position; 
                Vector3 randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), 0, 0);
                moveSeq.Join(p.transform.DOMove(rallyPoint + randomOffset, 0.3f).SetEase(Ease.OutBack));
            }
            yield return moveSeq.WaitForCompletion();

            // 3. 타격 및 데미지 계산
            SpawnVFX(vfxSlashPrefab, target.transform.position); 
            SoundManager.Instance.PlaySFX(SfxID.Attack_Sword);

            float critChance = 0.3f + (partners.Count * 0.1f);
            bool allSameAlign = partners.All(p => p.align == leader.align);
            if (allSameAlign) critChance += 0.3f;

            bool isCrit = Random.value < critChance;
            
            int totalStr = partners.Sum(p => p.GetTotalStr());
            float dmgMultiplier = 1.5f; 
            if (allSameAlign) dmgMultiplier = 2.0f; 

            int dmg = CalculateDamage(leader.gameObject, target, action, isCrit, dmgMultiplier);
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
            ShowLog("Formation Changing...");

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
            ShowLog("LAST STAND!!");

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
                
                SpawnVFX(vfxGuardHitPrefab, pc.transform.position); 
                pc.isGuarding = true; 
            }
            yield return seq.WaitForCompletion();
            
            yield return wait05;
            if (logPanel) logPanel.SetActive(false);
        }

        void SetGuardState(GameObject actor, bool state)
        {
            if (actor.TryGetComponent(out PlayerController pc)) pc.isGuarding = state;
            else if (actor.TryGetComponent(out MonsterController mc)) mc.isGuarding = state;
        }

        // 장전 코루틴
        IEnumerator HandleReloadAction(CombatAction action)
        {
            PlayerController actor = action.actor.GetComponent<PlayerController>();
            if (actor != null && actor.currentGun != null)
            {
                // 탄환 최대치로 충전
                actor.currentGunAmmo = actor.currentGun.maxHits;
                
                ShowLog("RELOADED FULL AMMO!");
                // SoundManager.Instance.PlaySFX(SfxID.Reload); // 장전 효과음
                
                // 간단한 연출 위로 살짝 뛰기
                yield return actor.transform.DOLocalMoveY(10f, 0.2f).SetLoops(2, LoopType.Yoyo).WaitForCompletion();
            }
            yield return wait05;
            if (logPanel) logPanel.SetActive(false);
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
                    // ShowLog("Target Changed!"); 
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
                    ShowLog($"{action.actor.name} 탄환 부족!\nReload 필요!");
                    
                    // 실패 효과음
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel); 

                    // 메시지를 읽을 시간을 주고 턴 종료 (공격 애니메이션 실행 X)
                    yield return wait10; 
                    yield break; 
                }
            }

            string actStr = (action.type == ActionType.Shoot) ? "의 사격!" : "의 참격!";
            ShowLog($"{action.actor.name}{actStr}");
            yield return wait05;

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
                if (qteTimingSlider)
                {
                    qteTimingSlider.gameObject.SetActive(true);
                    qteTimingSlider.minValue = 0f;
                    qteTimingSlider.maxValue = 1.0f;
                    qteTimingSlider.value = 1.0f;
                    qteTimingSlider.interactable = false;
                }

                float qteDuration = 2.0f; 
                float timer = 0f;
                if (logPanel) logText.text = "SHOOT IT IN THE HEAD!! (Space/Enter)";
                while (timer < qteDuration && currentHits < maxHits)
                {
                    timer += Time.deltaTime;
                    if (qteTimingSlider) qteTimingSlider.value = 1.0f - (timer / qteDuration);
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    {
                        List<GameObject> currentTargets = GetTargetsByScope(scope, action);
                        if (currentTargets.Count == 0) break;
                        foreach (var target in currentTargets) StartCoroutine(ProcessSingleHit(action, target));
                        currentHits++;
                        hitsPerformed++; // 실제 발사 수 증가
                        BattleEntity actorEntity = action.actor.GetComponent<BattleEntity>();
                        if (actorEntity) actorEntity.nextTurnSpeedPenalty += 500;
                        if (logPanel) logText.text = $"Combo! ({currentHits}/{maxHits})";
                        SoundManager.Instance.PlaySFX(SfxID.Attack_Gun); 
                    }
                    yield return null; 
                }
                if (qteTimingSlider) qteTimingSlider.gameObject.SetActive(false);
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
                if (scope == TargetScope.FrontAll || scope == TargetScope.AnyAll) break;
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

        // 유닛 애니메이션 통합 함수
        IEnumerator AnimateUnitVisual(Transform target, Vector3 toPos, Vector3 toScale, float duration = 0.15f)
        {
            Sequence seq = DOTween.Sequence();
            seq.Join(target.DOLocalMove(toPos, duration).SetEase(Ease.OutQuad));
            seq.Join(target.DOScale(toScale, duration).SetEase(Ease.OutQuad));
            yield return seq.WaitForCompletion();
        }

        IEnumerator ProcessSingleHit(CombatAction action, GameObject target)
        {
            GetPositionalModifiers(action.actor, target, action, out float posDmgMult, out float posEvaBonus);

            if (CheckEvasion(action.actor, target, posEvaBonus))
            {
                Debug.Log($"{target.name} 회피!");
                yield return StartCoroutine(ProcessDodgeAnimation(target.transform));
                yield break; 
            }

            if (CheckReflection(target, action.type))
            {
                ShowLog("반사!");
                SpawnVFX(vfxReflectPrefab, target.transform.position);
                int reflectDmg = CalculateDamage(action.actor, action.actor, action, false, 1.0f);
                ApplyDamage(action.actor, reflectDmg, false); 
                yield break;
            }

            if (CheckAbsorption(target, action.type))
            {
                ShowLog("흡수!");
                SpawnVFX(vfxAbsorbPrefab, target.transform.position);
                int absorbAmount = CalculateDamage(action.actor, target, action, false, 1.0f);
                var targetEntity = target.GetComponent<BattleEntity>();
                if (targetEntity is PlayerController pc) pc.Recover(absorbAmount, 0);
                else if (targetEntity is MonsterController mc) mc.currentHp = Mathf.Min(mc.currentHp + absorbAmount, mc.maxHp);
                yield break; 
            }

            if (isLastStandActive && target.GetComponent<PlayerController>() != null)
            {
                List<PlayerController> defenders = activePlayers.Where(p => p.currentHp > 0 && p.columnIndex < 3).Select(p => p as PlayerController).ToList();
                if (defenders.Count > 0)
                {
                    bool isCrit = CheckCritical(action.actor, target, action);
                    int originalDamage = CalculateDamage(action.actor, target, action, isCrit, posDmgMult);
                    int splitDamage = Mathf.Max(1, originalDamage / defenders.Count);
                    ShowLog("방어!");
                    foreach (var defender in defenders)
                    {
                        ApplyDamage(defender.gameObject, splitDamage, false);
                        SpawnVFX(vfxGuardHitPrefab, defender.transform.position);
                    }
                    yield return wait01;
                    yield break; 
                }
            }

            bool isCritical = CheckCritical(action.actor, target, action);
            int damage = 0;

            if (action.type == ActionType.Shoot && action.actor.GetComponent<PlayerController>())
                damage = CalculateGunDamage(action.actor.GetComponent<PlayerController>(), target, isCritical);
            else
                damage = CalculateDamage(action.actor, target, action, isCritical, posDmgMult);

            BattleEntity defenderEntity = target.GetComponent<BattleEntity>();
            if (defenderEntity != null && defenderEntity.isGuarding)
            {
                SpawnVFX(vfxGuardHitPrefab, target.transform.position);
                yield return wait01;
            }
            else
            {
                var sfxId = SfxID.None;
                GameObject vfxToSpawn = null;
                if (action.type == ActionType.Attack) { sfxId = SfxID.Attack_Sword; vfxToSpawn = vfxSlashPrefab; }
                else if (action.type == ActionType.Shoot) { sfxId = SfxID.Attack_Gun; vfxToSpawn = vfxGunPrefab; }
                else if (action.type == ActionType.Skill) { sfxId = SfxID.Attack_Magic; vfxToSpawn = vfxMagicPrefab; }
                else if (action.type == ActionType.Item)
                {
                    if (action.itemData.effectType == EffectType.Special_Atk || action.itemData.effectType == EffectType.Magic_Atk)
                    { sfxId = SfxID.Attack_Magic; vfxToSpawn = vfxMagicPrefab; }
                }

                if (sfxId != SfxID.None) SoundManager.Instance.PlaySFX(sfxId);
                SpawnVFX(vfxToSpawn, target.transform.position);
                yield return wait01;
            }

            ApplyDamage(target, damage, isCritical);
        }

        void SpawnVFX(GameObject vfxPrefab, Vector3 position)
        {
            if (vfxPrefab != null) Instantiate(vfxPrefab, new Vector3(position.x, position.y, -5f), Quaternion.identity);
        }

        void ApplyDamage(GameObject target, int damage, bool isCritical)
        {
            // 1. 타겟이 없거나, 비활성화 상태라면 아무것도 하지 않고 리턴
            if (target == null || !target.activeInHierarchy) return;

            var entity = target.GetComponent<BattleEntity>();
            if (entity != null)
            {
                entity.TriggerHitShake(isCritical); 
                StartCoroutine(entity.OnDamageTaken(damage)); 
            }
        }

        bool CheckAbsorption(GameObject target, ActionType type)
        {
            var entity = target.GetComponent<BattleEntity>();
            if (entity == null) return false;
            bool isPhysical = (type == ActionType.Attack || type == ActionType.Shoot);
            bool isMagic = (type == ActionType.Skill); 
            if (isPhysical && entity.isPhysicalAbsorb) return true;
            if (isMagic && entity.isMagicAbsorb) return true;
            return false;
        }

        bool CheckReflection(GameObject target, ActionType type)
        {
            var entity = target.GetComponent<BattleEntity>();
            if (entity == null) return false;
            bool isPhysical = (type == ActionType.Attack || type == ActionType.Shoot);
            bool isMagic = (type == ActionType.Skill); 
            if (isPhysical && entity.isPhysicalReflect) return true;
            if (isMagic && entity.isMagicReflect) return true;
            return false;
        }

        List<GameObject> GetTargetsByScope(TargetScope scope, CombatAction action)
        {
            List<GameObject> targets = new List<GameObject>();
            var livingMonsters = activeMonsters.Where(m => m.currentHp > 0).ToList();
            var livingPlayers = activePlayers.Where(p => p.currentHp > 0).ToList(); // 아군 생존자

            // 1. 단일 지정 (이미 타겟이 정해진 경우)
            if (scope == TargetScope.FrontSingle || scope == TargetScope.AnySingle || 
                scope == TargetScope.OneAlly || scope == TargetScope.DeadAlly)
            {
                if (action.target != null) targets.Add(action.target);
            }
            // 2. 적 랜덤 / 전체
            else if (scope == TargetScope.FrontRandom || scope == TargetScope.AnyRandom)
            {
                List<GameObject> candidates = new List<GameObject>();
                foreach(var m in livingMonsters) 
                {
                    bool isFront = (m.transform.parent.parent == enemyFrontRowContainer);
                    if (scope == TargetScope.FrontRandom && !isFront) continue;
                    candidates.Add(m.gameObject);
                }
                if (candidates.Count > 0) targets.Add(candidates[Random.Range(0, candidates.Count)]);
            }
            else if (scope == TargetScope.FrontAll || scope == TargetScope.AnyAll)
            {
                foreach(var m in livingMonsters) 
                {
                    bool isFront = (m.transform.parent.parent == enemyFrontRowContainer);
                    if (scope == TargetScope.FrontAll && !isFront) continue;
                    targets.Add(m.gameObject);
                }
                if (scope == TargetScope.FrontAll && targets.Count == 0) targets.AddRange(livingMonsters.Select(m => m.gameObject));
            }
            // 3. 아군 관련 타겟 (AllAllies, Self 등)
            else if (scope == TargetScope.AllAllies)
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

        // [성향 호환성 체크 헬퍼]
        bool IsAlignCompatible(Align a, Align b)
        {
            // 같거나, 둘 중 하나라도 Neutral(True_Neutral)이면 호환
            return a == b || a == Align.True_Neutral || b == Align.True_Neutral;
        }

        // [Union 참가 가능 파트너 찾기]
        List<PlayerController> GetValidUnionPartners(PlayerController leader)
        {
            List<PlayerController> partners = new List<PlayerController>();
            partners.Add(leader); // 리더(자기 자신) 포함

            // 리더가 전열(0, 1, 2)이 아니면 불가
            if (leader.columnIndex >= 3) return partners;

            // [변경] 현재 캐릭터의 왼쪽(-1), 오른쪽(+1) 이웃만 검사
            int[] neighborIndices = { leader.columnIndex - 1, leader.columnIndex + 1 };

            foreach (int i in neighborIndices)
            {
                // 인덱스 범위 체크 (전열 0~2)
                if (i < 0 || i > 2) continue;

                PlayerController p = allSlotControllers[i];

                // 1. 기본 상태 체크 (존재함, 빈 슬롯 아님, 살아있음)
                if (p == null || p.IsEmpty || p.currentHp <= 0) continue;

                // 2. 성향(Align) 호환성 체크
                if (!IsAlignCompatible(leader.align, p.align)) continue;

                // 3. 행동 예약 상태 체크. 이미 행동 큐에 등록된 행동이 있는지 확인
                CombatAction reservedAction = actionQueue.Find(a => a.actor == p.gameObject);

                // 조건: 행동이 아직 예약되지 않았거나(미행동), 예약된 행동이 'Next'인 경우만 가능
                if (reservedAction == null || reservedAction.type == ActionType.Next)
                {
                    partners.Add(p);
                }
            }

            return partners;
        }

        void ShowLog(string msg) { if (logPanel) { logPanel.SetActive(true); logText.SetText(msg); } }

        void GetWeaponInfo(CombatAction action, out int min, out int max, out TargetScope scope)
        {
            min = 1; max = 1; scope = TargetScope.FrontSingle; 
            var pActor = action.actor.GetComponent<PlayerController>();
            WeaponData weapon = null;
            if (pActor != null) weapon = (action.type == ActionType.Shoot) ? pActor.currentGun : pActor.currentWeapon;
            if (weapon != null) { min = weapon.minHits; max = weapon.maxHits; scope = weapon.attackRange; }
        }

        int CalculateGunDamage(PlayerController attacker, GameObject defender, bool isCritical)
        {
            int baseAtk = attacker.GetGunAttack();
            int def = 0;
            if (defender.TryGetComponent(out MonsterController mc)) def = mc.GetTotalVit();
            float rawDmg = Mathf.Max(1, baseAtk - (def * 0.5f));
            if (isCritical) rawDmg *= 1.5f; 
            return Mathf.RoundToInt(rawDmg);
        }

        bool IsAlive(GameObject obj) { return obj != null && obj.activeSelf && (obj.GetComponent<BattleEntity>()?.IsAlive ?? false); }

        // 아군 위치 이동(Move) 애니메이션
        IEnumerator PerformMove(CombatAction action)
        {
            PlayerController actor = action.actor.GetComponent<PlayerController>();
            if (actor == null || actor.currentHp <= 0) yield break;

            Transform targetSlotTransform = action.target.transform; 
            Transform originSlotTransform = actor.transform.parent;

            if (isLastStandActive)
            {
                int targetIndex = GetPlayerSlotIndex(targetSlotTransform);
                int originIndex = GetPlayerSlotIndex(originSlotTransform);
                if (targetIndex < 3 || originIndex < 3)
                {
                    if (logPanel) { logPanel.SetActive(true); logText.SetText("LAST STAND 발동중.\n이동 불가!"); }
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                    yield return wait10; 
                    if (logPanel) logPanel.SetActive(false);
                    yield break; 
                }
            }

            if (targetSlotTransform == originSlotTransform) yield break;

            PlayerController targetChar = targetSlotTransform.GetComponentInChildren<PlayerController>();

            if (messagePanel) { messagePanel.SetActive(true); messageText.SetText((targetChar != null && !targetChar.IsEmpty) ? "위치 교대!" : "자리 이동!"); }
            Debug.Log($"[Action] {actor.name} 이동: {originSlotTransform.name} -> {targetSlotTransform.name}");

            int actorListIndex = allSlotControllers.IndexOf(actor);
            int targetListIndex = (targetChar != null) ? allSlotControllers.IndexOf(targetChar) : -1;

            if (actorListIndex != -1 && targetListIndex != -1)
            {
                allSlotControllers[actorListIndex] = targetChar;
                allSlotControllers[targetListIndex] = actor;
            }

            if (targetChar != null) { targetChar.transform.SetParent(originSlotTransform, true); targetChar.columnIndex = GetPlayerSlotIndex(originSlotTransform); }
            actor.transform.SetParent(targetSlotTransform, true);
            actor.columnIndex = GetPlayerSlotIndex(targetSlotTransform);

            SoundManager.Instance.PlaySFX(SfxID.UI_Click); 

            Sequence seq = DOTween.Sequence();
            seq.Join(actor.transform.DOLocalMove(Vector3.zero, 0.5f).SetEase(Ease.OutQuad));
            if (targetChar != null) seq.Join(targetChar.transform.DOLocalMove(Vector3.zero, 0.5f).SetEase(Ease.OutQuad));
            
            yield return seq.WaitForCompletion();
            yield return wait05; 
            if (messagePanel) messagePanel.SetActive(false);
        }

        void InitializeSlots()
        {
            if (frontSlots.Count == 0) CreateSlotsFor(enemyFrontRowContainer, frontSlots);
            if (backSlots.Count == 0) CreateSlotsFor(enemyBackRowContainer, backSlots);
            ClearSlotContents(frontSlots); ClearSlotContents(backSlots);

            if (playerFrontSlots.Count == 0) CreateSlotsFor(playerFrontRowContainer, playerFrontSlots);
            if (playerBackSlots.Count == 0) CreateSlotsFor(playerBackRowContainer, playerBackSlots);
            ClearSlotContents(playerFrontSlots); ClearSlotContents(playerBackSlots);
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

            List<Transform> targetSlots = (entry.preferredRow == RowType.Front) ? frontSlots : backSlots;
            if (IsRowFull(targetSlots))
            {
                targetSlots = (targetSlots == frontSlots) ? backSlots : frontSlots;
                if (IsRowFull(targetSlots)) return;
            }

            List<int> emptyIndices = new List<int>();
            for (int i = 0; i < targetSlots.Count; i++) if (targetSlots[i].childCount == 0) emptyIndices.Add(i);

            int randomIndex = emptyIndices[Random.Range(0, emptyIndices.Count)];
            Transform selectedSlot = targetSlots[randomIndex];

            GameObject prefabToUse = (entry.prefab != null) ? entry.prefab : defaultMonsterPrefab;
            if (prefabToUse == null) return;

            GameObject newMonsterObj = Instantiate(prefabToUse, selectedSlot);
            newMonsterObj.transform.localPosition = Vector3.zero;

            MonsterController controller = newMonsterObj.GetComponentInChildren<MonsterController>();
            if (controller == null) { Destroy(newMonsterObj); return; }

            controller.Initialize(entry);
            newMonsterObj.name = $"{controller.sourceData.race} {controller.sourceData.name}";

            if (controller.currentHp <= 0) { Destroy(newMonsterObj); return; }

            controller.SetPositionInfo(randomIndex);
            bool isFront = (targetSlots == frontSlots);
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
            foreach (Transform child in enemyFrontRowContainer) Destroy(child.gameObject);
            foreach (Transform child in enemyBackRowContainer) Destroy(child.gameObject);
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

        void GetPositionalModifiers(GameObject attacker, GameObject defender, CombatAction action, out float damageMultiplier, out float evasionBonus)
        {
            damageMultiplier = 1.0f;
            evasionBonus = 0f;

            CombatPosition atkPos = GetUnitPosition(attacker);
            CombatPosition defPos = GetUnitPosition(defender);

            WeaponType wType = WeaponType.Melee;
            PlayerController pActor = attacker.GetComponent<PlayerController>();
            if (pActor != null)
            {
                if (action.type == ActionType.Shoot) wType = WeaponType.Gun;
                else if (pActor.currentWeapon != null && pActor.currentWeapon.type == WeaponType.Gun) wType = WeaponType.Gun;
            }

            if (wType == WeaponType.Melee)
            {
                if (!atkPos.isFrontRow) damageMultiplier *= 0.7f;
                if (!defPos.isFrontRow) { damageMultiplier *= 0.8f; evasionBonus += 0.1f; }
            }

            int colDiff = Mathf.Abs(atkPos.columnIndex - defPos.columnIndex);
            if (colDiff == 1) damageMultiplier *= 0.95f; 
            else if (colDiff >= 2) { damageMultiplier *= 0.90f; evasionBonus += 0.05f; }
        }

        private bool CheckEvasion(GameObject attackerObj, GameObject defenderObj, float evasionBonus)
        {
            var attacker = attackerObj.GetComponent<BattleEntity>();
            var defender = defenderObj.GetComponent<BattleEntity>();
            if (attacker == null || defender == null) return false;

            int attackerAgi = attacker.GetTotalAgi();
            int attackerLuc = attacker.GetTotalLuc();
            int defenderAgi = defender.GetTotalAgi();
            int defenderLuc = defender.GetTotalLuc();

            float baseEvasionChance = 0.05f; 
            float agiBonus = Mathf.Clamp((defenderAgi - attackerAgi) * 0.01f, -0.2f, 0.2f);
            float lucBonus = Mathf.Clamp((defenderLuc - attackerLuc) * 0.005f, -0.1f, 0.1f);
            float totalChance = Mathf.Clamp(baseEvasionChance + agiBonus + lucBonus + evasionBonus, 0f, 0.9f);

            return Random.value < totalChance;
        }
        
        private bool CheckCritical(GameObject attackerObj, GameObject defenderObj, CombatAction action)
        {
            var attacker = attackerObj.GetComponent<BattleEntity>();
            var defender = defenderObj.GetComponent<BattleEntity>();
            if (attacker == null || defender == null) return false;

            bool isMagic = (action.skillData != null && action.skillData.element != ElementType.Physical);
            int atkLuc = attacker.GetTotalLuc();
            int atkMainStat = isMagic ? attacker.GetMagicAttack() : attacker.GetAttack();
            int defLuc = defender.GetTotalLuc();
            int defAgi = defender.GetTotalAgi();

            float baseCritChance = 0.05f; 
            float lucBonus = (atkLuc - defLuc) * 0.002f; 
            float statBonus = (atkMainStat - defAgi) * 0.001f; 
            float totalChance = Mathf.Clamp(baseCritChance + lucBonus + statBonus, 0f, 0.7f);

            return Random.value < totalChance;
        }

        public int CalculateDamage(GameObject attackerObj, GameObject defenderObj, CombatAction action, bool isCritical, float damageMultiplier)
        {
            var attacker = attackerObj.GetComponent<BattleEntity>();
            var defender = defenderObj.GetComponent<BattleEntity>();
            if (attacker == null || defender == null) return 0;

            int baseAtk = attacker.GetTotalStr(); 
            int skillPower = 0;
            if (action.type == ActionType.Skill || action.type == ActionType.Item)
                if (action.itemData != null) skillPower = action.itemData.effectValue; 

            int totalAtk = baseAtk + skillPower;
            bool isGuarding = defender.isGuarding;
            float resistanceValue = GetResistanceValue(action.skillData, defender.GetResistances()); 
            int totalDef = defender.GetDefense();
            
            float rawDamage = Mathf.Max(1, totalAtk - (totalDef * 0.5f));
            // 성향 상성 보정 추가
            // StatData에 align이 있다고 가정 (sourceData.align)
            Align attAlign = attacker.align;
            Align defAlign = defender.align;

            float alignBonus = AlignmentSystem.GetDamageModifier(attAlign, defAlign);
            
            // 기존 Multiplier에 곱하기
            rawDamage *= damageMultiplier * alignBonus;

    // 디버그용 (필요시)
    // if (alignBonus > 1.0f) Debug.Log("상성 우위! 데미지 증가");

            float resistanceMultiplier = 1.0f - resistanceValue;
            float randomVar = Random.Range(0.9f, 1.1f);
            int finalDamage = Mathf.RoundToInt(rawDamage * resistanceMultiplier * randomVar);

            if (isCritical) finalDamage *= 2;
            if (isGuarding) { finalDamage = Mathf.FloorToInt(finalDamage * 0.5f); Debug.Log("방어 성공! 데미지 50% 감소"); }
            if (finalDamage < 1) finalDamage = 1;

            return finalDamage;
        }

        private float GetResistanceValue(BaseRootData data,ResistanceData resist)
        {
            if (data == null) return resist.physical; 
            switch(data.element)
            {
                case ElementType.Fire: return resist.fire;
                case ElementType.Ice: return resist.ice;
                case ElementType.Elec: return resist.elec;
                case ElementType.Force: return resist.force;
                case ElementType.Havoc: return resist.havoc;
                default: return resist.physical;
            }
        }

        bool CalculateEscapeSuccess()
        {
            List<BattleEntity> livingPlayers = activePlayers.Where(p => p.currentHp > 0).ToList();
            if (livingPlayers.Count == 0) return false;

            float playerAvgAgi = (float)livingPlayers.Average(p => p.GetTotalAgi());
            float playerAvgLuc = (float)livingPlayers.Average(p => p.GetTotalLuc());

            List<BattleEntity> livingMonsters = activeMonsters.Where(m => m.currentHp > 0).ToList();
            if (livingMonsters.Count == 0) return true; 

            float enemyAvgAgi = (float)livingMonsters.Average(m => m.GetTotalAgi());
            float enemyAvgLuc = (float)livingMonsters.Average(m => m.GetTotalLuc());

            float baseChance = 50f;
            float agiBonus = (playerAvgAgi - enemyAvgAgi) * 2.0f;
            float lucBonus = (playerAvgLuc - enemyAvgLuc) * 1.0f;
            float finalChance = Mathf.Clamp(baseChance + agiBonus + lucBonus, 10f, 100f);

            return Random.Range(0f, 100f) < finalChance;
        }

        IEnumerator EndBattleRoutine(bool isWin)
        {
            state = isWin ? BattleState.Won : BattleState.Lost;
            if (commandPanel) commandPanel.SetActive(false);
            if (logPanel) logPanel.SetActive(true);

            if (isWin) { logText.text = "승리는 나의 것!"; SoundManager.Instance.PlayBGM(BgmID.Victory); }
            else logText.text = "패배는 너의 것!";

            yield return wait05;
            while (!Input.GetKeyDown(KeyCode.Space) && !Input.GetKeyDown(KeyCode.Return)) yield return null;

            logPanel.SetActive(false);
            DungeonStateManager.Instance.ChangeState(GameState.Exploration);
        }

        private void ClearParty()
        {
            lastHighlightedPlayer = null; 
            activePlayers.Clear();
        }
    }
}