using UnityEngine;
using Manager;

public class DayNightColorController : MonoBehaviour
{
    [Tooltip("색상을 바꿀 대상을 연결해주세요. (SpriteRenderer, Camera 등)")]
    public SpriteRenderer targetRenderer; 

    [Header("Time Palettes (4등분)")]
    public Color morningColor = new Color(1f, 0.9f, 0.7f); // 아침: 06시~12시
    public Color dayColor = new Color(1f, 1f, 1f);         // 낮: 12시~17시
    public Color eveningColor = new Color(1f, 0.6f, 0.4f); // 저녁: 17시~20시
    public Color nightColor = new Color(0.2f, 0.2f, 0.4f); // 밤: 20시~06시

    [Header("Settings")]
    [Tooltip("체크 시 시간에 따라 색상이 부드럽게 섞입니다. 해제 시 지정된 시간에 색상이 즉각 변합니다.")]
    public bool smoothTransition = true; 

    private void Start()
    {
        if (ManagerRoot.Time != null)
        {
            ManagerRoot.Time.OnTimeUpdated += UpdateColor;
            
            // 시작 시 현재 시간에 맞는 색상으로 초기화
            UpdateColor(); 
        }
    }

    private void OnDestroy()
    {
        if (ManagerRoot.Time != null)
        {
            ManagerRoot.Time.OnTimeUpdated -= UpdateColor;
        }
    }

    private void UpdateColor()
    {
        // 걸음 수를 24시간 체계의 시간으로 변환
        // 예: 100보 기준 50보를 걸었다면 -> (50 / 100) * 24 = 12시
        float currentHour = ((float)ManagerRoot.Time.CurrentSteps / ManagerRoot.Time.stepsPerDay) * 24f;

        // 현재 시간에 맞는 색상 계산
        Color targetColor = GetColorForHour(currentHour);

        if (targetRenderer != null)
        {
            targetRenderer.color = targetColor;
        }
    }

    private Color GetColorForHour(float hour)
    {
        if (!smoothTransition)
        {
            // 지정된 시간에 딱 맞춰 색이 변함
            if (hour >= 6f && hour < 12f) return morningColor;   // 아침 (06~12)
            if (hour >= 12f && hour < 17f) return dayColor;      // 낮 (12~17)
            if (hour >= 17f && hour < 20f) return eveningColor;  // 저녁 (17~20)
            return nightColor;                                   // 밤 (20~06)
        }
        else
        {
            // 걸음 수에 따라 실시간으로 색이 자연스럽게 섞임
            if (hour >= 6f && hour < 12f)
            {
                float t = (hour - 6f) / (12f - 6f);
                return Color.Lerp(morningColor, dayColor, t);
            }
            else if (hour >= 12f && hour < 17f)
            {
                float t = (hour - 12f) / (17f - 12f);
                return Color.Lerp(dayColor, eveningColor, t);
            }
            else if (hour >= 17f && hour < 20f)
            {
                float t = (hour - 17f) / (20f - 17f);
                return Color.Lerp(eveningColor, nightColor, t);
            }
            else // 밤 (20시 ~ 다음날 06시)
            {
                // 밤 시간대는 자정(24시)을 넘어가므로 계산을 분리
                if (hour >= 20f)
                {
                    // 20시 ~ 24시 구간 (다음날 6시까지 총 10시간 중 앞부분 4시간)
                    float t = (hour - 20f) / 10f; 
                    return Color.Lerp(nightColor, morningColor, t);
                }
                else
                {
                    // 00시 ~ 06시 구간 (총 10시간 중 뒷부분 6시간)
                    float t = (hour + 4f) / 10f; // 앞의 4시간(20~24시)을 더해줌
                    return Color.Lerp(nightColor, morningColor, t);
                }
            }
        }
    }
}