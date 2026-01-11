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
namespace Manager
{
    public enum BattleState { Start, PlayerInput, EnemyInput, Processing, Won, Lost }
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance;

        [Header("UI References")]
        public GameObject baseCmdContainer;   // 1단계 메뉴 (Fight, Talk...)
        public GameObject fightCmdContainer;  // 2단계 메뉴 (Attack, Move...)
        public BattleItemUIController battleItemUI; // 인스펙터에서 할당
        public GameObject commandPanel;     // 커맨드 버튼들
        public GameObject logPanel;         // 시스템 로그 패널
        public TextMeshProUGUI logText;     // 시스템 안내 메시지 텍스트
        public GameObject messagePanel;     // 전투 중 캐릭터의 메시지 패널
        public TextMeshProUGUI messageText; // 전투 중 캐릭터의 메시지 텍스트

        [Header("Prefabs")]
        public GameObject defaultMonsterPrefab;
        public GameObject playerPrefab;

        [Header("First Focus Buttons")]
        public GameObject baseFirstButton;    // Base 메뉴의 첫 버튼 (Fight 버튼)
        public GameObject attackButton;    // Fight 메뉴의 첫 버튼 (Attack 버튼)

        public RectTransform targetCursor; // 아까 만든 손가락 커서 이미지
        public Vector3 cursorOffset = new Vector3(0, 50, 0); // 몬스터 머리 위 오프셋

        // 타겟팅 로직 변수
        private List<BattleEntity> validTargets = new List<BattleEntity>();
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
        private List<Transform> playerFrontSlots = new List<Transform>();
        private List<Transform> playerBackSlots = new List<Transform>();

        //이동 모드 관련 변수
        private PlayerController lastHighlightedPlayer; // 마지막으로 하이라이트된 플레이어를 기억할 변수
        private bool isSelectingMoveTarget = false;
        private int currentMoveSlotIndex = 0; // 0~2: 전열, 3~5: 후열

        [Header("Highlight Colors")]
        public Color moveSourceColor = Color.gray;   // 이동하려는 내 캐릭터 색상
        public Color moveTargetColor = Color.cyan;  // 커서가 가리키는 대상 색상

        [Header("Slot Management")]
        // 몬스터들의 슬롯을 관리할 리스트 (0,1,2: 전열 / 0,1,2: 후열)
        private List<Transform> frontSlots = new List<Transform>();
        private List<Transform> backSlots = new List<Transform>();
        
        private ConsumableItemData currentSelectedItem; // 현재 사용하려는 아이템
        private bool isAutoMode = false; // 오토 모드 활성화 여부
        
        // 각 캐릭터(인덱스)가 마지막으로 수행한 행동 타입 저장
        private Dictionary<int, CombatAction.ActionType> lastPlayerActions = new Dictionary<int, CombatAction.ActionType>();

        // -------------------------------------------------------
        // [핵심 변수] 전투 상태 및 리스트
        // -------------------------------------------------------
        public BattleState state;
        public List<BattleEntity> activeMonsters = new List<BattleEntity>();
        public List<BattleEntity> activePlayers = new List<BattleEntity>(); // 아군 리스트

        public List<CombatAction> actionQueue = new List<CombatAction>(); // 이번 턴의 모든 행동

        // 입력 제어용 변수
        private int currentPlayerIndex = 0; // 지금 누구 차례?
        private CombatAction.ActionType currentSelectedAction;
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

        // 자주 쓰는 딜레이 캐싱
        private WaitForSeconds wait01 = new WaitForSeconds(0.1f);
        private WaitForSeconds wait05 = new WaitForSeconds(0.5f);
        
        void Awake() { if (Instance == null) Instance = this; }

        // DungeonStateManager에서 호출
        public void Initialize(List<string> monsterIds)
        {
            // =========================================================
            // 전투 시작 시 모든 상태 플래그와 속도 리셋
            // =========================================================
            isAutoMode = false;         // 오토 모드 해제
            isFightMode = false;        // 메뉴 상태 초기화 (Base 메뉴부터 시작)
            Time.timeScale = 1.0f;      // 게임 속도 정상화 (혹시 2배속이었다면 복구)
            
            // 상태 초기화
            state = BattleState.Start;

            // 메시지 표시 초기화
            if (logPanel)
            {
                logPanel.SetActive(false);
                logText.SetText(string.Empty);
            }
            if (messagePanel)
            {
                messagePanel.SetActive(false);
                messageText.SetText(string.Empty);
            }
            // 기존 데이터 초기화
            activeMonsters.Clear(); 
            ClearParty();           

            InitializeSlots();

            // =========================================================
            // 리스트 개수에 따른 스폰 수량 제한 로직
            // =========================================================
            
            if (monsterIds == null || monsterIds.Count == 0) 
            {
                Debug.LogWarning("초기화할 몬스터 ID 리스트가 비어있습니다.");
                return;
            }

            SoundManager.Instance.PlayBGM(BgmID.Encounter);

            // 1. 최대 스폰 가능 수 결정
            // 리스트가 6개 미만(예: 2개)이면 최대 2마리까지만, 
            // 6개 이상이면 최대 6마리까지만 등장하도록 제한합니다.
            int maxSpawnLimit = Mathf.Min(monsterIds.Count, 6);

            // 2. 실제 스폰할 수 결정 (1 ~ maxSpawnLimit)
            // 예: monsterIds가 ["Boss"] 1개라면 -> Random.Range(1, 2) -> 1마리 확정
            // 예: monsterIds가 ["A", "B"] 2개라면 -> 1~2마리 랜덤
            int spawnCount = Random.Range(1, maxSpawnLimit + 1); 

            Debug.Log($"[Encounter] 몬스터 {spawnCount}마리가 출현합니다! (Pool Size: {monsterIds.Count})");

            // 3. 결정된 수만큼 소환
            for (int i = 0; i < spawnCount; i++)
            {
                // 몬스터 ID 풀에서 랜덤 선택 (중복 허용)
                int randomIndex = Random.Range(0, monsterIds.Count);
                string selectedId = monsterIds[randomIndex];

                SpawnMonster(selectedId);
            }
            
            // =========================================================

            SpawnParty();

            if (activePlayers.Count == 0)
            {
                Debug.LogError("오류: 아군 파티원이 한 명도 없습니다! PartyManager 데이터를 확인하세요.");
                // 전투 진행을 막거나 강제 종료
                return; 
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(enemyFrontRowContainer as RectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(playerFrontRowContainer as RectTransform);

            StartCoroutine(SetupBattle());
        }

        void SpawnParty()
        {
            // 0~2번: 전열, 3~5번: 후열
            for (int i = 0; i < 6; i++)
            {
                var data = PartyManager.Instance.GetMemberData(i);
                if (data == null) continue; // 데이터 없으면 스킵

                // 바로 컨테이너에 넣지 않고, 해당 인덱스의 '슬롯'을 찾아서 넣음
                bool isFront = (i < 3);
                List<Transform> targetSlots = isFront ? playerFrontSlots : playerBackSlots;
                int slotIndex = isFront ? i : (i - 3);
                
                // 해당 슬롯 가져오기
                Transform targetSlot = targetSlots[slotIndex];

                // 생성 (부모를 슬롯으로 지정)
                GameObject go = Instantiate(playerPrefab, targetSlot);
                go.transform.localPosition = Vector3.zero; // 슬롯 중앙 정렬
                
                PlayerController pc = go.GetComponent<PlayerController>();
                pc.Initialize(data, isFront ? RowType.Front : RowType.Back);
                activePlayers.Add(pc);
            }
        }

        // -----------------------------------------------------------
        // 1. 플레이어 턴 시작
        // -----------------------------------------------------------
        IEnumerator SetupBattle()
        {
            yield return new WaitForSeconds(1f);
            
            // 첫 번째 턴 시작!
            PreparePlayerTurn();
        }

        // =================================================================
        // 2. 플레이어 입력 단계 (순차적 입력)
        // =================================================================
        // [공통 함수] 무기(또는 맨손)에 따른 타겟팅 준비
        private void PrepareWeaponAction(WeaponData weapon, CombatAction.ActionType actionType)
        {
            BattleEntity currentActor = activePlayers[currentPlayerIndex];

            // =========================================================
            // [핵심 수정] 무기가 없으면(null) -> 맨손 공격 설정 (전열 1명)
            // =========================================================
            TargetScope scope = TargetScope.FrontSingle; 
            
            if (weapon != null) 
            {
                scope = weapon.attackRange;
            }
            else
            {
                // 무기가 없는데 Gun 타입 행동을 하려 한다면 차단 (맨손 총격은 불가능)
                if (actionType == CombatAction.ActionType.Gun) 
                {
                    Debug.Log("총이 없어 사격 불가");
                    return; 
                }
                // Attack 타입이라면 맨손 공격 허용 (scope = FrontSingle 유지)
            }
            // =========================================================

            // 1. 단일 타겟 지정 (Single) -> 커서 띄우기
            if (scope == TargetScope.FrontSingle || scope == TargetScope.AnySingle)
            {
                // 타겟 후보 필터링
                validTargets = activeMonsters.Where(m => m.currentHp > 0).ToList();

                // FrontSingle인 경우 전열만 남김
                if (scope == TargetScope.FrontSingle)
                {
                    validTargets = validTargets.Where(m => m.transform.parent.parent == enemyFrontRowContainer).ToList();
                    
                    // 만약 전열이 전멸했으면? -> 후열 공격 허용 (자동 보정)
                    if (validTargets.Count == 0)
                    {
                        validTargets = activeMonsters.Where(m => m.currentHp > 0).ToList();
                    }
                }
                
                // 정렬 (화면상 위치 순서)
                validTargets = validTargets.OrderBy(m => m.transform.parent.parent == enemyBackRowContainer)
                                            .ThenBy(m => m.transform.position.x).ToList();

                if (validTargets.Count == 0) return; // 칠 적이 없음

                // UI 세팅
                currentSelectedAction = actionType;
                isSelectingTarget = true;
                
                commandPanel.SetActive(false);
                if (logPanel) { logPanel.SetActive(true); logText.text = "SELECT TARGET"; }
                
                currentTargetIndex = 0;
                UpdateTargetHighlight();
                inputCooldown = 0.2f;
            }
            // 2. 광역/랜덤 타겟 (Random / All) -> 즉시 행동 예약
            else
            {
                currentSelectedAction = actionType;
                
                // 속도 계산
                int speed = currentActor.GetTotalAgi() + Random.Range(0, 5);
                
                CombatAction action = new CombatAction(currentActor.gameObject, null, actionType, speed);
                actionQueue.Add(action);

                Debug.Log($"{currentActor.name}: 공격 예약 (범위: {scope})");

                NextPlayerInput();
            }
        }

        void PreparePlayerTurn()
        {
            // 턴 시작 시퀀스(이동 -> 입력) 실행
            StartCoroutine(PreparePlayerTurnRoutine());
        }

        // 실제 턴 준비 시퀀스
        IEnumerator PreparePlayerTurnRoutine()
        {
            // 턴 시작 시 모든 아군의 방어 상태 및 일시적 상태 초기화
            foreach (var player in activePlayers)
            {
                player.ResetStatus(); // isGuarding = false
            }

            // 적군(몬스터) 상태 초기화
            foreach (var monster in activeMonsters)
            {
                monster.ResetStatus(); // isGuarding = false
            }

            // 1. 적들의 전열 이동 처리
            yield return StartCoroutine(ProcessEnemyRowShift());

            // 2. 플레이어 입력 상태 초기화
            state = BattleState.PlayerInput;
            actionQueue.Clear(); 
            currentPlayerIndex = -1; 
            
            // [핵심] 턴 시작 시에는 항상 Base 메뉴(싸우다/도망)부터 시작하도록 초기화
            isFightMode = false;

            Debug.Log("== 플레이어 페이즈 시작 ==");

            // 3. 첫 번째 플레이어 UI 표시
            NextPlayerInput(); 
        }
        
        void Update()
        {
            // 오토 모드 중 취소 키(Esc, Shift) 입력 감지 -> 해제
            if (isAutoMode)
            {
                if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.LeftShift))
                {
                    isAutoMode = false;
                    Debug.Log("오토 모드 해제 예약: 다음 턴부터 수동 조작");
                }
            }

            if (inputCooldown > 0) inputCooldown -= Time.deltaTime;

            if (state == BattleState.PlayerInput)
            {
                // 오토 모드일 때는 입력 처리를 건너뜀 (이미 NextPlayerInput에서 처리됨)
                if (isAutoMode) return;
                Time.timeScale = 1.0f;

                // 아이템 UI가 켜져 있다면 CombatManager의 모든 입력을 중단하고 리턴
                // 이렇게 해야 방향키 입력이 CombatManager로 새지 않고 아이템 UI 안에서만 돕니다.
                if (battleItemUI != null && battleItemUI.gameObject.activeSelf)
                {
                    return; 
                }

                if (isSelectingTarget)
                {
                    if (inputCooldown <= 0) HandleTargetSelectionInput();
                }
                // 이동 타겟 선택 모드
                else if (isSelectingMoveTarget)
                {
                    if (inputCooldown <= 0) HandleMoveSelectionInput();
                }
                else // 커맨드 모드
                {
                    if (inputCooldown <= 0) HandleCommandInput();
                }

                bool isItemUIPopupActive = (battleItemUI != null && battleItemUI.gameObject.activeSelf);
                // 포커스 유지 로직 (이동 선택 중이 아닐 때만)
                if (!isSelectingTarget && !isSelectingMoveTarget && !isItemUIPopupActive && commandPanel.activeSelf)
                {
                    MaintainSelection();
                }
            }
        }

