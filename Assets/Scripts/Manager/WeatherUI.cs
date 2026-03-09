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
            if (WeatherManager.Instance != null)
            {
                UpdateUI(); 
                
                WeatherManager.Instance.OnWeatherUpdated += UpdateUI; 
            }
        }

        private void OnDisable()
        {
            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.OnWeatherUpdated -= UpdateUI;
            }
        }

        private void UpdateUI()
        {
            if (WeatherManager.Instance != null)
            {
                if (locationTxt) locationTxt.text = WeatherManager.Instance.CurrentCity;
                if (weatherTxt) weatherTxt.text = WeatherManager.Instance.CurrentWeather;
                if (temperTxt) temperTxt.text = $"{WeatherManager.Instance.CurrentTemp}°C";
                if (timeTxt) timeTxt.text = WeatherManager.Instance.CurrentLocalTime.ToString("HH:mm");
            }
            else
            {
                Debug.LogWarning("WeatherManager 인스턴스를 찾을 수 없습니다.");
            }
        }
    }
}
