using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager;

public class MonsterCompileManager : MonoBehaviour
{
    [Header("Pool & Canvas")]
    [SerializeField] private AsciiObjectPool objectPool;
    [SerializeField] private RectTransform uiCanvasRoot;
    [SerializeField] private float gridSpacing = 16f;

    [Header("Monster A")]
    [SerializeField] private Image spriteA;
    [SerializeField] private TextAsset asciiA;

    [Header("Monster B")]
    [SerializeField] private Image spriteB;
    [SerializeField] private TextAsset asciiB;

    [Header("Result Monster")]
    [SerializeField] private Image spriteResult;
    [SerializeField] private TextAsset asciiResult;
    [SerializeField] private TextMeshProUGUI compileResultText;
    
    [SerializeField] private CanvasGroup matrixEffectGroup; 
    
    // 타자기 연출 속도 제어
    public float lineDelay = 0.05f; 
    [SerializeField] private string compileResultMsg = "HELLO WORLD";

    [Header("Timing Settings")]
    [SerializeField] private float moveAndFadeDuration = 1.0f;
    [SerializeField] private float asciiRevealDuration = 1.5f;
    [SerializeField] private float asciiDissolveDuration = 1.5f;

    private List<GameObject> activeAsciiNodes = new List<GameObject>();

    public System.Action OnCompileFinished;

    [ContextMenu("Test: Start New Sequence")]
    public void StartCompileSequence()
    {
        StartCoroutine(CompileSequenceRoutine());
    }

    // CompileUIController에서 이 함수를 호출하여 컷신을 시작
    public void StartCompileSequence(string monsterA_ID, string monsterB_ID)
    {
        var monsterA = ManagerRoot.Database.monsterDB.GetEntry(monsterA_ID);
        if (monsterA != null)
        {
            if (spriteA != null && monsterA.image != null)
            {
                spriteA.sprite = monsterA.image[0];
                spriteA.SetNativeSize();
            }
            asciiA = monsterA.compileAscii;
        }
        var monsterB = ManagerRoot.Database.monsterDB.GetEntry(monsterB_ID);
        if (monsterB != null)
        {
            if (spriteB != null && monsterB.image != null)
            {
                spriteB.sprite = monsterB.image[0];
                spriteB.SetNativeSize();
            }
            asciiB = monsterB.compileAscii;
        }

        var result = ManagerRoot.Database.monsterDB.GetCompileResult(monsterA_ID, monsterB_ID);
        if (result != null)
        {
            if (spriteResult != null && result.image != null)
            {
                spriteResult.sprite = result.image[0];
                spriteResult.SetNativeSize();

                asciiResult = result.compileAscii;
                compileResultMsg = result.compileResultMsg;
                
                StartCoroutine(CompileSequenceRoutine());
            }
        }
        else
        {
            Debug.LogError($"[{monsterA_ID}]와 [{monsterB_ID}]의 합체 결과를 찾을 수 없어 연출을 취소합니다.");
            
            // UI를 강제로 원래대로 돌려놓기 위해 즉시 콜백 호출
            OnCompileFinished?.Invoke(); 
        }
    }