        // 포커스가 풀리면 복구하는 함수
        void MaintainSelection()
        {
            // 1. 현재 선택된 오브젝트가 있는지 확인
            if (EventSystem.current.currentSelectedGameObject != null)
            {
                // 있다면 그 오브젝트를 '마지막 선택'으로 기억해둠
                if (EventSystem.current.currentSelectedGameObject != lastSelectedObject)
                {
                    lastSelectedObject = EventSystem.current.currentSelectedGameObject;
                }
            }
            else
            {
                // 2. 만약 선택된 오브젝트가 없다면 (빈 공간 클릭해서 null이 됨)
                // -> 기억해둔 오브젝트로 강제 복구
                if (lastSelectedObject != null && lastSelectedObject.activeInHierarchy)
                {
                    EventSystem.current.SetSelectedGameObject(lastSelectedObject);
                }
                else
                {
                    // 기억해둔 것도 없거나 꺼져있다면 -> 기본 공격 버튼 선택
                    EventSystem.current.SetSelectedGameObject(attackButton);
                }
            }
        }

        // 커맨드 메뉴에서의 키 입력 처리
        void HandleCommandInput()
        {
            // Esc 또는 LeftShift 키 감지
            if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.LeftShift))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);

                // 행동 큐가 비어있다는 것은, 내가 이번 턴의 '첫 번째 행동 결정자'라는 뜻.
                if (actionQueue.Count == 0)
                {
                    // Fight 메뉴가 켜져 있다면 -> Base 메뉴로 돌아감 (싸우기 취소)
                    if (fightCmdContainer != null && fightCmdContainer.activeSelf)
                    {
                        ShowBaseMenu(); 
                    }
                }
                // 큐에 뭔가 들어있다면 (내 앞사람이 행동을 결정했다면)
                else
                {
                    // 앞사람의 행동을 취소하러 돌아감
                    GoToPreviousPlayer();
                }
            }
        }

        // =========================================================
        // [UI 연결] BaseCmdContainer 버튼 함수들
        // =========================================================

        // 1. Fight 버튼 클릭 시 -> FightCmdContainer 활성화
        public void OnBaseCommand_Fight()
        {
            // [핵심] 이제부터는 전투 모드임
            isFightMode = true;

            // Base 끄기, Fight 켜기
            baseCmdContainer.SetActive(false);
            fightCmdContainer.SetActive(true);

            // Fight 메뉴의 첫 버튼(Attack)에 포커스
            StartCoroutine(SelectButton(attackButton));
        }

        // 2. Escape(Run) 버튼 클릭 시 (기존 Run 로직 연결)
        public void OnBaseCommand_Escape()
        {
            // 기존 OnCommandButton_Escape 로직 재활용
            OnCommandButton_Escape();
        }

        // (옵션) Talk, Auto는 아직 기능이 없다면 로그만 출력
        public void OnBaseCommand_Talk() { Debug.Log("대화하기 (미구현)"); }

        public void OnBaseCommand_Auto()
        {
            SoundManager.Instance.PlayBGM(BgmID.Normal_Battle);
            Debug.Log("오토 모드 시작! (x2 속도)");
            isAutoMode = true;
            
            // 게임 속도 2배
            Time.timeScale = 2.0f;

            // UI 즉시 숨기기
            if (baseCmdContainer) baseCmdContainer.SetActive(false);
            if (fightCmdContainer) fightCmdContainer.SetActive(false);
            commandPanel.SetActive(false);

            // 현재 캐릭터부터 오토 로직 수행
            NextPlayerInput();
        }


        // =========================================================
        // [UI 연결] FightCmdContainer 버튼 함수들
        // =========================================================
        // 공격 버튼 클릭
        public void OnFightCommand_Attack()
        {
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;
            
            // 무기가 null이어도 PrepareWeaponAction 호출 (맨손 공격)
            PrepareWeaponAction(actor.currentWeapon, CombatAction.ActionType.Attack);
        }
        public void OnFightCommand_Gun()
        {
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;

            // 1. 총과 총알이 모두 있는지 확인
            if (!actor.CanShootGun())
            {
                Debug.Log("총 또는 탄약이 장비되어 있지 않습니다!");
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel); // 실패음
                
                if (logPanel)
                {
                    logPanel.SetActive(true);
                    logText.text = "CANNOT USE GUN\n(Need Gun & Ammo)";
                }
                // 잠시 후 메시지 끄는 코루틴 등이 필요할 수 있음
                return;
            }

            // 2. 총 데이터로 공격 준비
            PrepareWeaponAction(actor.currentGun, CombatAction.ActionType.Gun);
        }

        public void OnFightCommand_Skill() { Debug.Log("스킬 (미구현)"); }
        public void OnFightCommand_Item()
        {
            if (battleItemUI != null) 
            {
                // 1. 뒷배경(커맨드 버튼들) 상호작용 차단
                // CanvasGroup의 interactable을 끄면 네비게이션이 이쪽으로 절대 넘어오지 않는다.
                SetContainerInteractable(fightCmdContainer, false);

                // 2. 아이템 UI 열기
                battleItemUI.Show();
            }
        }

        // 아이템 선택이 취소되거나 완료되어 창이 닫힐 때 호출할 함수
        public void OnItemMenuClosed()
        {
            // 1. 뒷배경 상호작용 다시 허용
            SetContainerInteractable(fightCmdContainer, true);

            // 2. 포커스 복구 (Item 버튼이나 Attack 버튼으로)
            StartCoroutine(SelectButton(attackButton)); 
        }

        // 헬퍼 함수: 컨테이너의 상호작용 켜기/끄기
        void SetContainerInteractable(GameObject container, bool isInteractable)
        {
            if (container == null) return;
            
            CanvasGroup group = container.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.interactable = isInteractable;
                group.blocksRaycasts = isInteractable; // 마우스 클릭도 차단
            }
        }

        // 2. 아이템이 선택되었을 때
        public void OnItemSelected(ConsumableItemData item)
        {
            currentSelectedItem = item;
            
            // 타겟 선택 로직 재활용을 위해 변수 설정
            // (PrepareWeaponAction과 유사한 로직)
            
            TargetScope scope = item.targetScope;
            
            // 범위에 따라 타겟 선택 모드 진입 or 즉시 실행
            if (scope == TargetScope.OneEnemy || scope == TargetScope.OneAlly || scope == TargetScope.DeadAlly)
            {
                // 타겟 선택 모드 시작 (validTargets 세팅 필요)
                StartItemTargetSelection(scope);
            }
            else
            {
                // 전체/랜덤/자신 -> 즉시 큐에 등록
                QueueItemAction(null); // 타겟 null (전체/자신)
            }
        }

        // 3. 아이템 타겟팅 준비 (Weapon 타겟팅 로직과 유사하게 구현)
        // 타겟팅 시작 시 리스트가 비어있으면 에러 방지
        void StartItemTargetSelection(TargetScope scope)
        {
            validTargets.Clear();

            // 1. 적군 선택
            if (scope == TargetScope.OneEnemy)
            {
                foreach(var m in activeMonsters) 
                {
                    if(m != null && m.currentHp > 0) validTargets.Add(m);
                }
            }
            // 2. 아군 선택 (OneAlly)
            else if (scope == TargetScope.OneAlly) 
            {
                foreach(var p in activePlayers)
                {
                    if(p != null && p.currentHp > 0) validTargets.Add(p);
                }
            }
            // 3. 죽은 아군 선택 (부활 아이템)
            else if (scope == TargetScope.DeadAlly)
            {
                foreach(var p in activePlayers)
                {
                    if(p != null && p.currentHp <= 0) validTargets.Add(p);
                }
            }
            
            // 2. 타겟이 한 명도 없으면 모드 진입 차단!
            if (validTargets.Count == 0)
            {
                Debug.LogWarning("사용할 수 있는 대상이 없습니다.");
                
                // 안내 메시지 표시
                if (logPanel)
                {
                    logPanel.SetActive(true);
                    logText.text = "No Target!";
                    StartCoroutine(HideLogAfterDelay(1.0f));
                }
                
                // 아이템 UI를 다시 켜거나, 취소 처리
                // 여기서는 아이템 선택 취소로 간주하고 UI 유지
                return; 
            }
            
            // 3. 타겟이 있을 때만 진입
            isSelectingTarget = true;
            currentSelectedAction = CombatAction.ActionType.Item;
            currentTargetIndex = 0; // 인덱스 초기화
            UpdateTargetHighlight();
            
            // 키 입력 중복 방지 쿨타임
            inputCooldown = 0.2f;

            // =========================================================
            // 뒷배경(커맨드 버튼) 상호작용 차단 & 포커스 제거
            // =========================================================
            if (fightCmdContainer != null && fightCmdContainer.activeSelf)
            {
                // 배경 버튼들이 눌리지 않도록 CanvasGroup 비활성화
                SetContainerInteractable(fightCmdContainer, false);
                
                // 현재 잡혀있는 버튼 포커스(예: 아이템 버튼) 강제 해제
                EventSystem.current.SetSelectedGameObject(null);
            }
            // =========================================================
        }

        // 로그 자동 숨김 코루틴
        IEnumerator HideLogAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if(logPanel) logPanel.SetActive(false);
        }

        // 4. 행동 큐 등록
        void QueueItemAction(GameObject target)
        {
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;
            
            CombatAction action = new CombatAction(actor.gameObject, target, CombatAction.ActionType.Item, actor.GetTotalAgi());
            action.skillData = null; // 스킬 아님
            action.itemData = currentSelectedItem; 

            actionQueue.Add(action);
            NextPlayerInput();
        }

        public void OnFightCommand_Guard()
        {
            // 1. 현재 캐릭터 가져오기
            PlayerController currentActor = activePlayers[currentPlayerIndex] as PlayerController;

            // 2. 행동 생성
            // 방어는 적의 공격보다 빨라야 하므로 속도에 큰 보정(+2000)을 준다.
            int guardSpeed = currentActor.GetTotalAgi() + 2000;

            // 타겟은 '자기 자신'으로 설정
            CombatAction action = new CombatAction(
                currentActor.gameObject, 
                currentActor.gameObject, // Self targeting
                CombatAction.ActionType.Guard, 
                guardSpeed
            );

            // 3. 큐에 추가
            actionQueue.Add(action);

            // 로그 출력 (선택 사항)
            Debug.Log($"{currentActor.name}: 방어 태세 예약");
            
            // 4. 다음 캐릭터로 넘기기
            // 방어는 타겟 선택 과정이 필요 없으므로 바로 넘긴다.
            NextPlayerInput();
        }

        
        // [내부 함수] 다시 Base 메뉴로 돌아오는 함수
        void ShowBaseMenu()
        {
            isFightMode = false; // [핵심] 전투 모드 해제

            fightCmdContainer.SetActive(false);
            baseCmdContainer.SetActive(true);
            
            StartCoroutine(SelectButton(baseFirstButton));
        }

        // [핵심] 이전 캐릭터로 되돌아가는 함수
        void GoToPreviousPlayer()
        {
            // 첫 번째 캐릭터라면 더 이상 뒤로 갈 수 없음
            if (currentPlayerIndex <= 0) 
            {
                Debug.Log("첫 번째 캐릭터입니다. 뒤로 갈 수 없습니다.");
                return;
            }

            Debug.Log("이전 캐릭터 명령 취소 및 복귀");

            // 1. 방금 전 캐릭터가 예약했던 행동을 큐에서 삭제
            if (actionQueue.Count > 0)
            {
                // 리스트의 가장 마지막 요소(직전 캐릭터의 행동) 삭제
                actionQueue.RemoveAt(actionQueue.Count - 1);
            }

            // 2. 인덱스 되감기
            // NextPlayerInput()을 호출하면 인덱스가 +1 되므로, 
            // 이전 캐릭터(-1)로 가려면 현재(-1)에서 한 번 더 빼야 함(-2)
            currentPlayerIndex -= 2;

            // 3. 입력 다시 시작 (이전 캐릭터의 턴으로 UI 갱신)
            NextPlayerInput();
        }
        
        void NextPlayerInput()
        {
            currentPlayerIndex++;

            // 종료 및 스킵 로직
            if (currentPlayerIndex >= activePlayers.Count) { ProcessTurn(); return; }

            PlayerController currentPlayer = activePlayers[currentPlayerIndex] as PlayerController;
            if (currentPlayer.currentHp <= 0) { NextPlayerInput(); return; }

            // =========================================================
            // 오토 모드 처리 로직
            // =========================================================
            if (isAutoMode)
            {
                // UI를 켜지 않고 내부적으로 행동 결정 후 재귀 호출
                ProcessAutoAction(currentPlayer);
                return; 
            }
            // =========================================================

            // UI 활성화
            isSelectingTarget = false;
            commandPanel.SetActive(true);
            
            if (logPanel)
            {
                logPanel.SetActive(true);
                logText.text = $"Command Wait: {currentPlayer.sourceData.name}";
            } 
            if (targetCursor) targetCursor.gameObject.SetActive(false);


            // =========================================================
            // isFightMode 변수에 따라 어떤 메뉴를 띄울지 결정
            // =========================================================

            if (isFightMode)
            {
                // 이미 "싸우다"를 골랐으므로, 곧바로 상세 행동(Attack/Skill...) 메뉴 표시
                if (baseCmdContainer) baseCmdContainer.SetActive(false);
                if (fightCmdContainer) fightCmdContainer.SetActive(true);
                
                StartCoroutine(SelectButton(attackButton));
            }
            else
            {
                // 아직 방침을 안 정했으므로(첫 캐릭터), Base 메뉴(Fight/Run...) 표시
                if (baseCmdContainer) baseCmdContainer.SetActive(true);
                if (fightCmdContainer) fightCmdContainer.SetActive(false);
                
                StartCoroutine(SelectButton(baseFirstButton));
            }
        }

        // 오토 행동 결정 및 처리
        void ProcessAutoAction(PlayerController actor)
        {
            // 1. 이전 행동 가져오기 (없으면 기본 Attack)
            CombatAction.ActionType actionType = CombatAction.ActionType.Attack;
            if (lastPlayerActions.ContainsKey(currentPlayerIndex))
            {
                actionType = lastPlayerActions[currentPlayerIndex];
            }

            // 2. 타겟 결정 (랜덤 적)
            // 살아있는 몬스터 중 랜덤 선택
            var livingMonsters = activeMonsters.Where(m => m.currentHp > 0).ToList();
            if (livingMonsters.Count == 0) 
            {
                // 적이 없으면 턴 종료 처리
                ProcessTurn(); 
                return;
            }
            BattleEntity target = livingMonsters[Random.Range(0, livingMonsters.Count)];

            // 3. 행동 생성 (현재는 Attack만 구현, 나중에 Guard/Skill 분기 추가 가능)
            // (Guard나 Skill은 타겟이 아군이거나 없을 수 있으므로 추후 보완 필요)
            int speed = actor.GetTotalAgi() + Random.Range(0, 5);
            CombatAction action = new CombatAction(actor.gameObject, target.gameObject, actionType, speed);

            // 4. 큐에 추가
            actionQueue.Add(action);

            // 5. 다음 캐릭터로 즉시 이동
            NextPlayerInput();
        }

        // 범용 버튼 선택 코루틴
        IEnumerator SelectButton(GameObject btnToSelect)
        {
            // UI가 켜지고 1프레임 뒤에 선택해야 안전함
            yield return null; 
            
            EventSystem.current.SetSelectedGameObject(null);
            
            if (btnToSelect != null)
            {
                EventSystem.current.SetSelectedGameObject(btnToSelect);
                lastSelectedObject = btnToSelect;
            }
        }

        // [UI 연결] Move 버튼 클릭 시 호출
        public void OnCommandButton_Move()
        {
            // 현재 캐릭터
            BattleEntity currentActor = activePlayers[currentPlayerIndex];

            // 상태 변경: 이동 타겟 선택 모드
            isSelectingMoveTarget = true;
            
            // UI 전환
            commandPanel.SetActive(false);
            if (logPanel)
            {
                logPanel.SetActive(true);
                logText.text = "이동할 위치를 선택하세요.";
            }

            // 커서 초기화: 현재 내 위치에서 시작
            currentMoveSlotIndex = GetPlayerSlotIndex(currentActor.transform.parent);
            
            // [중요] 혹시 GetPlayerSlotIndex가 실패해서 0이 아닌 엉뚱한 값을 뱉는지 확인
            Debug.Log($"초기 슬롯 인덱스: {currentMoveSlotIndex}");

            UpdateMoveCursor();
            RefreshMoveHighlights(currentMoveSlotIndex);
            
            inputCooldown = 0.2f;

            // UI 버튼에서 포커스를 떼어내야 키보드 입력(화살표)을 스크립트가 받을 수 있음
            EventSystem.current.SetSelectedGameObject(null);
        }

        void HandleMoveSelectionInput()
        {
           bool moved = false;

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) 
            {
                if (currentMoveSlotIndex % 3 > 0) { currentMoveSlotIndex--; moved = true; }
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) 
            {
                if (currentMoveSlotIndex % 3 < 2) { currentMoveSlotIndex++; moved = true; }
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) 
            {
                if (currentMoveSlotIndex >= 3) { currentMoveSlotIndex -= 3; moved = true; }
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) 
            {
                if (currentMoveSlotIndex < 3) { currentMoveSlotIndex += 3; moved = true; }
            }

            if (moved)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Click);
                Debug.Log($"이동함! 현재 인덱스: {currentMoveSlotIndex}"); // 로그 확인용
                UpdateMoveCursor();
                RefreshMoveHighlights(currentMoveSlotIndex); // 호출
            }

            // 3. 확정 (Space, Enter)
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Click);

                BattleEntity currentActor = activePlayers[currentPlayerIndex];
                int myCurrentIndex = GetPlayerSlotIndex(currentActor.transform.parent);

                // A. 제자리 선택 = 취소
                if (currentMoveSlotIndex == myCurrentIndex)
                {
                    CancelMoveSelection();
                    return;
                }

                // =========================================================
                // 즉시 실행하지 않고, 행동 큐(Queue)에 예약함
                // =========================================================
                
                // 1. 목표 슬롯 가져오기 (이것을 타겟으로 저장)
                Transform targetSlot = GetPlayerSlotByIndex(currentMoveSlotIndex);

                // 2. 행동 생성
                // 이동은 전략적으로 중요하므로 속도에 보너스(+2000)를 주어 턴의 가장 처음에 발동하게 함.
                int moveSpeed = currentActor.GetTotalAgi() + 2000; 

                CombatAction action = new CombatAction(
                    currentActor.gameObject, 
                    targetSlot.gameObject, 
                    CombatAction.ActionType.Move, 
                    moveSpeed
                );

                // 3. 큐에 추가
                actionQueue.Add(action);

                // 4. 하이라이트/커서 끄기
                isSelectingMoveTarget = false;
                if (targetCursor) targetCursor.gameObject.SetActive(false);
                foreach (var player in activePlayers) (player as PlayerController).ResetHighlightColor();

                Debug.Log($"{currentActor.name}: 위치 이동 예약 완료");

                // 5. 다음 캐릭터 입력으로 넘어감
                NextPlayerInput();
            }
        }

        IEnumerator ExecuteMoveAction(PlayerController actor, Transform targetSlot)
        {
            // 입력 차단 및 UI 숨김
            isSelectingMoveTarget = false;
            if (targetCursor) targetCursor.gameObject.SetActive(false);

            // 이동 시작 전 색상 초기화
            foreach (var player in activePlayers) (player as PlayerController).ResetHighlightColor();

            // 1. 대상 슬롯에 누가 있는지 확인
            PlayerController targetChar = targetSlot.GetComponentInChildren<PlayerController>();
            Transform myOriginalSlot = actor.transform.parent;

            logText.text = "위치 변경 중...";

            // 2. 스왑 로직
            if (targetChar != null)
            {
                // 상대방을 내 자리(원래 부모)로 보냄
                targetChar.transform.SetParent(myOriginalSlot);
                targetChar.transform.localPosition = Vector3.zero;
                
                // (옵션) 로그 출력
                Debug.Log($"{actor.name} <-> {targetChar.name} 자리 교체");
            }

            // 나는 목표 자리로 이동
            actor.transform.SetParent(targetSlot);
            actor.transform.localPosition = Vector3.zero;

            // 3. 연출 대기
            yield return new WaitForSeconds(0.8f);

            // 4. 턴 소비 처리
            // 이동했으므로 이번 캐릭터의 행동은 끝난 것으로 간주하고 다음으로 넘김
            if (logPanel) logPanel.SetActive(false);
            NextPlayerInput();
        }

        // 슬롯 Transform으로 0~5 인덱스 찾기
        int GetPlayerSlotIndex(Transform slot)
        {
            int index = playerFrontSlots.IndexOf(slot);
            if (index != -1) return index; // 전열 0~2

            index = playerBackSlots.IndexOf(slot);
            if (index != -1) return index + 3; // 후열 3~5

            return 0; // 예외
        }

        // 인덱스 0~5로 슬롯 Transform 찾기
        Transform GetPlayerSlotByIndex(int index)
        {
            // 인덱스 유효성 검사
            if (index < 0 || index >= 6) return null;

            if (index < 3) 
            {
                // 전열 리스트 범위 체크
                if (index < playerFrontSlots.Count) return playerFrontSlots[index];
            }
            else 
            {
                // 후열 리스트 범위 체크
                int backIndex = index - 3;
                if (backIndex < playerBackSlots.Count) return playerBackSlots[backIndex];
            }

            return null; // 예외 상황
        }

        // Move 취소 시 복귀
        void CancelMoveSelection()
        {
            isSelectingMoveTarget = false;
            if (targetCursor) targetCursor.gameObject.SetActive(false);
            foreach (var player in activePlayers) (player as PlayerController).ResetHighlightColor();

            // 커맨드 패널 표시 (여기서는 전체 패널을 켜고)
            commandPanel.SetActive(true);
            
            // 이동(Move) 버튼은 Fight 메뉴에 있으므로, Fight 메뉴 상태를 유지해야 함
            baseCmdContainer.SetActive(false);
            fightCmdContainer.SetActive(true);

            if (logPanel) logPanel.SetActive(true); 
            logText.SetText($"Command Wait: {activePlayers[currentPlayerIndex].entityName}");
            
            inputCooldown = 0.2f;
            
            // Move 버튼이나 Attack 버튼 등 Fight 메뉴의 버튼으로 포커스
            // (편의상 첫 버튼인 Attack 버튼으로 보냄, 원한다면 moveButton 변수를 따로 만들어 연결해도 됨)
            StartCoroutine(SelectButton(attackButton)); 
        }

        void CancelTargetSelection()
        {
            isSelectingTarget = false;
            if (targetCursor) targetCursor.gameObject.SetActive(false); 
            
            commandPanel.SetActive(true);
            
            // 공격(Attack) 취소 시에도 Fight 메뉴 유지
            baseCmdContainer.SetActive(false);
            fightCmdContainer.SetActive(true);

            if (logPanel) logPanel.SetActive(false);

            // =========================================================
            // 뒷배경 상호작용 다시 허용
            // =========================================================
            SetContainerInteractable(fightCmdContainer, true);
            // =========================================================

            inputCooldown = 0.2f; 
            StartCoroutine(SelectButton(attackButton));
        }

        // Move 커서 위치 업데이트
        void UpdateMoveCursor()
        {
            Transform slot = GetPlayerSlotByIndex(currentMoveSlotIndex);
            if (targetCursor)
            {
                targetCursor.gameObject.SetActive(true);
                // 슬롯 위치에 커서 표시 (약간 위로 띄움)
                targetCursor.position = slot.position + cursorOffset; 
            }
        }

        // 해당 슬롯의 캐릭터 하이라이트 업데이트
        void RefreshMoveHighlights(int cursorSlotIndex)
        {
            // 1. 모든 아군 캐릭터의 색상을 흰색(기본)으로 초기화
            foreach (PlayerController player in activePlayers)
            {
                player.ResetHighlightColor();
            }

            // 2. '이동하려는 주인공(Source)'을 초록색으로 칠하기
            if (currentPlayerIndex < activePlayers.Count)
            {
                PlayerController sourcePlayer = activePlayers[currentPlayerIndex] as PlayerController;
                sourcePlayer.SetHighlightColor(moveSourceColor);
            }

            // 3. '현재 커서가 가리키는 대상(Target)'을 노란색으로 칠하기 (덮어쓰기)
            // 커서가 -1이거나 유효하지 않으면 패스 (하이라이트 끄기 모드)
            if (cursorSlotIndex < 0) return;

            Transform targetSlot = GetPlayerSlotByIndex(cursorSlotIndex);
            if (targetSlot != null)
            {
                PlayerController targetChar = targetSlot.GetComponentInChildren<PlayerController>();
                if (targetChar != null)
                {
                    // 만약 내 자리를 가리키고 있다면 노란색이 초록색을 덮어씀 (선택 중임을 강조)
                    targetChar.SetHighlightColor(moveTargetColor);
                }
            }
        }

        // [UI 연결] Run 버튼 클릭 시 호출
        public void OnCommandButton_Escape()
        {
            // 실수로 눌렀을 때를 대비해 바로 실행하지 않고 코루틴으로 처리
            StartCoroutine(ProcessRunAttempt());
        }

        // 회피 애니메이션 (옆으로 쓱 움직였다 복귀)
        IEnumerator ProcessDodgeAnimation(Transform targetTransform)
        {
            Vector3 originalPos = targetTransform.localPosition;
            
            // 랜덤하게 왼쪽(-1) 또는 오른쪽(1) 방향 결정
            float direction = (Random.value > 0.5f) ? 1f : -1f;
            
            // 이동할 거리 (X축 기준)
            Vector3 dodgeOffset = new Vector3(10.5f * direction, 0, 0); 
            Vector3 targetPos = originalPos + dodgeOffset;

            float duration = 0.15f; // 편도 이동 시간 (빠르게)
            float elapsed = 0f;

            // 1. 밖으로 이동
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                targetTransform.localPosition = Vector3.Lerp(originalPos, targetPos, elapsed / duration);
                yield return null;
            }
            targetTransform.localPosition = targetPos;

            // 잠시 대기? (필요 없다면 생략 가능)
            // yield return new WaitForSeconds(0.05f);

            // 2. 원래 자리로 복귀
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                targetTransform.localPosition = Vector3.Lerp(targetPos, originalPos, elapsed / duration);
                yield return null;
            }
            targetTransform.localPosition = originalPos;
        }

        IEnumerator ProcessRunAttempt()
        {
            // 1. UI 숨기기 및 상태 변경
            state = BattleState.Processing; // 입력 차단
            commandPanel.SetActive(false);
            if (targetCursor) targetCursor.gameObject.SetActive(false);
            
            if (logPanel)
            {
                logPanel.SetActive(true);
                logText.text = "도망치는 중...";
            }

            // 2. 연출 대기 (긴장감)
            yield return new WaitForSeconds(1.0f);

            // 3. 도망 성공 여부 계산
            bool isSuccess = CalculateEscapeSuccess();

            if (isSuccess)
            {
                // 4-A. 성공 시퀀스
                logText.text = "무사히 도망쳤다!";
                Debug.Log("도망 성공!");
                
                yield return new WaitForSeconds(1.0f);

                // 전투 종료 및 던전 탐색 상태 복귀 (승리가 아니므로 false 전달하지 않고 바로 종료 처리)
                // EndBattleRoutine을 재활용하거나 직접 종료 로직 수행
                DungeonStateManager.Instance.ChangeState(GameState.Exploration);
            }
            else
            {
                // 4-B. 실패 시퀀스
                logText.text = "도망치지 못했다!\n적에게 틈을 보이고 말았다.";
                Debug.Log("도망 실패!");

                yield return new WaitForSeconds(1.0f);

                // 5. 실패 페널티: 아군 턴을 강제로 종료하고 적의 턴으로 넘김
                // 큐에 예약된 행동들을 다 지우고 바로 ProcessTurn 호출
                actionQueue.Clear(); 
                
                // 바로 적들의 턴(공격) 시작
                ProcessTurn();
            }
        }

        void HandleTargetSelectionInput()
        {
            // 취소/확인
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                // 선택된 몬스터의 하이라이트 끄기
                if (validTargets.Count > currentTargetIndex)
                {
                    validTargets[currentTargetIndex].SetSelectionState(false);
                    
                    // 선택 완료 함수 호출
                    OnTargetSelected(validTargets[currentTargetIndex]);
                }
                return;
            }

            BattleEntity currentEntity = validTargets[currentTargetIndex];

            // 현재 타겟 리스트가 '아군'인지 '적군'인지 판단하여 기준 컨테이너 결정
            Transform targetFrontContainer = enemyFrontRowContainer;
            
            // validTargets의 첫 번째 요소가 PlayerController라면 아군 타겟팅 중인 것임
            if (validTargets.Count > 0 && validTargets[0] is PlayerController)
            {
                targetFrontContainer = playerFrontRowContainer;
            }
            
            // 현재 타겟이 전열인가? (기준 컨테이너와 비교)
            bool isCurrentFront = (currentEntity.transform.parent.parent == targetFrontContainer);
            
            int currentCol = currentEntity.columnIndex;
            BattleEntity nextEntity = null;
            bool moved = false;

            // 1. 좌우 이동 (매개변수에 컨테이너 전달)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                nextEntity = FindEntityInRow(targetFrontContainer, isCurrentFront, currentCol, -1);
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                nextEntity = FindEntityInRow(targetFrontContainer, isCurrentFront, currentCol, 1);
                moved = true;
            }
            // 2. 상하 이동
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                // 후열 -> 전열 이동
                if (!isCurrentFront) 
                {
                    moved = true;
                    nextEntity = FindClosestEntityInRow(targetFrontContainer, true, currentCol);
                }
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                // 전열 -> 후열 이동
                if (isCurrentFront) 
                {
                    moved = true;
                    nextEntity = FindClosestEntityInRow(targetFrontContainer, false, currentCol);
                }
            }

            if (moved) SoundManager.Instance.PlaySFX(SfxID.UI_Click);

            if (nextEntity != null)
            {
                currentTargetIndex = validTargets.IndexOf(nextEntity);
                UpdateTargetHighlight();
            }
        }

        // direction: -1(왼쪽), 1(오른쪽)
        BattleEntity FindEntityInRow(Transform frontContainer, bool isTargetFront, int startCol, int direction)
        {
            // 컨테이너 비교 로직 수정
            var rowEntities = validTargets
                .Where(m => (m.transform.parent.parent == frontContainer) == isTargetFront)
                .OrderBy(m => m.columnIndex)
                .ToList();

            if (rowEntities.Count == 0) return null;

            BattleEntity current = validTargets[currentTargetIndex];
            int currentIndexInRow = rowEntities.IndexOf(current);
            
            if (currentIndexInRow == -1) return null;

            int nextIndex = currentIndexInRow + direction;

            if (nextIndex >= 0 && nextIndex < rowEntities.Count)
            {
                return rowEntities[nextIndex];
            }
            return null; 
        }

        BattleEntity FindClosestEntityInRow(Transform frontContainer, bool isTargetFront, int targetCol)
        {
            var targetRowEntities = validTargets
                .Where(m => (m.transform.parent.parent == frontContainer) == isTargetFront)
                .ToList();

            if (targetRowEntities.Count == 0) return null;

            BattleEntity closest = targetRowEntities
                .OrderBy(m => Mathf.Abs(m.columnIndex - targetCol))
                .First();

            return closest;
        }

        // 커서 위치 이동 대신 색상 변경 함수 호출
        void UpdateTargetHighlight()
        {
            // 1. 모든 타겟의 하이라이트 끄기 (초기화)
            foreach (var monster in validTargets)
            {
                monster.SetSelectionState(false);
            }

            // 2. 현재 선택된 타겟만 하이라이트 켜기
            if (validTargets.Count > 0)
            {
                BattleEntity currentTarget = validTargets[currentTargetIndex];
                currentTarget.SetSelectionState(true);
            }
        }

        void UpdateCursorPosition()
        {
            if (validTargets.Count == 0) return;

            BattleEntity target = validTargets[currentTargetIndex];

            // 몬스터의 스크린 좌표(UI 좌표)를 가져와서 커서를 이동
            // 몬스터가 UI 요소(Image)라면 transform.position을 그대로 쓰면 됨
            if (targetCursor) targetCursor.position = target.preferredImage.transform.position + cursorOffset;
        }

        // [이벤트] 타겟 클릭 시 (타겟 지정 완료)
        public void OnTargetSelected(BattleEntity targetEntity)
        {
            if (!isSelectingTarget) return;

            // 현재 순서의 플레이어 가져오기
            PlayerController actor = activePlayers[currentPlayerIndex] as PlayerController;

            // (옵션) 이번 행동을 '마지막 행동'으로 기록 (오토 모드용)
            if (lastPlayerActions.ContainsKey(currentPlayerIndex))
                lastPlayerActions[currentPlayerIndex] = currentSelectedAction;
            else
                lastPlayerActions.Add(currentPlayerIndex, currentSelectedAction);
            
            // 1. 행동 속도 결정 (기본 AGI + 난수)
            int speed = actor.GetTotalAgi() + Random.Range(0, 5);

            // 2. CombatAction 생성
            CombatAction action = new CombatAction(actor.gameObject, targetEntity.gameObject, currentSelectedAction, speed); //
            
            // =========================================================
            // OnTargetSelected 아이템 사용이라면, 아이템 데이터를 액션에 연결해야 함!
            // =========================================================
            if (currentSelectedAction == CombatAction.ActionType.Item)
            {
                action.itemData = currentSelectedItem; 

                // (옵션) 아이템 사용은 공격보다 우선순위를 높이고 싶다면 속도 보정
                action.speed += 500;
            }
            // =========================================================

            Debug.Log($"{actor.entityName}의 {currentSelectedAction} 예약 완료! (Target: {targetEntity.entityName})");

            // 3. 큐에 저장 
            actionQueue.Add(action);
            // 4. UI 정리 및 다음 입력으로 이동
            isSelectingTarget = false;
            if (targetCursor) targetCursor.gameObject.SetActive(false); // 커서 끄기
            
            // 타겟 하이라이트 끄기
            targetEntity.SetSelectionState(false);

            // =========================================================
            // 다음 턴을 위해 상호작용 복구
            // =========================================================
            SetContainerInteractable(fightCmdContainer, true);
            // =========================================================

            // 다음 캐릭터로 턴 넘기기
            NextPlayerInput();
        }

        // =================================================================
        // 3. 턴 처리 및 실행 (Process & Execution)
        // =================================================================
        void ProcessTurn()
        {
            SoundManager.Instance.PlayBGM(BgmID.Normal_Battle);
            
            state = BattleState.Processing;
            commandPanel.SetActive(false);
            if (logPanel) logPanel.SetActive(false);

            // 1. 적들의 행동 결정 (AI)
            // 살아있는 아군 리스트 추출
            List<BattleEntity> livingPlayers = activePlayers.Where(p => p.currentHp > 0).ToList();

            if (livingPlayers.Count == 0)
            {
                state = BattleState.Lost;
                Debug.Log("전멸했습니다...");
                return;
            }

            foreach (MonsterController monster in activeMonsters)
            {
                if (monster.currentHp <= 0) continue;

                // livingPlayers 리스트를 넘겨줌
                CombatAction enemyAction = monster.ChooseAction(livingPlayers);
                if (enemyAction != null)
                {
                    actionQueue.Add(enemyAction);
                }
            }

            // 2. 속도 순 정렬
            actionQueue = actionQueue.OrderByDescending(x => x.speed).ToList();

            // 3. 실행 시작
            StartCoroutine(ExecuteActions());
        }

        IEnumerator ExecuteActions()
        {
            foreach (var action in actionQueue)
            {
                // 매 행동마다 전투 종료 체크
                if (CheckBattleEnd(out bool isWin)) 
                {
                    // 종료 조건 만족 시, 바로 종료 시퀀스 실행 후 루프 탈출
                    StartCoroutine(EndBattleRoutine(isWin));
                    yield break; 
                }

                // =========================================================
                // 행동 주체(Actor)의 생존 여부 확인
                // =========================================================
                bool isActorDead = false;

                // 1. 오브젝트가 아예 없거나 꺼져있는지 체크
                if (action.actor == null || !action.actor.activeSelf) 
                {
                    isActorDead = true;
                }
                else
                {
                    // 2. HP 체크 (몬스터 & 플레이어)
                    if (action.actor.TryGetComponent(out BattleEntity be) && !be.IsAlive) isActorDead = true;
                }

                // 3. 죽었다면 이번 행동 스킵
                if (isActorDead)
                {
                    // Debug.Log($"[Skip] {action.actor?.name}은(는) 사망하여 행동 취소");
                    continue; 
                }
                // =========================================================

                yield return StartCoroutine(PerformAction(action));
            }

            // 턴의 모든 행동이 끝난 후 다시 체크
            if (CheckBattleEnd(out bool win))
            {
                StartCoroutine(EndBattleRoutine(win));
            }
            else
            {
                // PreparePlayerTurn()을 호출하면 -> PreparePlayerTurnRoutine()이 시작됨
                // -> 적 이동 체크 -> UI 표시 순서로 진행됨
                PreparePlayerTurn(); 
            }
        }

        // 반환값: 전투가 끝났으면 true
        // out 변수 isWin: 승리했으면 true, 패배했으면 false
        bool CheckBattleEnd(out bool isWin)
        {
            isWin = false;

            // 1. 적 전멸 체크
            bool allEnemiesDead = activeMonsters.TrueForAll(m => m.currentHp <= 0);
            if (allEnemiesDead)
            {
                isWin = true;
                return true; // 전투 종료 (승리)
            }

            // 2. 아군 전멸 체크
            bool allPlayersDead = activePlayers.TrueForAll(p => p.currentHp <= 0);
            if (allPlayersDead)
            {
                isWin = false;
                return true; // 전투 종료 (패배)
            }

            // 전투 계속
            return false;
        }

        IEnumerator PerformAction(CombatAction action)
        {
            // 액션 타입에 따라 적절한 핸들러 코루틴 실행
            switch (action.type)
            {
                case CombatAction.ActionType.Item:
                    yield return HandleItemAction(action);
                    break;

                case CombatAction.ActionType.Guard:
                    yield return HandleGuardAction(action);
                    break;

                case CombatAction.ActionType.Move:
                    yield return StartCoroutine(PerformMove(action));
                    break;

                case CombatAction.ActionType.Attack:
                case CombatAction.ActionType.Gun:
                    yield return HandleAttackAction(action);
                    break;
            }

            // 공통 후처리
            yield return wait05;
            if (logPanel) logPanel.SetActive(false);
            logText.SetText(string.Empty);
        }

        // ========================================================================
        // 1. 아이템 처리 핸들러
        // ========================================================================
        IEnumerator HandleItemAction(CombatAction action)
        {
            ConsumableItemData item = action.itemData;
            GameObject target = action.target;

            Debug.Log($"{action.actor.name}의 아이템 사용: {item.dataName}");
            // Manager.InventoryManager.Instance.UseItem(item.id); // 아이템 차감

            // 효과 적용
            ApplyItemEffect(target, item);

            yield return wait05;
        }

        void ApplyItemEffect(GameObject target, ConsumableItemData item)
        {
            // 공통 컴포넌트 접근 헬퍼 사용 (하단 구현 참조)
            var pTarget = target.GetComponent<PlayerController>();
            var mTarget = target.GetComponent<MonsterController>();

            switch (item.effectType)
            {
                case ItemEffectType.RecoverHP:
                    if (pTarget) pTarget.Recover(item.effectValue, 0);
                    // 몬스터 회복 로직 추가 가능
                    break;
                case ItemEffectType.RecoverMP:
                    if (pTarget) pTarget.Recover(0, item.effectValue);
                    break;
                case ItemEffectType.Revive:
                    if (pTarget && pTarget.currentHp <= 0) pTarget.Revive(item.effectValue);
                    break;
                case ItemEffectType.Attack_Physical:
                case ItemEffectType.Attack_Magic:
                    int dmg = item.effectValue;
                    ApplyDamage(target, dmg, false); // 공통 데미지 함수 호출
                    break;
                case ItemEffectType.Buff_ReflectPhys:
                    if (pTarget) pTarget.isPhysicalReflect = true;
                    Debug.Log($"{target.name}: 물리 반사 배리어!");
                    break;
                case ItemEffectType.Buff_ReflectMagic:
                    if (pTarget) pTarget.isMagicReflect = true;
                    Debug.Log($"{target.name}: 마법 반사 배리어!");
                    break;
            }
        }

        // ========================================================================
        // 2. 방어 처리 핸들러
        // ========================================================================
        IEnumerator HandleGuardAction(CombatAction action)
        {
            // 플레이어/몬스터 여부 상관없이 처리
            SetGuardState(action.actor, true);

            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            ShowLog($"{action.actor.name}의 방어 태세!");

            yield return wait05;
            if (logPanel) logPanel.SetActive(false);
        }

        void SetGuardState(GameObject actor, bool state)
        {
            if (actor.TryGetComponent(out PlayerController pc)) pc.isGuarding = state;
            else if (actor.TryGetComponent(out MonsterController mc)) mc.isGuarding = state;
        }

        // ========================================================================
        // 3. 공격 처리 핸들러 (가장 복잡한 부분 최적화)
        // ========================================================================
        IEnumerator HandleAttackAction(CombatAction action)
        {
            // 1. 무기 및 공격 정보 설정
            GetWeaponInfo(action, out int minHits, out int maxHits, out TargetScope scope);
            
            int hitCount = Random.Range(minHits, maxHits + 1);
            
            string actStr = string.Empty;
            if (action.type == CombatAction.ActionType.Gun)
            {
                actStr = "의 총이 불을 뿜었다!";
            }
            else actStr = "은(는) 검을 휘둘렀다!";
            ShowLog($"{action.actor.name}{actStr} 횟수: {hitCount}");
            
            // 2. 연타 루프
            for (int i = 0; i < hitCount; i++)
            {
                // 타겟 갱신 (죽은 적 제외 등)
                List<GameObject> currentTargets = GetTargetsByScope(scope, action);
                if (currentTargets.Count == 0) break; // 대상 없음

                // 공격 애니메이션 (첫 타격만)
                if (i == 0)
                {
                    action.actor.transform.localPosition += Vector3.forward * 0.3f;
                    yield return wait01;
                }

                // 실제 타격 처리
                foreach (var target in currentTargets)
                {
                    yield return StartCoroutine(ProcessSingleHit(action, target));
                }

                yield return new WaitForSeconds(0.15f); // 타격 간 딜레이

                // 전체 공격은 1회만 수행
                if (scope == TargetScope.FrontAll || scope == TargetScope.AnyAll) break;
            }

            // 복귀
            action.actor.transform.localPosition -= Vector3.forward * 0.3f;
            yield return new WaitForSeconds(0.3f);
        }

        // 단일 타격 처리 (반사, 회피, 데미지 통합)
        IEnumerator ProcessSingleHit(CombatAction action, GameObject target)
        {
            // 사운드
            var sfxId = (action.type == CombatAction.ActionType.Gun) ? SfxID.Attack_Gun : SfxID.Attack_Sword;
            SoundManager.Instance.PlaySFX(sfxId);

            // 위치 보정값 계산
            GetPositionalModifiers(action.actor, target, action, out float posDmgMult, out float posEvaBonus);

            // 1. 회피 체크
            if (CheckEvasion(action.actor, target, posEvaBonus))
            {
                Debug.Log($"{target.name} 회피!");
                yield return StartCoroutine(ProcessDodgeAnimation(target.transform));
                yield break; // 종료
            }

            // 2. 반사 체크
            if (CheckReflection(target, action.type))
            {
                Debug.Log("공격 반사!");
                ShowLog("Reflect!");
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);

                // 공격자에게 데미지 반사 (계산 로직 재사용)
                int reflectDmg = CalculateDamage(action.actor, action.actor, action, false, 1.0f);
                ApplyDamage(action.actor, reflectDmg, false); // 본인에게 데미지
                yield break;
            }

            // 3. 정상 피격 (데미지 계산)
            bool isCritical = CheckCritical(action.actor, target, action);
            int damage = 0;

            if (action.type == CombatAction.ActionType.Gun && action.actor.GetComponent<PlayerController>())
                damage = CalculateGunDamage(action.actor.GetComponent<PlayerController>(), target, isCritical);
            else
                damage = CalculateDamage(action.actor, target, action, isCritical, posDmgMult);

            ApplyDamage(target, damage, isCritical);
        }


        // ========================================================================
        // 4. 공통 헬퍼 함수 (핵심: 중복 코드 제거)
        // ========================================================================

        // Player와 Monster의 데미지 처리를 하나로 통합
        void ApplyDamage(GameObject target, int damage, bool isCritical)
        {
            // 부모 클래스로 가져옵니다. (Player든 Monster든 상관없음)
            var entity = target.GetComponent<BattleEntity>();

            if (entity != null)
            {
                entity.TriggerHitShake(isCritical); // 부모 클래스에 정의된 공통 메서드
                StartCoroutine(entity.OnDamageTaken(damage)); // 자식에서 구현한 오버라이드 메서드 실행
            }
        }

        // 반사 여부 체크 로직
        bool CheckReflection(GameObject target, CombatAction.ActionType type)
        {
            var entity = target.GetComponent<BattleEntity>();
            if (entity == null) return false;

            bool isPhysical = (type == CombatAction.ActionType.Attack || type == CombatAction.ActionType.Gun);
            // 스킬 데이터가 물리 속성이면 물리 반사, 그 외면 마법 반사 등의 디테일 추가 가능
            bool isMagic = (type == CombatAction.ActionType.Skill); // 임시

            if (isPhysical && entity.isPhysicalReflect) return true;
            if (isMagic && entity.isMagicReflect) return true;

            return false;
        }

        // 타겟팅 로직
        List<GameObject> GetTargetsByScope(TargetScope scope, CombatAction action)
        {
            List<GameObject> targets = new List<GameObject>();
            
            // 살아있는 목록 갱신 (프로퍼티나 메서드로 관리 추천)
            var livingMonsters = activeMonsters.Where(m => m.currentHp > 0).ToList();
            
            // 1. 단일 타겟
            if (scope == TargetScope.FrontSingle || scope == TargetScope.AnySingle)
            {
                if (action.target != null && IsAlive(action.target))
                    targets.Add(action.target);
                else
                {
                    // 타겟 사망 시 자동 변경
                    var newTarget = FindNearestLivingTarget(action.actor);
                    if (newTarget) 
                    {
                        action.target = newTarget;
                        targets.Add(newTarget);
                    }
                }
            }
            // (2) 랜덤/전체 타겟 로직
            else if (scope == TargetScope.FrontRandom || scope == TargetScope.AnyRandom)
            {
                List<GameObject> candidates = new List<GameObject>();
                foreach(var m in livingMonsters) 
                {
                    bool isFront = (m.transform.parent.parent == enemyFrontRowContainer);
                    if (scope == TargetScope.FrontRandom && !isFront) continue;
                    candidates.Add(m.gameObject);
                }
                if (scope == TargetScope.FrontRandom && candidates.Count == 0) candidates.AddRange(livingMonsters.Select(m => m.gameObject)); // 보정

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
                if (scope == TargetScope.FrontAll && targets.Count == 0) targets.AddRange(livingMonsters.Select(m => m.gameObject)); // 보정
            }
            
            return targets;
        }

        void ShowLog(string msg)
        {
            if (logPanel)
            {
                logPanel.SetActive(true);
                logText.SetText(msg);
            }
        }

        // 무기 정보 가져오기
        void GetWeaponInfo(CombatAction action, out int min, out int max, out TargetScope scope)
        {
            min = 1; max = 1; scope = TargetScope.FrontSingle; // 기본값

            var pActor = action.actor.GetComponent<PlayerController>();
            WeaponData weapon = null;

            if (pActor != null)
                weapon = (action.type == CombatAction.ActionType.Gun) ? pActor.currentGun : pActor.currentWeapon;

            if (weapon != null)
            {
                min = weapon.minHits;
                max = weapon.maxHits;
                scope = weapon.attackRange;
            }
        }

        // 총기 데미지 계산 함수 (기존 CalculateDamage 변형)
        int CalculateGunDamage(PlayerController attacker, GameObject defender, bool isCritical)
        {
            // 1. 공격력: 총 + 총알 + 스탯
            int baseAtk = attacker.GetGunAttack();
            
            // 2. 방어력
            int def = 0;
            if (defender.TryGetComponent(out MonsterController mc)) def = mc.GetTotalVit();
            // (플레이어 방어 로직 생략)

            // 3. 계산
            float rawDmg = Mathf.Max(1, baseAtk - (def * 0.5f));
            if (isCritical) rawDmg *= 1.5f; // 총은 크리티컬 배율이 다를 수 있음 (예: 1.5배)

            return Mathf.RoundToInt(rawDmg);
        }

        // 헬퍼: 살아있는지 확인
        bool IsAlive(GameObject obj)
        {
            return obj != null && obj.activeSelf && (obj.GetComponent<BattleEntity>()?.IsAlive ?? false);
        }

        // 턴 실행 중에 호출될 이동 로직
        IEnumerator PerformMove(CombatAction action)
        {
            PlayerController actor = action.actor.GetComponent<PlayerController>();
            
            // action.target은 우리가 위에서 넣은 '슬롯(Slot)' 오브젝트입니다.
            Transform targetSlot = action.target.transform; 
            Transform originSlot = actor.transform.parent; // 현재 나의 부모(원래 위치)

            // 로그 출력
            if (messagePanel) 
            {
                messagePanel.SetActive(true);
                messageText.SetText("자리 좀 바꿀게!");
            }
            Debug.Log($"[Action] {actor.name}의 이동 실행 -> {targetSlot.name}");

            // 이미 그 자리에 있다면 스킵
            if (targetSlot == originSlot) 
            {
                if (messagePanel) messagePanel.SetActive(false);
                yield break;
            }

            // 1. 대상 슬롯에 누가 있는지 확인 (스왑 대상)
            PlayerController targetChar = targetSlot.GetComponentInChildren<PlayerController>();

            // 2. 스왑 로직
            if (targetChar != null)
            {
                // 상대방을 내 자리(원래 부모)로 보냄
                targetChar.transform.SetParent(originSlot);
                targetChar.transform.localPosition = Vector3.zero;
            }

            // 3. 나는 목표 자리로 이동
            actor.transform.SetParent(targetSlot);
            actor.transform.localPosition = Vector3.zero;

            // 4. 간단한 연출 대기
            SoundManager.Instance.PlaySFX(SfxID.UI_Click); // 발소리 등으로 교체 추천
            yield return wait05;

            if (messagePanel) messagePanel.SetActive(false);
        }


        void InitializeSlots()
        {
            // 1. 몬스터 슬롯 (기존 코드)
            if (frontSlots.Count == 0) CreateSlotsFor(enemyFrontRowContainer, frontSlots);
            if (backSlots.Count == 0) CreateSlotsFor(enemyBackRowContainer, backSlots);
            ClearSlotContents(frontSlots);
            ClearSlotContents(backSlots);

            // 2. 아군 슬롯 생성
            if (playerFrontSlots.Count == 0) CreateSlotsFor(playerFrontRowContainer, playerFrontSlots);
            if (playerBackSlots.Count == 0) CreateSlotsFor(playerBackRowContainer, playerBackSlots);
            
            // 아군 슬롯 내용물 비우기
            ClearSlotContents(playerFrontSlots);
            ClearSlotContents(playerBackSlots);
        }

        // 슬롯 3개 생성 함수
        void CreateSlotsFor(Transform container, List<Transform> slotList)
        {
            // 기존 자식 다 삭제 (초기화)
            foreach (Transform child in container) Destroy(child.gameObject);
            slotList.Clear();

            for (int i = 0; i < 3; i++)
            {
                // 빈 오브젝트 생성
                GameObject slot = new GameObject($"Slot_{i}");
                slot.transform.SetParent(container, false);
                
                // Layout Group에서 영역을 차지하도록 설정
                // (Stretch 모드라면 부모가 알아서 넓혀주겠지만, 안전하게 설정)
                RectTransform rect = slot.AddComponent<RectTransform>();
                
                // (옵션) 투명한 이미지 컴포넌트를 넣어야 클릭 판정이 좋다면 추가
                // Image img = slot.AddComponent<Image>();
                // img.color = Color.clear;

                slotList.Add(slot.transform);
            }
        }

        void ClearSlotContents(List<Transform> slotList)
        {
            foreach (var slot in slotList)
            {
                foreach (Transform child in slot)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        void SpawnMonster(string id)
        {
            SoundManager.Instance.PlaySFX(SfxID.Encounter);
            var entry = monsterDB.GetEntry(id);
            if (entry == null) return;

            // 1. 배치할 Row의 슬롯 리스트 결정
            List<Transform> targetSlots = (entry.preferredRow == RowType.Front) ? frontSlots : backSlots;
            
            // 만약 해당 줄이 꽉 찼으면 다른 줄로 변경 (지난번 로직 응용)
            if (IsRowFull(targetSlots))
            {
                targetSlots = (targetSlots == frontSlots) ? backSlots : frontSlots;
                // 둘 다 꽉 찼으면 포기
                if (IsRowFull(targetSlots)) return;
            }

            // 2. [랜덤 위치] 빈 슬롯 중에서 랜덤하게 하나 뽑기
            List<int> emptyIndices = new List<int>();
            for (int i = 0; i < targetSlots.Count; i++)
            {
                if (targetSlots[i].childCount == 0) emptyIndices.Add(i);
            }

            // 랜덤 인덱스 선택
            int randomIndex = emptyIndices[Random.Range(0, emptyIndices.Count)];
            Transform selectedSlot = targetSlots[randomIndex];

            // 3. 생성 및 배치
            GameObject prefabToUse = (entry.prefab != null) ? entry.prefab : defaultMonsterPrefab;
            if (prefabToUse == null) return;

            GameObject newMonsterObj = Instantiate(prefabToUse, selectedSlot);
            
            // 위치 초기화 (슬롯의 정중앙에 위치하도록)
            newMonsterObj.transform.localPosition = Vector3.zero;

            // 데이터 주입
            // 1. 컴포넌트 가져오기 (자식까지 검색)
            MonsterController controller = newMonsterObj.GetComponentInChildren<MonsterController>();

            // 2. 스크립트 존재 여부 확인
            if (controller == null)
            {
                Debug.LogError($"[스폰 실패] '{entry.name}'(ID:{id}) 프리팹에 MonsterController 스크립트가 없습니다.");
                Destroy(newMonsterObj); // 껍데기 삭제
                return;
            }

            // 3. 데이터 초기화 실행
            controller.Initialize(entry);

            // 4. [요청하신 기능] 데이터 무결성 체크 (HP가 0이면 불량품 취급)
            if (controller.currentHp <= 0)
            {
                Debug.LogWarning($"[스폰 제외] '{entry.name}'(ID:{id})의 HP가 0입니다. 데이터(Stats)를 확인해주세요. 오브젝트를 제거합니다.");
                
                // 불량품 폐기 (화면에서 지움)
                Destroy(newMonsterObj); 
                
                // 리스트에 추가하지 않고 함수 종료
                return; 
            }

            // 5. 정상일 경우에만 등록
            controller.SetPositionInfo(randomIndex);

            // 배치된 슬롯이 전열인지 후열인지 확인하여 색상 적용
            bool isFront = (targetSlots == frontSlots);
            controller.SetRowAppearance(isFront); // 전열이면 원래 색, 후열이면 어둡게

            activeMonsters.Add(controller);

            Debug.Log($"[{entry.name}] 소환 성공 (HP: {controller.currentHp}, Slot: {randomIndex})");
        }

        // 해당 줄의 슬롯이 모두 몬스터로 차있는지 확인
        bool IsRowFull(List<Transform> slots)
        {
            foreach (var slot in slots)
            {
                if (slot.childCount == 0) return false; // 빈방 있음
            }
            return true; // 꽉 참
        }

        // 몬스터가 전열로 이동할 수 있는지 체크하고 이동시키는 함수
        IEnumerator CheckAndMoveForward(MonsterController monster)
        {
            // 이미 전열 슬롯의 자식이면 패스
            // (부모의 부모가 FrontRowContainer인지 확인하거나, 리스트 포함 여부 확인)
            if (frontSlots.Contains(monster.transform.parent)) yield break;

            // 내 앞자리 슬롯(Same Column Index) 가져오기
            Transform myFrontSlot = frontSlots[monster.columnIndex];

            // 앞 슬롯이 비어있는지 확인
            bool isSlotEmpty = (myFrontSlot.childCount == 0);

            // 만약 앞 슬롯에 뭔가 있는데 죽은 놈(시체)이라면?
            if (!isSlotEmpty)
            {
                var frontMonster = myFrontSlot.GetChild(0).GetComponent<MonsterController>();
                
                // 죽은 놈(HP <= 0)이면 치우고 들어감
                if (frontMonster != null && frontMonster.currentHp <= 0)
                {
                    activeMonsters.Remove(frontMonster);
                    Destroy(frontMonster.gameObject);
                    isSlotEmpty = true; // 이제 비었다!
                }
            }

            // 이동
            if (isSlotEmpty)
            {
                Debug.Log($"[전진] {monster.sourceData.name} -> 전열 {monster.columnIndex}번 슬롯으로 이동");

                // 1. 부모 변경 (슬롯 안으로 입양)
                // 부모가 바뀌는 순간, 유니티가 현재 보이는 크기를 유지하려고 scale 값을 이상하게 바꿀 수 있다.
                monster.transform.SetParent(myFrontSlot);

                // 2. 애니메이션 시작/목표값 설정
                Vector3 startPos = monster.transform.localPosition; // 현재 위치(부모 변경 직후)에서 시작
                Vector3 endPos = Vector3.zero;                      // 목표는 슬롯 정중앙
                
                Vector3 startScale = Vector3.one * 0.74f; //monster.transform.localScale;  // 현재 크기(약 0.74)에서 시작
                Vector3 endScale = Vector3.one;                     // 목표는 원래 크기(1.0)

                // 색상 시작/목표값 설정
                // 후열 색상(어두움) -> 전열 색상(밝음)
                Color startColor = new Color(0.6f, 0.6f, 0.6f, 1f); 
                Color endColor = Color.white;

                float duration = 0.5f; // 애니메이션 진행 시간 (초)
                float elapsed = 0f;

                // 3. 시간 경과에 따른 부드러운 변화 (Lerp 루프)
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration; // 진행률 (0.0 ~ 1.0)

                    // [옵션] SmoothStep을 쓰면 움직임이 더 부드러워짐 (시작과 끝이 감속됨)
                    // t = Mathf.SmoothStep(0f, 1f, t);

                    // 위치와 크기를 서서히 변화시킴
                    monster.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                    monster.transform.localScale = Vector3.Lerp(startScale, endScale, t);

                    // 색상이 부드럽게 밝아지도록 Lerp 적용
                    monster.SetColor(Color.Lerp(startColor, endColor, t));

                    yield return null; // 다음 프레임까지 대기
                }

                // 4. 최종값 보정 (루프가 끝난 후 미세한 오차 제거)
                monster.transform.localPosition = endPos;
                monster.transform.localScale = endScale;
                monster.SetColor(endColor);
            }
        }

        private void ClearCombatField()
        {
            // 리스트 비우기
            activeMonsters.Clear();

            // 전열/후열에 있는 모든 자식 오브젝트 삭제
            foreach (Transform child in enemyFrontRowContainer) Destroy(child.gameObject);
            foreach (Transform child in enemyBackRowContainer) Destroy(child.gameObject);
        }

        IEnumerator ProcessEnemyRowShift()
        {
            // 1. 후열에 있는 몬스터만 추려낸다. (리스트를 복사해서 사용)
            var backRowMonsters = activeMonsters
                .Where(m => backSlots.Contains(m.transform.parent)) // 부모가 뒤쪽 슬롯인 애들만
                .OrderBy(m => m.columnIndex) // 왼쪽부터 차례대로 처리
                .ToList();


            // 2. 각 몬스터에 대해 이동 가능 여부 체크
            foreach (MonsterController monster in backRowMonsters)
            {
                // CheckAndMoveForward는 이동이 발생하면 0.5초 대기(yield)를 포함하고 있음
                // 이동이 발생했을 때만 시간을 소모.
                yield return StartCoroutine(CheckAndMoveForward(monster));
            }
        }

        // 공격자가 때릴 수 있는 '가장 가까운 살아있는 적' 찾기
        GameObject FindNearestLivingTarget(GameObject attacker)
        {
            GameObject bestTarget = null;
            float closestDistance = float.MaxValue;
            Vector3 attackerPos = attacker.transform.position;

            // 1. 공격자가 플레이어인 경우 -> 살아있는 몬스터 중에서 검색
            if (attacker.GetComponent<PlayerController>() != null)
            {
                foreach (var monster in activeMonsters)
                {
                    if (monster != null && monster.currentHp > 0 && monster.gameObject.activeSelf)
                    {
                        float dist = Vector3.Distance(attackerPos, monster.transform.position);
                        
                        // 더 가까운 적을 발견하면 갱신
                        // (거리가 같으면 리스트 앞쪽 우선)
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            bestTarget = monster.gameObject;
                        }
                    }
                }
            }
            // 2. 공격자가 몬스터인 경우 -> 살아있는 플레이어 중에서 검색
            else if (attacker.GetComponent<MonsterController>() != null)
            {
                foreach (var player in activePlayers)
                {
                    if (player != null && player.currentHp > 0 && player.gameObject.activeSelf)
                    {
                        float dist = Vector3.Distance(attackerPos, player.transform.position);
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            bestTarget = player.gameObject;
                        }
                    }
                }
            }
            
            return bestTarget;
        }

        // 오브젝트의 위치(전열/후열, 인덱스)를 판별하는 함수
        CombatPosition GetUnitPosition(GameObject unit)
        {
            CombatPosition pos = new CombatPosition();
            
            // 1. 플레이어인 경우
            if (unit.TryGetComponent(out PlayerController pc))
            {
                // PlayerController.Initialize에서 currentRow를 저장해뒀다고 가정하거나
                // 부모 컨테이너를 확인하여 판단
                pos.isFrontRow = (pc.transform.parent.parent == playerFrontRowContainer);
                
                // 슬롯 인덱스 찾기 (부모인 Slot의 형제 인덱스)
                pos.columnIndex = pc.transform.parent.GetSiblingIndex();
            }
            // 2. 몬스터인 경우
            else if (unit.TryGetComponent(out MonsterController mc))
            {
                pos.isFrontRow = (mc.transform.parent.parent == enemyFrontRowContainer);
                pos.columnIndex = mc.columnIndex; // MonsterController는 이미 columnIndex를 갖고 있음
            }

            return pos;
        }

        // [보정치 계산] out 변수를 통해 데미지 배율과 명중률 보정을 반환
        void GetPositionalModifiers(GameObject attacker, GameObject defender, CombatAction action, 
                                    out float damageMultiplier, out float evasionBonus)
        {
            // 기본값 (보정 없음)
            damageMultiplier = 1.0f;
            evasionBonus = 0f;

            // 1. 위치 정보 가져오기
            CombatPosition atkPos = GetUnitPosition(attacker);
            CombatPosition defPos = GetUnitPosition(defender);

            // 2. 무기 타입 확인 (맨손은 Melee 취급)
            WeaponType wType = WeaponType.Melee;
            
            PlayerController pActor = attacker.GetComponent<PlayerController>();
            if (pActor != null)
            {
                // 총 공격(ActionType.Gun)이거나, 무기 데이터가 Gun이면 원거리
                if (action.type == CombatAction.ActionType.Gun) wType = WeaponType.Gun;
                else if (pActor.currentWeapon != null && pActor.currentWeapon.type == WeaponType.Gun) wType = WeaponType.Gun;
            }
            // (몬스터는 기본적으로 Melee로 가정하되, 데이터에 따라 분기 가능)

            // =========================================================
            // [규칙 1] 세로 거리 (Row) 보정
            // =========================================================
            if (wType == WeaponType.Melee)
            {
                // 근접 무기는 '후열'에 있거나 '후열'을 때릴 때 페널티
                // Case A: 내가 후열에서 때림 -> 데미지 70%
                if (!atkPos.isFrontRow) 
                {
                    damageMultiplier *= 0.7f;
                }

                // Case B: 상대를 후열까지 때려야 함 -> 데미지 80%, 회피율 증가
                if (!defPos.isFrontRow)
                {
                    damageMultiplier *= 0.8f;
                    evasionBonus += 0.1f; // 적 회피율 10% 증가
                }
            }
            else // Gun (원거리)
            {
                // 총은 거리 페널티 없음 (오히려 후열 저격에 유리)
            }

            // =========================================================
            // [규칙 2] 가로 거리 (Column) 보정 (대각선 공격 페널티)
            // =========================================================
            // 인덱스 차이 절댓값 (0: 같은열, 1: 옆줄, 2: 대각선 끝)
            int colDiff = Mathf.Abs(atkPos.columnIndex - defPos.columnIndex);

            if (colDiff == 1) // 바로 옆 줄
            {
                damageMultiplier *= 0.95f; // 5% 감소
            }
            else if (colDiff >= 2) // 정반대 대각선 (왼쪽 끝 -> 오른쪽 끝)
            {
                damageMultiplier *= 0.90f; // 10% 감소
                evasionBonus += 0.05f;     // 적 회피율 5% 증가
            }

            // 디버그용 로그 (테스트 끝나면 주석 처리)
            // Debug.Log($"[위치 보정] {attacker.name}({atkPos.columnIndex}) -> {defender.name}({defPos.columnIndex}) : Dmg x{damageMultiplier:F2}, Eva +{evasionBonus:F2}");
        }

        // 회피 성공 여부 판단
        private bool CheckEvasion(GameObject attackerObj, GameObject defenderObj, float evasionBonus)
        {
            // 1. BattleEntity로 통일해서 가져오기
            var attacker = attackerObj.GetComponent<BattleEntity>();
            var defender = defenderObj.GetComponent<BattleEntity>();

            if (attacker == null || defender == null) return false;

            // 2. 추상 메서드를 통해 스탯 바로 가져오기
            int attackerAgi = attacker.GetTotalAgi();
            int attackerLuc = attacker.GetTotalLuc();

            int defenderAgi = defender.GetTotalAgi();
            int defenderLuc = defender.GetTotalLuc();

            // ---------------------------------------------------------
            // 임시 회피 공식
            // ---------------------------------------------------------
            float baseEvasionChance = 0.05f; // 기본 5%

            // AGI 차이 보정 (방어자 AGI가 높을수록 유리)
            // 예: 차이가 10이면 +10% 포인트
            float agiBonus = (defenderAgi - attackerAgi) * 0.01f;
            agiBonus = Mathf.Clamp(agiBonus, -0.2f, 0.2f); // 최대 +/- 20%로 제한

            // LUC 차이 보정 (AGI의 절반 정도 영향력)
            float lucBonus = (defenderLuc - attackerLuc) * 0.005f;
            lucBonus = Mathf.Clamp(lucBonus, -0.1f, 0.1f); // 최대 +/- 10%로 제한

            // 위치 보정치 합산
            float totalChance = Mathf.Clamp(baseEvasionChance + agiBonus + lucBonus + evasionBonus, 0f, 0.9f);

            // 확률 디버깅용 로그 (완성 후 주석 처리)
            // Debug.Log($"[회피 체크] AGI차:{defenderAgi-attackerAgi}, LUC차:{defenderLuc-attackerLuc} -> 최종확률: {totalChance*100:F1}%");

            // 주사위 굴리기 (0.0 ~ 1.0)
            return Random.value < totalChance;
        }
        
        // 크리티컬 발생 여부 판단
        private bool CheckCritical(GameObject attackerObj, GameObject defenderObj, CombatAction action)
        {
            // 1. BattleEntity로 통일
            var attacker = attackerObj.GetComponent<BattleEntity>();
            var defender = defenderObj.GetComponent<BattleEntity>();

            if (attacker == null || defender == null) return false;

            // 2. 공격 타입에 따른 주 스탯 결정 (물리:STR, 마법:MAG)
            bool isMagic = (action.skillData != null && action.skillData.element != ElementType.Physical);

            // 3. 다스탯 가져오기
            int atkLuc = attacker.GetTotalLuc();
            int atkMainStat = isMagic ? attacker.GetMagicAttack() : attacker.GetAttack();

            int defLuc = defender.GetTotalLuc();
            int defAgi = defender.GetTotalAgi();

            // ---------------------------------------------------------
            // [크리티컬 공식]
            // ---------------------------------------------------------
            float baseCritChance = 0.05f; // 기본 5%

            // A. 운(LUC) 싸움: 운이 좋을수록, 상대보다 운이 높을수록 확률 증가
            float lucBonus = (atkLuc - defLuc) * 0.002f; // 차이 1당 0.2%

            // B. 압도적 힘/지능 vs 민첩성: 상대가 느릴수록 약점을 찌르기 쉬움
            float statBonus = (atkMainStat - defAgi) * 0.001f; // 차이 1당 0.1%

            // 최종 확률 (최소 0%, 최대 70%로 제한)
            float totalChance = Mathf.Clamp(baseCritChance + lucBonus + statBonus, 0f, 0.7f);

            // 디버그 (필요시 주석 해제)
            // Debug.Log($"[Crit Check] LUC차:{atkLuc-defLuc}, Stat차:{atkMainStat-defAgi} => 확률:{totalChance*100:F1}%");

            return Random.value < totalChance;
        }

        public int CalculateDamage(GameObject attackerObj, GameObject defenderObj, CombatAction action, bool isCritical, float damageMultiplier)
        {
            var attacker = attackerObj.GetComponent<BattleEntity>();
            var defender = defenderObj.GetComponent<BattleEntity>();

            if (attacker == null || defender == null) return 0;

            // 1. 공격력 계산
            int baseAtk = attacker.GetTotalStr(); 

            int skillPower = (action.type == CombatAction.ActionType.Attack || action.type == CombatAction.ActionType.Gun) ? 0 : (action.skillData != null ? action.skillData.basePower : 0);
            int totalAtk = baseAtk + skillPower;

            // ------------------------------------
            // 2. 방어력 (Defense) 및 내성 (Resistance) 가져오기
            // ------------------------------------
            bool isGuarding = defender.isGuarding;
            float resistanceValue = GetResistanceValue(action.skillData, defender.GetResistances()); // 기본 내성 0 (0% 감소 = 100% 피해)
            int totalDef = defender.GetDefense();
            
            // ------------------------------------
            // 3. 최종 데미지 산출
            // ------------------------------------
            // 1단계: 기본 데미지
            float rawDamage = Mathf.Max(1, totalAtk - (totalDef * 0.5f));
            // 위치 보정 배율 적용!
            rawDamage *= damageMultiplier;

            // 2단계: 내성 적용
            float resistanceMultiplier = 1.0f - resistanceValue;
            // 3단계: 랜덤 변수
            float randomVar = Random.Range(0.9f, 1.1f);
            int finalDamage = Mathf.RoundToInt(rawDamage * resistanceMultiplier * randomVar);

            // ---------------------------------------------------------
            // 크리티컬 적용 (2배)
            // ---------------------------------------------------------
            if (isCritical) finalDamage *= 2;

            // =========================================================
            // 방어(Guard) 상태일 경우 데미지 반감
            // =========================================================
            if (isGuarding)
            {
                // 50% 감소 (소수점 버림은 정수 나눗셈에서 자동 처리됨, 혹은 명시적으로 곱하기 0.5f)
                finalDamage = Mathf.FloorToInt(finalDamage * 0.5f);
                Debug.Log("방어 성공! 데미지 50% 감소");
            }

            // 최소 데미지 보정
            if (finalDamage < 1) finalDamage = 1;

            return finalDamage;
        }

        private float GetResistanceValue(SkillData skill, ResistanceData resist)
        {
            if (skill == null) return resist.physical; 
            
            ElementType type = skill.element;
            switch(type)
            {
                case ElementType.Fire:
                return resist.fire;
                case ElementType.Ice:
                    return resist.ice;
                case ElementType.Elec:
                    return resist.elec;
                case ElementType.Force:
                    return resist.force;
                case ElementType.Havoc:
                    return resist.havoc;
                default:
                    return resist.physical;
            }
        }

        // 도망 확률 계산 공식
        bool CalculateEscapeSuccess()
        {
            // 1. 아군 평균 AGI, LUC 계산 (살아있는 인원만)
            List<BattleEntity> livingPlayers = activePlayers.Where(p => p.currentHp > 0).ToList();
            if (livingPlayers.Count == 0) return false;

            float playerAvgAgi = (float)livingPlayers.Average(p => p.GetTotalAgi());
            float playerAvgLuc = (float)livingPlayers.Average(p => p.GetTotalLuc());

            // 2. 적군 평균 AGI, LUC 계산
            List<BattleEntity> livingMonsters = activeMonsters.Where(m => m.currentHp > 0).ToList();
            if (livingMonsters.Count == 0) return true; // 적이 없으면 당연히 성공

            float enemyAvgAgi = (float)livingMonsters.Average(m => m.GetTotalAgi());
            float enemyAvgLuc = (float)livingMonsters.Average(m => m.GetTotalLuc());

            // 3. 비교 공식 (기획에 따라 조절 가능)
            // 기본 확률 50%
            // AGI 차이 1당 2% 보정
            // LUC 차이 1당 1% 보정
            float baseChance = 50f;
            float agiBonus = (playerAvgAgi - enemyAvgAgi) * 2.0f;
            float lucBonus = (playerAvgLuc - enemyAvgLuc) * 1.0f;

            float finalChance = baseChance + agiBonus + lucBonus;

            // 최소 10%, 최대 100%로 제한
            finalChance = Mathf.Clamp(finalChance, 10f, 100f);

            Debug.Log($"도망 확률: {finalChance}% (아군AGI:{playerAvgAgi:F1} vs 적AGI:{enemyAvgAgi:F1})");

            // 주사위 굴리기 (0 ~ 100)
            float dice = Random.Range(0f, 100f);
            
            // 주사위 값이 확률보다 낮으면 성공
            return dice < finalChance;
        }

        IEnumerator EndBattleRoutine(bool isWin)
        {
            // 1. 상태 변경 (더 이상 입력 안 받음)
            state = isWin ? BattleState.Won : BattleState.Lost;

            // 2. 메시지 패널 켜기
            if (commandPanel) commandPanel.SetActive(false);
            if (logPanel) logPanel.SetActive(true);

            // 3. 결과 메시지 출력
            if (isWin)
            {
                logText.text = "VICTORY!\n(Press Space Key)";
                Debug.Log("승리! 경험치 획득..."); 
                SoundManager.Instance.PlayBGM(BgmID.Victory);
            }
            else
            {
                logText.text = "You Lose\n(Press Space Key)";
            }

            // 4. 플레이어가 결과를 읽을 때까지 대기 (키 입력)
            //    실수로 연타해서 넘어가는 것을 방지하기 위해 0.5초 딜레이 후 입력을 받는다.
            yield return wait05;
            
            // 플레이어가 Space나 Enter를 누를 때까지 무한 대기
            while (!Input.GetKeyDown(KeyCode.Space) && !Input.GetKeyDown(KeyCode.Return))
            {
                yield return null;
            }

            // 5. UI 끄기
            logPanel.SetActive(false);
            
            // 6. 던전 탐색 상태로 복귀
            DungeonStateManager.Instance.ChangeState(GameState.Exploration);
        }

        private void ClearParty()
        {
            // 이전 전투의 하이라이트 기록 삭제
            lastHighlightedPlayer = null; 

            // 1. 리스트 초기화
            activePlayers.Clear();
        }

    }
}
