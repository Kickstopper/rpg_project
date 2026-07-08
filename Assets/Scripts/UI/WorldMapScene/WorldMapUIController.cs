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
            if (ManagerRoot.GameState == null || ManagerRoot.GameState.CurrentState != GameState.Exploration) return;
            
            if (Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown())
            {
                ShowMenu(); 
            } 
        }

        private void ShowMenu()
        {
            if (ManagerRoot.GameState) 
                ManagerRoot.GameState.ChangeState(GameState.PlayerMenu);
        }

        private void RefreshModules()
        {
            if (weatherWidget)
                weatherWidget.SetActive(ManagerRoot.Module.IsMounted(Data.ModuleFeature.WeatherWidget));
            if (encounterSlider)
                encounterSlider.SetActive(ManagerRoot.Module.IsMounted(Data.ModuleFeature.MobSensor));
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