using Manager;
using UnityEngine;

namespace UI.WorldMapScene
{
    public class WorldMapMenuController : MonoBehaviour
    {
        [Header("UI 연결")]
        public GameObject menuPanel;

        void Update()
        {
            if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Exploration) return;
            
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetMouseButtonDown(1))
            {
                ShowMenu(); 
            } 
        }

        private void ShowMenu()
        {
            if (GameStateManager.Instance) 
                GameStateManager.Instance.ChangeState(GameState.PlayerMenu);
        }

    }
}