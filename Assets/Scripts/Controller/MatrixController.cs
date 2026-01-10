using UnityEngine;

public class MatrixController : MonoBehaviour
{
    [Header("Settings")]
    public GameObject streamPrefab; 
    public Transform canvasTransform; 
    
    [Header("Density Settings")]
    public float columnSize = 20f; // 글자 하나의 너비 (폰트 크기와 비슷하게 설정)
    
    [Header("Speed Settings")]
    public float minSpeed = 200f; 
    public float maxSpeed = 500f;

    private void Start()
    {
        SpawnDenseStreams();
    }

    void SpawnDenseStreams()
    {
        RectTransform canvasRect = canvasTransform.GetComponent<RectTransform>();
        
        // 캔버스의 실제 너비와 높이 가져오기
        float screenWidth = canvasRect.rect.width;
        float screenHeight = canvasRect.rect.height;

        // 화면 왼쪽 끝부터 오른쪽 끝까지 글자 크기(columnSize) 간격으로 생성
        // 예: 화면 1920픽셀, 글자크기 30 -> 약 64개 생성
        int streamCount = Mathf.CeilToInt(screenWidth / columnSize);
        float startX = -(screenWidth / 2) + (columnSize / 2); // 왼쪽 끝 좌표 보정

        for (int i = 0; i < streamCount; i++)
        {
            GameObject obj = Instantiate(streamPrefab, canvasTransform);
            RectTransform rect = obj.GetComponent<RectTransform>();

            // X축: 정확히 간격에 맞춰 배치 (랜덤 제거하여 빈틈 방지)
            float posX = startX + (i * columnSize);
            
            // Y축: 시작 높이를 화면 전체 랜덤한 위치로 한다.
            //float posY = Random.Range(-screenHeight / 2, screenHeight);
            float randomY = Random.Range(-screenHeight / 2, screenHeight * 1.5f);

            rect.anchoredPosition = new Vector2(posX, randomY);
            rect.sizeDelta = new Vector2(columnSize, rect.sizeDelta.y); // 너비 맞춤

            MatrixStream streamScript = obj.GetComponent<MatrixStream>();
            streamScript.Setup(Random.Range(minSpeed, maxSpeed));
        }
    }
}