    private IEnumerator CompileSequenceRoutine()
    {
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

        // 화면 초기화
        compileResultText.text = "";
        spriteA.color = new Color(1, 1, 1, 0);
        spriteB.color = new Color(1, 1, 1, 0);
        spriteResult.color = new Color(1, 1, 1, 0);
        
        // 매트릭스 효과 투명도 0으로 초기화
        if (matrixEffectGroup != null) matrixEffectGroup.alpha = 0f;

        // 1280x720 해상도 기준, 화면 밖 좌표 (왼쪽, 오른쪽)
        Vector2 leftOffScreen = new Vector2(-1280, 0);
        Vector2 rightOffScreen = new Vector2(1280, 0);
        Vector2 centerPos = Vector2.zero;

        // 몬스터 A 시퀀스
        yield return StartCoroutine(MoveAndFadeSprite(spriteA, leftOffScreen, centerPos, moveAndFadeDuration, true));

        DrawAscii(asciiA, 0f);
        StartCoroutine(FadeSpriteAlpha(spriteA, 1f, 0f, asciiRevealDuration));
        yield return StartCoroutine(RevealAsciiSequential(asciiRevealDuration));

        yield return StartCoroutine(DissolveAsciiRandomly(asciiDissolveDuration));
        
        // 몬스터 B 시퀀스
        yield return StartCoroutine(MoveAndFadeSprite(spriteB, rightOffScreen, centerPos, moveAndFadeDuration, true));

        DrawAscii(asciiB, 0f);
        StartCoroutine(FadeSpriteAlpha(spriteB, 1f, 0f, asciiRevealDuration));
        yield return StartCoroutine(RevealAsciiSequential(asciiRevealDuration));

        yield return StartCoroutine(DissolveAsciiRandomly(asciiDissolveDuration));

        // 결과 몬스터 시퀀스
        DrawAscii(asciiResult, 0f);

        // 결과 몬스터 아스키 랜덤 페이드인 & 50% 시점에 스프라이트와 매트릭스 효과 동시 페이드인
        yield return StartCoroutine(RevealResultAsciiAndSprite(asciiRevealDuration));

        // 결과 몬스터 아스키 전체 페이드아웃
        yield return StartCoroutine(FadeOutAllAscii(1.0f));

        // 인사말 표시
        compileResultText.text = "";

        // 키보드 연타로 인한 오작동을 막기 위해 프레임 버퍼를 한 번 비워줌
        yield return null; 

        bool isSkipped = false;
        foreach (char c in compileResultMsg)
        {
            compileResultText.text += c;
            
            if (!isSkipped)
            {
                float timer = 0;
                while (timer < lineDelay)
                {
                    timer += Time.deltaTime;
                    
                    // 타이핑 도중 스페이스/엔터/클릭 시 전체 문장 즉시 출력 (스킵)
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                    {
                        isSkipped = true;
                        break;
                    }
                    yield return null;
                }
            }
        }

        // 스킵이 발동했다면 남은 문장을 한 번에 출력
        if (isSkipped)
        {
            compileResultText.text = compileResultMsg;
            
            // 스킵할 때 누른 키보드 입력이 아래의 창 닫기 로직까지 연달아 실행시키지 않도록 한 프레임 대기
            yield return null; 
        }

        // 인삿말이 완전히 다 나온 후, 플레이어의 입력을 대기하여 창 닫기
        yield return new WaitUntil(() => 
            Input.GetKeyDown(KeyCode.Space) || 
            Input.GetKeyDown(KeyCode.Return) || 
            Input.GetMouseButtonDown(0)
        );

        // 마무리 정리 및 콜백 호출
        objectPool.ReturnAllObjects(activeAsciiNodes);
        OnCompileFinished?.Invoke();
    }

