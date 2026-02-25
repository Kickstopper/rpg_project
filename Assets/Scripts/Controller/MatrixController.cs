using UnityEngine;

public class MatrixController : MonoBehaviour
{
    [Header("Settings")]
    public GameObject streamPrefab; 
    public Transform canvasTransform; 
    
    [Header("Density Settings")]
    public float columnSize = 32;
    public int streamsPerColumn = 2; // 한 세로줄에 떨어질 문자열 뭉치의 개수
    
    [Header("Speed Settings")]
    public float minSpeed = 50f;
    public float maxSpeed = 150f;

    void Start()
    {
        SpawnDenseStreams();
    }

    void SpawnDenseStreams()
    {
        RectTransform canvasRect = canvasTransform.GetComponent<RectTransform>();
        
        // 캔버스의 실제 너비와 높이 가져오기
        float screenWidth = canvasRect.rect.width;
        float screenHeight = canvasRect.rect.height;

        // 화면의 가로 사이즈에 맞춰 문자 스트림 생성
        int streamCount = Mathf.CeilToInt(screenWidth / columnSize);
        float startX = -(screenWidth / 2) + (columnSize / 2); 

        for (int i = 0; i < streamCount; i++)
        {
            float posX = startX + (i * columnSize);
            
            // 한 열(X 좌표)에 streamsPerColumn 개수만큼 스트림을 추가 생성 (세로 밀도 증가)
            for (int j = 0; j < streamsPerColumn; j++)
            {
                GameObject obj = Instantiate(streamPrefab, canvasTransform);
                RectTransform rect = obj.GetComponent<RectTransform>();

                // Y축: 겹치지 않게 더 넓은 범위(-screenHeight ~ screenHeight * 2)에서 랜덤 분산
                float randomY = Random.Range(-screenHeight, screenHeight * 2f);

                rect.anchoredPosition = new Vector2(posX, randomY);
                rect.sizeDelta = new Vector2(columnSize, rect.sizeDelta.y);

                MatrixStream streamScript = obj.GetComponent<MatrixStream>();
                streamScript.Setup(Random.Range(minSpeed, maxSpeed));
            }
        }
    }
}