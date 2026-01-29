using UnityEngine;
using System.Collections.Generic;
using System;
public enum GameState
{
    TitleScreen,
    Exploration, // Pseudo-3D 던전 탐험 모드
    WorldMap,    // 월드맵
    Battle,      // 전투 모드
    PlayerMenu,        // 메뉴/인벤토리
    Event,       // 이벤트
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

        public void RegisterSceneUI(GameObject exploration, GameObject combat, GameObject menu)
        {
            this.explorationCanvas = exploration;
            this.combatCanvas = combat;
            this.menuCanvas = menu;

            // UI가 연결되었으니 현재 상태에 맞춰 화면 갱신
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

        // [분리] UI 갱신 로직을 별도 함수로 분리 (참조가 끊겼을 때 안전하게 처리)
        private void RefreshUIState()
        {
            // 아직 UI가 연결되지 않았다면 무시
            if (explorationCanvas == null || combatCanvas == null || menuCanvas == null) return;

            // 모든 캔버스 일단 끄기 
            explorationCanvas.SetActive(false);
            combatCanvas.SetActive(false);
            menuCanvas.SetActive(false);

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
            }
        }
        
        // 적을 만났을 때 호출
        public void StartEncounter(List<string> monsterList)
        {
            Debug.Log("적 출현!");
            CombatManager.Instance.Initialize(monsterList);

            // 상태 전환
            ChangeState(GameState.Battle);
        }
    }
}
