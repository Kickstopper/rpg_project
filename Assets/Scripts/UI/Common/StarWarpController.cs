using System.Collections;
using UnityEngine;

public class StarWarpController : MonoBehaviour
{
    [Header("Material Settings")]
    public Material starMaterial;

    [Header("Terminal UI Warp Settings")]
    public float warpMaxSpeed = 20.0f;
    public float warpMaxStretch = 10.0f;
    public float collapseSpeed = -100.0f;
    public float accelerationTime = 1.5f;
    public float collapseTime = 0.2f;
    public float starDensity = 50f;

    [Header("Return To Idle Settings")]
    [Tooltip("수축 후 다시 잔잔한 우주로 부드럽게 돌아오는 데 걸리는 시간")]
    public float returnTime = 1.0f;

    [Header("Title Scene Settings")]
    public float idleSpeed = 4.0f;
    public float idleStretch = 0.2f;
    public float swaySpeed = 0.1f;
    public float swayAmount = 0.15f;

    // 쉐이더 속성 ID 캐싱
    private int customTimeId;
    private int stretchId;
    private int viewOffsetId;
    private int masterAlphaId;

    // C#에서 직접 관리하는 시간과 속도
    private float currentSpeed = 0f;
    private float customTime = 0f;
    
    private bool isWarping = false;
    private Vector2 currentOffset = Vector2.zero;

    void Start()
    {
        //starDensity = Shader.PropertyToID("_Density");
        starMaterial.SetFloat("_Density", starDensity);

        customTimeId = Shader.PropertyToID("_CustomTime");
        stretchId = Shader.PropertyToID("_Stretch");
        viewOffsetId = Shader.PropertyToID("_ViewOffset");
        masterAlphaId = Shader.PropertyToID("_MasterAlpha");

        SetTitleIdleMode();
    }

    void Update()
    {
        // 현재 속도를 기반으로 C#에서 자체적으로 시간을 누적
        // 속도가 음수로 변해도 시간의 흐름이 단절되지 않고 부드럽게 역재생됨
        customTime += Time.deltaTime * currentSpeed;

        if (starMaterial != null)
        {
            // 누적된 시간을 쉐이더에 전달
            starMaterial.SetFloat(customTimeId, customTime);

            if (!isWarping)
            {
                // 워프 중이 아닐 때 표류 효과
                float noiseX = (Mathf.PerlinNoise(Time.time * swaySpeed, 0f) * 2f) - 1f;
                float noiseY = (Mathf.PerlinNoise(0f, Time.time * swaySpeed) * 2f) - 1f;
                
                currentOffset = new Vector2(noiseX, noiseY) * swayAmount;
                starMaterial.SetVector(viewOffsetId, currentOffset);
            }
        }
    }

    public void SetTitleIdleMode()
    {
        StopAllCoroutines();
        isWarping = false;
        
        // 속도를 C# 변수에 대입
        currentSpeed = idleSpeed; 

        if (starMaterial != null)
        {
            starMaterial.SetFloat(stretchId, idleStretch);
            starMaterial.SetFloat(masterAlphaId, 1.0f); // 투명도 원상 복구
        }
    }

    public void PlayWarpAndCollapse()
    {
        if (starMaterial != null)
        {
            StopAllCoroutines();
            StartCoroutine(WarpSequence());
        }
    }

