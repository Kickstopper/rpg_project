using System.Collections;
using UnityEngine;

public class MatrixController : MonoBehaviour
{
    [Header("Settings")]
    public GameObject streamPrefab; 
    public Transform canvasTransform; 
    
    [Header("Density Settings")]
    public float columnSize = 32;
    public int streamsPerColumn = 2; 
    
    [Header("Speed Settings")]
    public float minSpeed = 50f;
    public float maxSpeed = 150f;

    // Start를 코루틴으로 변경하여 UI 레이아웃이 갱신될 시간을 줍니다.
    private IEnumerator Start()
    {
        // UI 컴포넌트가 켜지고 RectTransform 크기가 정해질 때까지 1프레임 대기
        yield return null; 
        SpawnDenseStreams();
    }

    void SpawnDenseStreams()
    {
        RectTransform canvasRect = canvasTransform.GetComponent<RectTransform>();
        
        float screenWidth = canvasRect.rect.width;
        float screenHeight = canvasRect.rect.height;

        // 혹시라도 크기를 못 가져왔다면 기기 해상도로 임시 대체하는 안전장치
        if (screenWidth <= 0) screenWidth = Screen.width;
        if (screenHeight <= 0) screenHeight = Screen.height;

        int streamCount = Mathf.CeilToInt(screenWidth / columnSize);
        float startX = -(screenWidth / 2) + (columnSize / 2); 

        for (int i = 0; i < streamCount; i++)
        {
            float posX = startX + (i * columnSize);
            
            for (int j = 0; j < streamsPerColumn; j++)
            {
                GameObject obj = Instantiate(streamPrefab, canvasTransform);
                RectTransform rect = obj.GetComponent<RectTransform>();

                // 프리팹 생성 시 스케일과 Z축 위치를 강제로 초기화합니다.
                // 이 두 줄이 없으면 UI가 캔버스 뒤로 숨어버리거나 점(Dot)만하게 작아질 수 있습니다.
                rect.localScale = Vector3.one;
                rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, 0f);

                float randomY = Random.Range(-screenHeight, screenHeight * 2f);

                rect.anchoredPosition = new Vector2(posX, randomY);
                rect.sizeDelta = new Vector2(columnSize, rect.sizeDelta.y);

                MatrixStream streamScript = obj.GetComponent<MatrixStream>();
                streamScript.Setup(minSpeed, maxSpeed);
            }
        }
    }
}