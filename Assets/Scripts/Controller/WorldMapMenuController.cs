using Manager;
using UnityEngine;

namespace Controller
{
    public class WorldMapMenuController : MonoBehaviour
    {
        [Header("UI 연결")]
        public GameObject menuPanel;

        [Header("플레이어 연결")]
        public WorldMapMovementController playerMovement;

        private bool isMenuOpen = false;

        void Start()
        {
            if (menuPanel != null)
                menuPanel.SetActive(false);
        }

        void Update()
        {
            if (isMenuOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift)))
            {
                HideMenu();
            }
            
            if (!isMenuOpen && Input.GetKeyDown(KeyCode.C))
            {
                ShowMenu();
            }
        }

        private void HideMenu()
        {
            isMenuOpen = false;
            GameStateManager.Instance.ChangeState(GameState.Exploration);
            if (playerMovement != null) playerMovement.canMove = true;
        }

        private void ShowMenu()
        {
            isMenuOpen = true;
            // 1. 메뉴 패널 끄고 켜기
            GameStateManager.Instance.ChangeState(GameState.PlayerMenu);
            if (playerMovement != null) playerMovement.canMove = false;
        }

    }
}