using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Manager
{
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        public event Action OnWeatherUpdated;

        [Serializable]
        public class WeatherData
        {
            public WeatherInfo[] weather;
            public MainInfo main;
            public string name; // 도시 이름
            public int timezone;
        }

        [Serializable]
        public class WeatherInfo
        {
            public string main; // 날씨 상태
            public string description;
        }

        [Serializable]
        public class MainInfo
        {
            public float temp; // 현재 온도
        }

        [Serializable]
        public class LocationInfo
        {
            public string city;    
            public string loc; // 위도와 경도 "37.2411,131.8688"
        }

        private string apiKey = "b5ca9daa3edf371c2ce4a0838687bc9b";
        
        public string CurrentCity { get; private set; } = "UNKNOWN AREA";
        public string CurrentWeather { get; private set; } = "SYSTEM ERROR";
        public float CurrentTemp { get; private set; } = 0f;
        public DateTime CurrentLocalTime { get; private set; }
        private float updateInterval = 600f; // 10분

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject); 
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InvokeRepeating(nameof(UpdateWeatherProcess), 0f, updateInterval);
        }

        private void UpdateWeatherProcess()
        {
            StartCoroutine(GetLocationAndWeather());
        }

        // IP 기반으로 현재 위치 파악
        private IEnumerator GetLocationAndWeather()
        {
            string locationUrl = "https://ipinfo.io/json";
            string targetLat = "";
            string targetLon = "";
            string displayCityName = CurrentCity;
            
            using (UnityWebRequest locRequest = UnityWebRequest.Get(locationUrl))
            {
                locRequest.timeout = 5;
                yield return locRequest.SendWebRequest();

                if (locRequest.result == UnityWebRequest.Result.Success)
                {
                    LocationInfo locData = JsonUtility.FromJson<LocationInfo>(locRequest.downloadHandler.text);
                    
                    // loc 데이터가 존재하는지 확인하고 위도와 경도로 분리
                    if (!string.IsNullOrEmpty(locData.loc))
                    {
                        string[] coords = locData.loc.Split(',');
                        if (coords.Length == 2)
                        {
                            targetLat = coords[0];
                            targetLon = coords[1];
                            displayCityName = locData.city;
                            Debug.Log($"IP 감지 성공! 좌표: {targetLat}, {targetLon}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("위치 감지 실패. 기본 설정된 좌표(서울)로 날씨를 요청합니다.");
                    // 위치 감지 실패 시 서울의 좌표를 기본값으로 세팅
                    targetLat = "37.5665";
                    targetLon = "126.9780";
                    displayCityName = "Seoul (Default)";
                    string jsonResponse = locRequest.downloadHandler.text;
                    LocationInfo locData = JsonUtility.FromJson<LocationInfo>(jsonResponse);
                }
            }

            yield return StartCoroutine(GetWeatherByCoordinates(targetLat, targetLon, displayCityName));
        }

        // 좌표 기반으로 날씨를 요청하는 코루틴
        private IEnumerator GetWeatherByCoordinates(string lat, string lon, string cityName)
        {
            string url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={apiKey}&units=metric";

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.timeout = 10;
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    WeatherData data = JsonUtility.FromJson<WeatherData>(jsonResponse);

                    CurrentCity = cityName; 
                    CurrentWeather = data.weather[0].main;
                    CurrentTemp = data.main.temp;
                    CurrentLocalTime = DateTime.UtcNow.AddSeconds(data.timezone);
                }
                else
                {
                    Debug.LogError("날씨 정보를 가져올 수 없습니다.");
                    CurrentCity = "UNKNOWN AREA";
                    CurrentWeather = "OFFLINE";
                    CurrentTemp = 0f;
                    CurrentLocalTime = DateTime.Now;
                }
                
                OnWeatherUpdated?.Invoke(); 
            }
        }
    }
}
