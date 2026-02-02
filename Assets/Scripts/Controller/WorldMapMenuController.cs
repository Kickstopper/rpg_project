using Manager;
using UnityEngine;

namespace Controller
{
    public class WorldMapMenuController : MonoBehaviour
    {
        [Header("UI 연결")]
        public GameObject menuPanel;

        void Update()
        {
            if (GameStateManager.Instance.CurrentState != GameState.Exploration) return;
            
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ShowMenu(); 
            } 
        }

        private void ShowMenu()
        {
            GameStateManager.Instance.ChangeState(GameState.PlayerMenu);
        }

    }
}