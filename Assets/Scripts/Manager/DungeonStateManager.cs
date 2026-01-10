using UnityEngine;
using System.Collections.Generic;
using System;
public enum GameState
{
    Exploration, // 탐험 모드
    Battle,      // 전투 모드
    Menu,        // 메뉴/인벤토리
    Event,       // 이벤트
}

namespace Manager
{
    public class DungeonStateManager : MonoBehaviour
    {
        public static DungeonStateManager Instance;

        [Header("UI Groups")]
        public GameObject explorationCanvas; // 탐험용 UI
        public GameObject combatCanvas;      // 전투용 UI (커맨드, 적 이미지 등)

        // 상태 변경 알림 이벤트
        public event Action<GameState> OnStateChanged;

        public GameState CurrentState { get; private set; }

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

        public void ChangeState(GameState newState)
        {
            CurrentState = newState;
            Time.timeScale = 1.0f;

            // 상태에 따라 UI와 조작을 켜고 끕니다.
            switch (newState)
            {
                case GameState.Exploration:
                    explorationCanvas.SetActive(true);
                    combatCanvas.SetActive(false);
                    // 플레이어 이동 스크립트 활성화
                    break;

                case GameState.Battle:
                    explorationCanvas.SetActive(false); // 미니맵 등 숨김
                    combatCanvas.SetActive(true);
                    // 플레이어 이동 스크립트 비활성화 (못 움직이게)
                    break;
            }

            OnStateChanged?.Invoke(newState);
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
