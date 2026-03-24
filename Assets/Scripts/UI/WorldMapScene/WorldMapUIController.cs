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
            
            if (Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown())
            {
                ShowMenu(); 
            } 
        }

        private void ShowMenu()
        {
            if (GameStateManager.Instance) 
                GameStateManager.Instance.ChangeState(GameState.PlayerMenu);
        }

        private void RefreshModules()
        {
            if (weatherWidget)
                weatherWidget.SetActive(ModuleManager.Instance.IsMounted(Data.ModuleFeature.WeatherWidget));
            if (encounterSlider)
                encounterSlider.SetActive(ModuleManager.Instance.IsMounted(Data.ModuleFeature.MobSensor));
        }

        private void HideModules()
        {
            if (weatherWidget) weatherWidget.SetActive(false);
            if (encounterSlider) encounterSlider.SetActive(false);
        }

        void OnEnable()
        {
            RefreshModules();
        }

        void OnDisable()
        {
            HideModules();
        }
    }
}