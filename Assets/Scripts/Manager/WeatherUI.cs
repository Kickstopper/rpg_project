using Manager;
using TMPro;
using UnityEngine;

namespace UI
{
    public class WeatherUI : MonoBehaviour
    {
        public TextMeshProUGUI locationTxt;
        public TextMeshProUGUI timeTxt;
        public TextMeshProUGUI weatherTxt;
        public TextMeshProUGUI temperTxt;

        void OnEnable()
        {
            if (ManagerRoot.Weather != null)
            {
                UpdateUI(); 
                
                ManagerRoot.Weather.OnWeatherUpdated += UpdateUI; 
            }
        }

        private void OnDisable()
        {
            if (ManagerRoot.Weather != null)
            {
                ManagerRoot.Weather.OnWeatherUpdated -= UpdateUI;
            }
        }

        private void UpdateUI()
        {
            if (ManagerRoot.Weather != null)
            {
                if (locationTxt) locationTxt.text = ManagerRoot.Weather.CurrentCity;
                if (weatherTxt) weatherTxt.text = ManagerRoot.Weather.CurrentWeather;
                if (temperTxt) temperTxt.text = $"{ManagerRoot.Weather.CurrentTemp}°C";
                if (timeTxt) timeTxt.text = ManagerRoot.Weather.CurrentLocalTime.ToString("HH:mm");
            }
            else
            {
                Debug.LogWarning("WeatherManager 인스턴스를 찾을 수 없습니다.");
            }
        }
    }
}
