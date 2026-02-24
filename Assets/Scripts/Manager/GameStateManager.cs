using UnityEngine;
using System.Collections.Generic;
using System;
using Controller;
using UI.Shop;
public enum GameState
{
    None,
    Exploration, // 월드맵 또는 Pseudo-3D 던전 탐험 모드
    Battle,      // 전투 모드
    PlayerMenu,        // 메뉴/인벤토리
    Event,       // 이벤트
    Shop,        // 상점
}

namespace Manager
{
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance;

        [Header("UI Groups")]
        public GameObject explorationCanvas; // 탐험용 UI
        public GameObject combatCanvas;      // 전투용 UI (커맨드, 적 이미지 등)
        public GameObject menuCanvas;        // 메뉴 UI
        public GameObject shopCanvas;        // 상점 UI

        public BattleManager currentBattleManager;
        public ShopModeSelectUI shopUIController;

        // 상태 변경 알림 이벤트
        public event Action<GameState> OnStateChanged;

        public GameState CurrentState { get; private set; }

        public bool canHardSave = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                ChangeState(GameState.Exploration);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void RegisterSceneUI(GameObject exploration, GameObject combat, GameObject menu, GameObject shop)
        {
            this.explorationCanvas = exploration;
            this.combatCanvas = combat;
            this.menuCanvas = menu;
            this.shopCanvas = shop;

            RefreshUIState();
        }

        public void ChangeState(GameState newState)
        {
            CurrentState = newState;
            Time.timeScale = 1.0f;

            // 세이브 가능 여부 로직
            canHardSave = (newState == GameState.Exploration);

            // 실제 UI 켜고 끄기
            RefreshUIState();

            OnStateChanged?.Invoke(newState);
        }

        private void RefreshUIState()
        {
            // 아직 UI가 연결되지 않았다면 무시
            if (explorationCanvas == null || combatCanvas == null || menuCanvas == null || shopCanvas == null) return;

            // 모든 캔버스 일단 끄기 
            explorationCanvas.SetActive(false);
            combatCanvas.SetActive(false);
            menuCanvas.SetActive(false);
            shopCanvas.SetActive(false);
            switch (CurrentState)
            {
                case GameState.Exploration:
                    explorationCanvas.SetActive(true);
                    break;

                case GameState.Battle:
                    combatCanvas.SetActive(true);
                    break;

                case GameState.PlayerMenu:
                    menuCanvas.SetActive(true);
                    break;
                case GameState.Shop:
                    shopCanvas.SetActive(true);
                    break;
            }
        }

        // UI 등록 시 컨트롤러도 함께 등록받음
        public void RegisterSceneComponents(GameObject expl, GameObject cbt, GameObject menu, GameObject shop, 
                                            BattleManager manager, ShopModeSelectUI shopController)
        {
            this.explorationCanvas = expl;
            this.combatCanvas = cbt;
            this.menuCanvas = menu;
            this.shopCanvas = shop;
            this.currentBattleManager = manager;
            this.shopUIController = shopController;

            RefreshUIState();
        }

        // 적을 만났을 때 호출
        public void StartEncounter(List<string> monsterList)
        {
            Debug.Log("적 출현!");
            
            if (currentBattleManager != null)
            {
                currentBattleManager.Initialize(monsterList);
                ChangeState(GameState.Battle);
            }
            else
            {
                Debug.LogError("BattleManager가 연결되지 않았습니다!");
            }
        }

        public void ShowShop(string shopID)
        {
            if (shopCanvas != null && shopUIController != null)
            {
                shopUIController.OpenShop(shopID);
                ChangeState(GameState.Shop);
            }
            else
            {
                Debug.LogWarning("Shop UI가 연결되지 않았습니다!");
            }
        }
        
    }
}
