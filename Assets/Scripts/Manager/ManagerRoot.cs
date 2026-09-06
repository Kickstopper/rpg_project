using System;
using UnityEngine;

namespace Manager
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public class ManagerRoot : MonoBehaviour
    {
        private static ManagerRoot s_instance;
        private static bool s_isQuitting;

        public static ManagerRoot Instance =>
            !s_isQuitting && s_instance != null
                ? s_instance
                : null;

        [Header("Child Managers")]
        [SerializeField] private GameSettingManager gameSettingManager;
        [SerializeField] private SoundManager soundManager;
        [SerializeField] private DatabaseManager databaseManager;
        [SerializeField] private QuestManager questManager;
        [SerializeField] private DialogueManager dialogueManager;
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private DungeonManager dungeonManager;
        [SerializeField] private DungeonMapStateManager dungeonMapStateManager;
        [SerializeField] private DungeonEventManager dungeonEventManager;
        [SerializeField] private TerminalManager terminalManager;
        [SerializeField] private EffectManager effectManager;
        [SerializeField] private FlagManager flagManager;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private FinanceManager financeManager;
        [SerializeField] private ModuleManager moduleManager;
        [SerializeField] private PartyManager partyManager;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private ShopManager shopManager;
        [SerializeField] private TimeManager timeManager;
        [SerializeField] private WorldManager worldManager;
        [SerializeField] private FieldMapManager fieldMapManager;

        public static GameSettingManager GameSetting =>
            Instance?.gameSettingManager;

        public static SoundManager Sound => Instance?.soundManager;
        public static DatabaseManager Database => Instance?.databaseManager;
        public static QuestManager Quest => Instance?.questManager;
        public static DialogueManager Dialogue => Instance?.dialogueManager;
        public static GameStateManager GameState => Instance?.gameStateManager;
        public static DungeonManager Dungeon => Instance?.dungeonManager;

        public static DungeonMapStateManager DungeonMapState =>
            Instance?.dungeonMapStateManager;

        public static DungeonEventManager DungeonEvent =>
            Instance?.dungeonEventManager;

        public static TerminalManager Terminal => Instance?.terminalManager;
        public static EffectManager Effect => Instance?.effectManager;
        public static FlagManager Flag => Instance?.flagManager;
        public static InventoryManager Inventory => Instance?.inventoryManager;
        public static FinanceManager Finance => Instance?.financeManager;
        public static ModuleManager Module => Instance?.moduleManager;
        public static PartyManager Party => Instance?.partyManager;
        public static SaveManager Save => Instance?.saveManager;
        public static ShopManager Shop => Instance?.shopManager;
        public static TimeManager Time => Instance?.timeManager;
        public static WorldManager World => Instance?.worldManager;
        public static FieldMapManager FieldMap => Instance?.fieldMapManager;

        // Domain Reload를 껐을 때도 정적 상태를 초기화합니다.
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_instance = null;
            s_isQuitting = false;
        }

        private void Awake()
        {
            if (s_isQuitting)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            // 씬 전환 등으로 들어온 중복 루트 처리.
            if (s_instance != null && s_instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            // DontDestroyOnLoad를 적용할 루트는 최상위에 둡니다.
            if (transform.parent != null)
            {
                throw new InvalidOperationException(
                    "[ManagerRoot] @Managers를 씬의 최상위에 배치하세요.");
            }

            BindManagerReferences();
            ConnectQuestData();

            // 참조 검증과 연결이 끝난 뒤 인스턴스를 공개합니다.
            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void BindManagerReferences()
        {
            BindRequired(ref gameSettingManager);
            BindRequired(ref soundManager);
            BindRequired(ref databaseManager);
            BindRequired(ref questManager);
            BindRequired(ref dialogueManager);
            BindRequired(ref gameStateManager);
            BindRequired(ref dungeonManager);
            BindRequired(ref dungeonMapStateManager);
            BindRequired(ref dungeonEventManager);
            BindRequired(ref terminalManager);
            BindRequired(ref effectManager);
            BindRequired(ref flagManager);
            BindRequired(ref inventoryManager);
            BindRequired(ref financeManager);
            BindRequired(ref moduleManager);
            BindRequired(ref partyManager);
            BindRequired(ref saveManager);
            BindRequired(ref shopManager);
            BindRequired(ref timeManager);
            BindRequired(ref worldManager);
            BindRequired(ref fieldMapManager);
        }

        private void BindRequired<T>(ref T manager) where T : Component
        {
            // Inspector에 지정된 참조를 우선 사용합니다.
            if (manager == null)
            {
                T[] candidates = GetComponentsInChildren<T>(true);

                if (candidates.Length != 1)
                {
                    throw new MissingReferenceException(
                        $"[ManagerRoot] {typeof(T).Name} 참조가 필요합니다. " +
                        $"하위에서 {candidates.Length}개를 찾았습니다. " +
                        "Inspector에서 사용할 컴포넌트를 지정하세요.");
                }

                manager = candidates[0];
            }

            // 루트와 함께 유지되는 객체인지 검사합니다.
            bool belongsToRoot =
                manager.transform == transform ||
                manager.transform.IsChildOf(transform);

            if (!belongsToRoot)
            {
                throw new InvalidOperationException(
                    $"[ManagerRoot] {typeof(T).Name}을 " +
                    "@Managers 자신 또는 하위 오브젝트에 배치하세요.");
            }

            // 현재 매니저들은 Awake에서 내부 데이터를 준비합니다.
            if (!manager.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    $"[ManagerRoot] {typeof(T).Name}의 " +
                    "게임 오브젝트를 활성화하세요.");
            }
        }

        private void ConnectQuestData()
        {
            if (databaseManager.questDB == null ||
                databaseManager.questDB.db == null)
            {
                throw new MissingReferenceException(
                    "[ManagerRoot] QuestDatabase가 설정되지 않았습니다.");
            }

            // 현재 InitializeQuests는 전달받은 목록을 연결합니다.
            questManager.InitializeQuests(databaseManager.questDB.db);
        }

        private void OnApplicationQuit()
        {
            if (s_instance == this)
            {
                s_isQuitting = true;
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}