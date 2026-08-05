using UnityEngine;

namespace Manager
{
    public class ManagerRoot : MonoBehaviour
    {
        private static ManagerRoot s_instance;
        
        // 앱이 종료 중인지 추적하는 플래그
        private static bool s_isQuitting = false; 

        public static ManagerRoot Instance 
        { 
            get 
            { 
                // 종료 중일 때는 유령 객체를 만들지 않고 즉시 null 반환
                if (s_isQuitting) return null; 
                
                Init(); 
                return s_instance; 
            } 
        }

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
        [SerializeField] private ModuleManager moduleManager;
        [SerializeField] private PartyManager partyManager;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private ShopManager shopManager;
        [SerializeField] private TimeManager timeManager;
        [SerializeField] private WorldManager worldManager;
        [SerializeField] private FieldMapManager fieldMapManager;

        public static GameSettingManager GameSetting => Instance?.gameSettingManager;
        public static SoundManager Sound => Instance?.soundManager;
        public static DatabaseManager Database => Instance?.databaseManager;
        public static QuestManager Quest => Instance?.questManager;
        public static DialogueManager Dialogue => Instance?.dialogueManager;
        public static GameStateManager GameState => Instance?.gameStateManager;
        public static DungeonManager Dungeon => Instance?.dungeonManager;
        public static DungeonMapStateManager DungeonMapState => Instance?.dungeonMapStateManager;
        public static DungeonEventManager DungeonEvent => Instance?.dungeonEventManager;
        public static TerminalManager Terminal => Instance?.terminalManager;
        public static EffectManager Effect => Instance?.effectManager;
        public static FlagManager Flag => Instance?.flagManager;
        public static InventoryManager Inventory => Instance?.inventoryManager;
        public static ModuleManager Module => Instance?.moduleManager;
        public static PartyManager Party => Instance?.partyManager;
        public static SaveManager Save => Instance?.saveManager;
        public static ShopManager Shop => Instance?.shopManager;
        public static TimeManager Time => Instance?.timeManager;
        public static WorldManager World => Instance?.worldManager;
        public static FieldMapManager FieldMap => Instance?.fieldMapManager;
        private void Awake()
        {
            if (s_instance == null)
            {
                Init();
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        // 강제 종료 시 플래그 켜기
        private void OnApplicationQuit()
        {
            s_isQuitting = true;
        }

        // 매니저가 정상적으로 파괴될 때도 플래그 켜기
        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_isQuitting = true;
            }
        }

        private static void Init()
        {
            // 종료 중이 아닐 때만 재생성 시도
            if (s_instance == null && !s_isQuitting)
            {
                GameObject go = GameObject.Find("@Managers");
                
                if (go == null)
                {
                    go = new GameObject { name = "@Managers" };
                    go.AddComponent<ManagerRoot>();
                    return; 
                }

                DontDestroyOnLoad(go);
                s_instance = go.GetComponent<ManagerRoot>();

                s_instance.InitializeAllManagers();
            }
        }

        private void InitializeAllManagers()
        {
            if (soundManager == null) soundManager = GetComponentInChildren<SoundManager>();
            if (gameSettingManager == null) gameSettingManager = GetComponentInChildren<GameSettingManager>();
            if (databaseManager == null) databaseManager = GetComponentInChildren<DatabaseManager>();
            if (questManager == null) questManager = GetComponentInChildren<QuestManager>();
            if (questManager != null && databaseManager != null && databaseManager.questDB != null)
            {
                questManager.InitializeQuests(databaseManager.questDB.db);
            }
            if (timeManager == null) timeManager = GetComponentInChildren<TimeManager>();
            if (dialogueManager == null) dialogueManager = GetComponentInChildren<DialogueManager>();
            if (gameStateManager == null) gameStateManager = GetComponentInChildren<GameStateManager>();
            if (dungeonManager == null) dungeonManager = GetComponentInChildren<DungeonManager>();
            if (dungeonMapStateManager == null) dungeonMapStateManager = GetComponentInChildren<DungeonMapStateManager>();
            if (dungeonEventManager == null) dungeonEventManager = GetComponentInChildren<DungeonEventManager>();
            if (terminalManager == null) terminalManager = GetComponentInChildren<TerminalManager>();
            if (effectManager == null) effectManager = GetComponentInChildren<EffectManager>();
            if (flagManager == null) flagManager = GetComponentInChildren<FlagManager>();
            if (inventoryManager == null) inventoryManager = GetComponentInChildren<InventoryManager>();
            if (moduleManager == null) moduleManager = GetComponentInChildren<ModuleManager>();
            if (partyManager == null) partyManager = GetComponentInChildren<PartyManager>();
            if (saveManager == null) saveManager = GetComponentInChildren<SaveManager>();
            if (shopManager == null) shopManager = GetComponentInChildren<ShopManager>();
            if (worldManager == null) worldManager = GetComponentInChildren<WorldManager>();
            if (fieldMapManager == null) fieldMapManager = GetComponentInChildren<FieldMapManager>();
        }
    }
}