    // 헬퍼 함수들
    private void DrawAscii(TextAsset asciiData, float initialAlpha)
    {
        if (activeAsciiNodes.Count > 0) objectPool.ReturnAllObjects(activeAsciiNodes);

        string[] lines = asciiData.text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        int totalRows = lines.Length;
        int totalCols = lines[0].Length;

        float startX = -(totalCols - 1) * gridSpacing * 0.5f;
        float startY = (totalRows - 1) * gridSpacing * 0.5f;

        for (int y = 0; y < totalRows; y++)
        {
            for (int x = 0; x < totalCols; x++)
            {
                if (lines[y][x] == ' ') continue;

                Vector2 pos = new Vector2(startX + (x * gridSpacing), startY - (y * gridSpacing));
                GameObject nodeObj = objectPool.GetObjectFromPool(uiCanvasRoot, pos);

                // 몬스터 합체 반복 시 스케일 축소 버그 방지
                RectTransform rect = nodeObj.GetComponent<RectTransform>();
                rect.localScale = Vector3.one; 
                rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, 0f);
                rect.localRotation = Quaternion.identity;
                
                TextMeshProUGUI tmp = nodeObj.GetComponent<TextMeshProUGUI>();
                tmp.text = lines[y][x].ToString();
                tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, initialAlpha); 
                activeAsciiNodes.Add(nodeObj);
            }
        }
    }

    private IEnumerator MoveAndFadeSprite(Image img, Vector2 start, Vector2 end, float duration, bool fadeIn)
    {
        float time = 0;
        img.rectTransform.anchoredPosition = start;
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float easeOutT = 1f - Mathf.Pow(1f - t, 3f);
            
            img.rectTransform.anchoredPosition = Vector2.Lerp(start, end, easeOutT);
            img.color = new Color(1, 1, 1, Mathf.Lerp(startAlpha, endAlpha, t));
            yield return null;
        }
    }

    private IEnumerator FadeSpriteAlpha(Image img, float startAlpha, float endAlpha, float duration)
    {
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            img.color = new Color(1, 1, 1, Mathf.Lerp(startAlpha, endAlpha, time / duration));
            yield return null;
        }
    }

    // 알파 페이드 헬퍼 코루틴
    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float startAlpha, float endAlpha, float duration)
    {
        if (cg == null) yield break;
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
            yield return null;
        }
    }

    private IEnumerator RevealAsciiSequential(float duration)
    {
        int totalNodes = activeAsciiNodes.Count;
        int nodesPerFrame = Mathf.CeilToInt(totalNodes / (duration / Time.deltaTime));

        int currentIndex = 0;
        while (currentIndex < totalNodes)
        {
            for (int i = 0; i < nodesPerFrame && currentIndex < totalNodes; i++)
            {
                TextMeshProUGUI tmp = activeAsciiNodes[currentIndex].GetComponent<TextMeshProUGUI>();
                tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f);
                currentIndex++;
            }
            yield return null;
        }
    }

    private IEnumerator DissolveAsciiRandomly(float duration)
    {
        List<GameObject> shuffledNodes = new List<GameObject>(activeAsciiNodes);
        ShuffleList(shuffledNodes);

        int totalNodes = shuffledNodes.Count;
        int nodesPerFrame = Mathf.CeilToInt(totalNodes / (duration / Time.deltaTime));

        int currentIndex = 0;
        while (currentIndex < totalNodes)
        {
            for (int i = 0; i < nodesPerFrame && currentIndex < totalNodes; i++)
            {
                TextMeshProUGUI tmp = shuffledNodes[currentIndex].GetComponent<TextMeshProUGUI>();
                tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 0f);
                currentIndex++;
            }
            yield return null;
        }
        objectPool.ReturnAllObjects(activeAsciiNodes);
    }

    private IEnumerator RevealResultAsciiAndSprite(float duration)
    {
        List<GameObject> shuffledNodes = new List<GameObject>(activeAsciiNodes);
        ShuffleList(shuffledNodes);

        int totalNodes = shuffledNodes.Count;
        int nodesPerFrame = Mathf.CeilToInt(totalNodes / (duration / Time.deltaTime));
        int halfWayPoint = totalNodes / 2;
        bool spriteFadingStarted = false;

        int currentIndex = 0;
        while (currentIndex < totalNodes)
        {
            for (int i = 0; i < nodesPerFrame && currentIndex < totalNodes; i++)
            {
                TextMeshProUGUI tmp = shuffledNodes[currentIndex].GetComponent<TextMeshProUGUI>();
                tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f);
                currentIndex++;
            }

            // 절반 이상 진행되었고, 아직 페이드인을 시작하지 않았다면
            if (currentIndex >= halfWayPoint && !spriteFadingStarted)
            {
                spriteFadingStarted = true;
                float remainingTime = duration - (currentIndex / (float)totalNodes * duration);
                
                // 결과 스프라이트 페이드인 시작
                StartCoroutine(FadeSpriteAlpha(spriteResult, 0f, 1f, remainingTime));
                
                // Digital Rain 효과 페이드인 동시 시작
                if (matrixEffectGroup != null)
                {
                    StartCoroutine(FadeCanvasGroup(matrixEffectGroup, 0f, 1f, remainingTime));
                }
            }
            yield return null;
        }
    }

    private IEnumerator FadeOutAllAscii(float duration)
    {
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float alpha = 1f - (time / duration);
            
            foreach (var node in activeAsciiNodes)
            {
                TextMeshProUGUI tmp = node.GetComponent<TextMeshProUGUI>();
                tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, alpha);
            }
            yield return null;
        }
        objectPool.ReturnAllObjects(activeAsciiNodes);
    }

    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}