    private IEnumerator WarpSequence()
    {
        isWarping = true;
        float elapsedTime = 0f;
        
        // 연출이 끝난 후 투명해진 화면을 즉시 100% 보이게 복구
        starMaterial.SetFloat(masterAlphaId, 1.0f);
        
        // 현재 속도가 0이든 음수이든 무시하고, 무조건 기본 상태부터 시작하도록 강제 지정
        float startSpeed = idleSpeed;
        float startStretch = idleStretch;
        
        // 시점 흔들림도 현재 위치를 기준으로 다시 중앙으로 모일 수 있게 캡처
        Vector2 startOffset = currentOffset;

        // 가속 및 시점 중앙 정렬
        while (elapsedTime < accelerationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / accelerationTime;
            float curve = t * t * t; 
            
            currentSpeed = Mathf.Lerp(startSpeed, warpMaxSpeed, curve);
            starMaterial.SetFloat(stretchId, Mathf.Lerp(startStretch, warpMaxStretch, curve));
            starMaterial.SetVector(viewOffsetId, Vector2.Lerp(startOffset, Vector2.zero, curve));
            
            yield return null;
        }

        // 중심 소실점으로 수축 및 화면 비우기
        elapsedTime = 0f;
        float collapseStartSpeed = currentSpeed;
        float collapseStartStretch = starMaterial.GetFloat(stretchId);

        while (elapsedTime < collapseTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / collapseTime;
            float curve = 1f - (1f - t) * (1f - t);

            currentSpeed = Mathf.Lerp(collapseStartSpeed, collapseSpeed, curve);
            starMaterial.SetFloat(stretchId, Mathf.Lerp(collapseStartStretch, 0f, curve));
            
            // 수축과 동시에 알파값을 1에서 0으로 페이드아웃
            starMaterial.SetFloat(masterAlphaId, Mathf.Lerp(1.0f, 0.0f, curve));
            
            yield return null;
        }

        // 완전히 수축한 후 상태 고정
        currentSpeed = 0f;
        starMaterial.SetFloat(stretchId, 0f);
        starMaterial.SetVector(viewOffsetId, Vector2.zero);
        
        // 루프 종료 후 혹시 모를 오차를 방지하기 위해 투명도 0을 명시적으로 확정
        starMaterial.SetFloat(masterAlphaId, 0.0f); 
    }

    // 진행 중인 연출을 즉시 중단하고 Idle 상태로 초기화
    public void Reset()
    {
        // 진행 중인 워프 수축이나 페이드 복귀 코루틴을 즉시 강제 종료
        StopAllCoroutines();
        
        // Update()문의 카메라 자연 표류가 다시 작동하도록 상태 해제
        isWarping = false; 
        
        // 속도를 잔잔한 기본 속도로 즉시 변경
        currentSpeed = idleSpeed;

        if (starMaterial != null)
        {
            // 선 길이와 화면 투명도를 즉시 100% 보이게 덮어쓰기
            starMaterial.SetFloat(stretchId, idleStretch);
            starMaterial.SetFloat(masterAlphaId, 1.0f);
            
            // 시점을 즉시 정중앙으로 초기화
            currentOffset = Vector2.zero;
            starMaterial.SetVector(viewOffsetId, Vector2.zero);
        }
    }

    // 수축되어 어두워진 화면에서 다시 Idle 상태로 부드럽게 복귀하는 연출
    public void PlayReturnToIdle()
    {
        if (starMaterial != null)
        {
            StopAllCoroutines();
            StartCoroutine(ReturnToIdleSequence());
        }
    }

    private IEnumerator ReturnToIdleSequence()
    {
        // 복귀 중 시점이 갑자기 튀지 않도록 워프 상태 유지
        isWarping = true; 
        float elapsedTime = 0f;

        // 현재 쉐이더의 상태 캡처 (보통 속도=0, Alpha=0, Stretch=0 인 상태)
        float startAlpha = starMaterial.GetFloat(masterAlphaId);
        float startSpeed = currentSpeed;
        float startStretch = starMaterial.GetFloat(stretchId);

        while (elapsedTime < returnTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / returnTime;
            
            // 처음엔 빠르게, 끝날 땐 부드럽게 안착 (Ease-Out 곡선)
            float curve = 1f - (1f - t) * (1f - t);

            // 속도, 선 길이, 투명도를 Idle로 서서히 변환
            currentSpeed = Mathf.Lerp(startSpeed, idleSpeed, curve);
            starMaterial.SetFloat(stretchId, Mathf.Lerp(startStretch, idleStretch, curve));
            starMaterial.SetFloat(masterAlphaId, Mathf.Lerp(startAlpha, 1.0f, curve));
            
            yield return null;
        }

        // 목표치로 완벽하게 고정
        currentSpeed = idleSpeed;
        starMaterial.SetFloat(stretchId, idleStretch);
        starMaterial.SetFloat(masterAlphaId, 1.0f);
        
        // 워프 상태를 종료하여 Update()문의 자연스러운 카메라 Sway를 다시 시작
        isWarping = false; 
    }
}