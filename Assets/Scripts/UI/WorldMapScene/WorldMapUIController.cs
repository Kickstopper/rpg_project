using Manager;
using UnityEngine;

namespace UI.WorldMapScene
{
    public class WorldMapUIController : MonoBehaviour
    {
        [Header("UI 연결")]
        public GameObject menuPanel;
        public GameObject encounterSlider;
        public GameObject weatherWidget;

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

        void OnEnable()
        {
            if (weatherWidget && ModuleManager.Instance.IsMounted(Data.ModuleFeature.WeatherWidget))
                weatherWidget.SetActive(true);
            if (encounterSlider && ModuleManager.Instance.IsMounted(Data.ModuleFeature.MobSensor))
                encounterSlider.SetActive(false);
        }

        void OnDisable()
        {
            if (weatherWidget) weatherWidget.SetActive(false);
            if (encounterSlider) encounterSlider.SetActive(false);
        }

    }
}