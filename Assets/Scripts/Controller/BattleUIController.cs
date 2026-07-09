using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using Controller;
using System;
using UI;
using System.Collections;
using static Controller.BattleManager;

public class BattleUIController : MonoBehaviour
{
    [Header("Basic UI")]
    public RectTransform backgroundContainer;
    public RectTransform monsterContainer;
    public Transform battleUIContainer;
    public GameObject phaseIndicator;
    public TextMeshProUGUI phaseIndicatorText;
    public SimpleGradient phaseIndicatorBg;

    public GameObject breakSliderContainer;
    public Slider enemyBreakSlider;
    public Slider partyBreakSlider;
    public Slider qteTimingSlider; // 타이머 슬라이더. 인스펙터에서 할당
    public Button autoModeButton; // 오토 모드의 트리거
    public BattleResultUI resultUI;
    public Image background;
    public RectTransform targetCursor; // 손가락 커서 이미지
    public Image screenFlashImage;       // 화면 번쩍임 효과용 이미지

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public Image gameOverFadeOverlay;     // 전체 패널 페이드인 용도
    public Image gameOverMonsterImage;          // 몬스터 이미지
    public TextMeshProUGUI gameOverMessageText; // 조롱 메시지 텍스트
    public TextMeshProUGUI gameOverTitleText;

    [Header("Instant Win Settings")]
    public GameObject instantResultPanel; // 결과 표시 패널
    public TextMeshProUGUI instantResultText; // 결과 텍스트
    public float instantWinDelay = 1.5f; // 결과 표시 후 대기 시간
    
    [Header("Command UI")]
    public GameObject baseCmdContainer;   
    public GameObject fightCmdContainer;
    public RectTransform fightBtnContainer; //fightCmdContainer의 버튼이 붙는 트랜스폼 (Inspector 할당)
    public GameObject fightSubMenuContainer; // 서브 메뉴 패널 오브젝트 (Inspector 할당)
    public List<Button> baseButtons; 
    public List<CommandButton> allFightButtons;
    public BattleSkillUIController battleSkillUI;
    public BattleItemUIController battleItemUI;
    public GameObject commandPanel;     // 커맨드 버튼들
    
    [Header("Log & Message")]
    public GameObject statePanel;
    public TextMeshProUGUI stateText;
    public GameObject logPanel;
    public TextMeshProUGUI logText;
    public GameObject messagePanel;
    public TextMeshProUGUI messageText;
    public List<Button> activeFightButtons = new List<Button>();
    public List<Button> currentMenuButtons = new List<Button>(); // 현재 화면에 표시/조작 중인 버튼 리스트
        
    public bool IsItemUIVisible => battleItemUI != null && battleItemUI.gameObject.activeSelf;
    public bool IsSkillUIVisible => battleSkillUI != null && battleSkillUI.gameObject.activeSelf;
    public bool IsCmdPanelVisible => commandPanel != null && commandPanel.activeSelf;

    private float targetPartyGauge = 0f;
    private float targetEnemyGauge = 0f;

    [Range(0f, 1f)]
    public float bgParallaxRatio = 0.4f;

    private Coroutine zoomCoroutine;
    private Coroutine monsterAnimCoroutine; // 게임 오버 시에 표시되는 몬스터의 애니메이션 코루틴 추적용 변수

    //  BattleEntity 사망 시의 확대 관련 변수들
    private Vector3 defaultBgPos;
    private Vector3 defaultBgScale;
    private Vector3 defaultMonsterPos;
    private Vector3 defaultMonsterScale;
    private bool isZoomInitialized = false;

    public void Initialize()
    {
        baseCmdContainer.SetActive(false);
        fightCmdContainer.SetActive(false);
        commandPanel.SetActive(false);
        fightSubMenuContainer.SetActive(false);
        HideStateMessage();
        HideLog();
        HideMessage();
        ResetPartyGauge();
        ResetEnemyGauge();
    }

    public void ShowQTESlider()
    {
        qteTimingSlider.gameObject.SetActive(true);
        qteTimingSlider.minValue = 0f;
        qteTimingSlider.maxValue = 1.0f;
        qteTimingSlider.value = 1.0f;
        qteTimingSlider.interactable = false;
    }

    public void UpdateQTESliderValue(float value)
    {
        qteTimingSlider.value = value;
    }

    public void HideQTESlider()
    {
        qteTimingSlider.gameObject.SetActive(false);
    }

    // 상태 메시지 표시
    public void ShowStateMessage(string msg)
    {
        if (statePanel) 
        {
            statePanel.SetActive(true);
            stateText.text = msg;
        }
    }

    public void HideStateMessage()
    {
        if (statePanel) 
        {
            statePanel.SetActive(false);
            stateText.text = string.Empty;
        }
    }

    // 로그 표시
    public void ShowLog(string msg)
    {
        if (logPanel) 
        {
            logPanel.SetActive(true);
            logText.text = msg;
        }
    }

    public void HideLog()
    {
        if (logPanel)
        {
            logPanel.SetActive(false);
            logText.text = string.Empty;
        }
    }

    // 파티 메시지 표시
    public void ShowMessage(string msg)
    {
        if (messagePanel)
        {
            messagePanel.SetActive(true);
            messageText.text = msg;
        }
    }

    public void HideMessage()
    {
        if (messagePanel) {
            messagePanel.SetActive(false);
            messageText.text = string.Empty;
        }
    }

    public void SetSubMenuVisible(bool isVisible)
    {
        fightSubMenuContainer.SetActive(isVisible);
        SetInteractable(fightSubMenuContainer, isVisible, isVisible);
    }

    public void HideSubMenu()
    {
        foreach (var btn in currentMenuButtons)
        {
            btn.transform.SetParent(fightBtnContainer, false);
            btn.gameObject.SetActive(false);
        }
        fightSubMenuContainer.SetActive(false);
    }

    public void SetSubMenuButtons(List<Button> subButtons, float posY)
    {
        foreach (var btn in subButtons)
        {
            btn.transform.SetParent(fightSubMenuContainer.transform, false);
            btn.gameObject.SetActive(true);
        }
        
        ResizeContainer(fightSubMenuContainer.GetComponent<RectTransform>(), subButtons.Count);

        // 서브 메뉴의 높이를 메인 메뉴의 카테고리 버튼과 일치시킴
        fightSubMenuContainer.transform.parent.transform.localPosition = new Vector3(-180f, posY, 0);
    }

    public void SetCmdPanelVisible(bool isVisible)
    {
        commandPanel.SetActive(isVisible);
    }

    public void SetBaseCmdVisible(bool isVisible)
    {
        baseCmdContainer.SetActive(isVisible);
    }

    public void SetFightCmdVisible(bool isVisible)
    {
        fightCmdContainer.SetActive(isVisible);
    }

    // UI 인터랙션 제어
    public void SetBaseCmdInteractable(bool isInteractable)
    {
        SetInteractable(baseCmdContainer, isInteractable);
    }

    public void SetFightCmdInteractable(bool isInteractable)
    {
        SetInteractable(fightCmdContainer, isInteractable);
    }

    private void SetInteractable(GameObject container, bool isInteractable, bool ignoreParent = false)
    {
        if (container == null) return;
        
        CanvasGroup group = container.GetComponent<CanvasGroup>();
        // CanvasGroup이 없으면 자동으로 추가
        if (group == null) group = container.AddComponent<CanvasGroup>();

        group.interactable = isInteractable;
        group.blocksRaycasts = isInteractable;
        group.ignoreParentGroups = ignoreParent; // 부모의 설정 무시 여부
    }

    public void SetAutoButtonVisible(bool isVisible)
    {
        autoModeButton.gameObject.SetActive(isVisible);
        autoModeButton.GetComponent<Image>().color = Color.red;
    }

    public void SetAutoButtonSelect()
    {
        autoModeButton.Select();
        autoModeButton.GetComponent<Image>().color = Color.white;
    }

    public Color GetBackgroundColor()
    {
        return background.color;
    }

    public void SetBackgroundColor(Color color)
    {
        background.color = color;
    }

    public void InitCommandButtons()
    {
        foreach (var btn in allFightButtons)
        {
            btn.transform.SetParent(fightBtnContainer, false); 
            btn.gameObject.SetActive(false);
        }

        // 기존 리스트 초기화
        allFightButtons.ForEach(btn => btn.gameObject.SetActive(false));
        activeFightButtons.Clear(); 
    }

    public void ResizeMenuButtonContainer(int count)
    {
        ResizeContainer(fightBtnContainer, count);
    }

    public void ResizeSubMenuButtonContainer(int count)
    {
        ResizeContainer(fightSubMenuContainer.GetComponent<RectTransform>(), count);
    }


    // 공식: (버튼 개수 * 60) + 10
    private void ResizeContainer(RectTransform container, int count)
    {
        if (container != null)
        {
            float newHeight = (count * 60f) + 10f;
            container.sizeDelta = new Vector2(container.sizeDelta.x, newHeight);
        }
    }

    public void AddPartyGauge(float amount)
    {
        // 슬라이더의 현재 값이 아닌, 내부 목표값에 수치를 누적
        targetPartyGauge = Mathf.Clamp01(targetPartyGauge + amount);
        
        // 애니메이션 적용
        partyBreakSlider.DOKill();
        partyBreakSlider.DOValue(targetPartyGauge, 0.3f).SetEase(Ease.OutCubic);
    }
    
    public float GetPartyGaugeValue()
    {
        return targetPartyGauge;
    }

    public void ResetPartyGauge()
    {
        targetPartyGauge = 0f;
        partyBreakSlider.DOKill();
        partyBreakSlider.value = 0f;
    }

    public void AddEnemyGauge(float amount)
    {
        targetEnemyGauge = Mathf.Clamp01(targetEnemyGauge + amount);
        
        enemyBreakSlider.DOKill();
        enemyBreakSlider.DOValue(targetEnemyGauge, 0.3f).SetEase(Ease.OutCubic);
    }

    public float GetEnemyGaugeValue()
    {
        return targetEnemyGauge;
    }

    public void ResetEnemyGauge()
    {
        targetEnemyGauge = 0f;
        enemyBreakSlider.DOKill();
        enemyBreakSlider.value = 0f;
    }

    public void SetBreakSliderVisible(bool visible)
    {
        breakSliderContainer.SetActive(visible);
    }

    public void ShowBattleStartAnimation(Action onCompleteCallback)
    {
        float screenPy = 216f;
        float duration = 0.3f;

        // 초기 위치 설정
        battleUIContainer.localPosition = new Vector3(0, -screenPy, 0);
        backgroundContainer.localPosition = Vector3.zero;

        DOTween.Sequence()
            .Join(backgroundContainer.DOLocalMoveY(screenPy, duration).SetEase(Ease.OutBounce))
            .Join(battleUIContainer.DOLocalMoveY(0f, duration).SetEase(Ease.OutBounce))
            .OnComplete(() => 
            {
                // 종료 후 위치 확정 (Floating point 오차 방지)
                backgroundContainer.localPosition = new Vector3(0, screenPy, 0);
                battleUIContainer.localPosition = Vector3.zero;
                onCompleteCallback?.Invoke();
            });
    }

    public void ShowBattleEndAnimation(Action onCompleteCallback)
    {
        HideMessage();
        HideLog();
        SetAutoButtonVisible(false);

        float duration = 0.1f;
        float battleUIPy = -216f;
        DOTween.Sequence()
        .Join(backgroundContainer.DOLocalMoveY(0f, duration).SetEase(Ease.OutSine))
        .Join(battleUIContainer.DOLocalMoveY(battleUIPy, duration).SetEase(Ease.OutSine))
        .OnComplete(() => 
        {
            backgroundContainer.localPosition = Vector3.zero;
            onCompleteCallback?.Invoke();
        });
    }

    public void ShowSkills(List<string> skillIds, PlayerController actor)
    {
        battleSkillUI.Show(skillIds, actor);
    }

    public void ShowItems()
    {
        battleItemUI.Show();
    }

    public IEnumerator ShowFlashEffect()
    {
        screenFlashImage.color = new Color(1, 1, 1, 0);
        Sequence flashSeq = DOTween.Sequence();
        flashSeq.Append(screenFlashImage.DOFade(1.0f, 0.1f));
        flashSeq.Append(screenFlashImage.DOFade(0.0f, 0.3f));
        yield return flashSeq.WaitForCompletion();
    }

    public IEnumerator ShowInstantWinPanel(BattleReward reward)
    {
        instantResultPanel.SetActive(true);
        string itemTxt = reward.dropItems.Count > 0 ? $"\nDrops: {reward.dropItems.Count}" : "";
        
        instantResultText.text = $"<size=120%>YOU는 SHOCK!!</size>\n\n" +
                                    $"EXP +{reward.totalExp}\nGOLD +{reward.totalMoney}{itemTxt}\n" +
                                    $"적들은 이미 죽어 있다...";
        
        Tween tween = instantResultPanel.transform.DOScale(1.1f, instantWinDelay);
        yield return tween.WaitForCompletion();
    }

    public IEnumerator ShowPhaseIndicator(bool isEnemyTurn)
    {
        // 1. 텍스트 설정 (캐싱된 변수 사용)
        if (phaseIndicatorText != null)
        {
            phaseIndicatorText.text = isEnemyTurn ? "ENEMY PHASE" : "PLAYER PHASE";
            //phaseIndicatorText.color = isEnemyTurn ? Color.softRed : Color.softBlue;
        }
        if (phaseIndicatorBg != null)
        {
            phaseIndicatorBg.colorRight = isEnemyTurn ? Color.darkRed : Color.darkBlue;
        }

        RectTransform rectT = phaseIndicator.transform as RectTransform;
        
        rectT.DOKill();

        RectTransform parentRect = rectT.parent as RectTransform;
        float offScreenX = (parentRect.rect.width / 2f) + (rectT.rect.width / 2f);

        // 왼쪽 화면 밖으로 위치 초기화
        rectT.anchoredPosition = new Vector2(-offScreenX, rectT.anchoredPosition.y);

        var seq = DOTween.Sequence();
        float moveDuration = 0.3f;

        // 가운데로 날아오기
        seq.Append(rectT.DOAnchorPosX(0, moveDuration).SetEase(Ease.OutCubic).OnStart(()=> {
            phaseIndicator.SetActive(true);
        }));

        // 0.5초 대기 후 오른쪽 화면 밖으로 날아가기
        seq.Append(rectT.DOAnchorPosX(offScreenX, moveDuration).SetDelay(0.5f).SetEase(Ease.InCubic).OnComplete(()=> {
            phaseIndicator.SetActive(false);
        }));

        yield return seq.WaitForCompletion();
    }

    public void HideInstantWinPanel()
    {
        instantResultPanel.transform.localScale = Vector3.one;
        instantResultPanel.SetActive(false);
    }

    public void SetTargetCursorVisible(bool isVisible)
    {
        if (targetCursor) targetCursor.gameObject.SetActive(isVisible);
    }

    public void SetTargetCursorPosition(Vector3 pos)
    {
        if (targetCursor) targetCursor.position = pos;
    }

    public IEnumerator UIZoomRoutine(Transform target, float zoomScale, float zoomInTime, float holdTime, float zoomOutTime)
    {
        if (backgroundContainer == null || monsterContainer == null || target == null) yield break;

        if (!isZoomInitialized)
        {
            defaultBgPos = backgroundContainer.localPosition;
            defaultBgScale = backgroundContainer.localScale;
            
            defaultMonsterPos = monsterContainer.localPosition;
            defaultMonsterScale = monsterContainer.localScale;
            
            isZoomInitialized = true;
        }

        backgroundContainer.DOKill();
        monsterContainer.DOKill();

        Vector3 targetLocalPos = monsterContainer.InverseTransformPoint(target.position);
        targetLocalPos = new Vector3(
            targetLocalPos.x * monsterContainer.localScale.x,
            targetLocalPos.y * monsterContainer.localScale.y,
            targetLocalPos.z * monsterContainer.localScale.z
        );

        Vector3 monsterMoveOffset = -targetLocalPos * (zoomScale - 1f) * 0.7f; 
        Vector3 monsterTargetPos = defaultMonsterPos + monsterMoveOffset;
        float monsterTargetScale = zoomScale;

        Vector3 bgTargetPos = defaultBgPos + (monsterMoveOffset * bgParallaxRatio);
        float bgTargetScale = 1f + ((zoomScale - 1f) * bgParallaxRatio);
        
        Sequence zoomInSeq = DOTween.Sequence().SetUpdate(true); // .SetUpdate(true)를 붙이면 Time.timeScale이 0.1이 되어도 애니메이션은 원래 속도로 재생됨
    
        zoomInSeq.Join(monsterContainer.DOLocalMove(monsterTargetPos, zoomInTime).SetEase(Ease.OutCubic));
        zoomInSeq.Join(monsterContainer.DOScale(monsterTargetScale, zoomInTime).SetEase(Ease.OutCubic));
        zoomInSeq.Join(backgroundContainer.DOLocalMove(bgTargetPos, zoomInTime).SetEase(Ease.OutCubic));
        zoomInSeq.Join(backgroundContainer.DOScale(bgTargetScale, zoomInTime).SetEase(Ease.OutCubic));

        yield return zoomInSeq.WaitForCompletion();
        
        // 원상 복구
        Sequence zoomOutSeq = DOTween.Sequence().SetUpdate(true);
        // 슬로우 모션 연출 유지 시간. 정확한 시간을 대기하도록 Realtime 함수를 사용
        if (holdTime > 0) yield return new WaitForSecondsRealtime(holdTime);

        zoomOutSeq.Join(monsterContainer.DOLocalMove(defaultMonsterPos, zoomOutTime).SetEase(Ease.InOutQuad));
        zoomOutSeq.Join(monsterContainer.DOScale(defaultMonsterScale, zoomOutTime).SetEase(Ease.InOutQuad));
        zoomOutSeq.Join(backgroundContainer.DOLocalMove(defaultBgPos, zoomOutTime).SetEase(Ease.InOutQuad));
        zoomOutSeq.Join(backgroundContainer.DOScale(defaultBgScale, zoomOutTime).SetEase(Ease.InOutQuad));

        yield return zoomOutSeq.WaitForCompletion();
    }

    // 줌 코루틴을 중단
    public void StopZoomCoroutine()
    {
        if (zoomCoroutine != null)
        {
            StopCoroutine(zoomCoroutine);
            zoomCoroutine = null;
        }
    }

    // 외부에서 코루틴을 시작하고 추적하기 위한 래퍼 함수
    public void StartZoomEffect(Transform target, float zoomScale, float zoomInTime, float holdTime, float zoomOutTime)
    {
        StopZoomCoroutine();
        zoomCoroutine = StartCoroutine(UIZoomRoutine(target, zoomScale, zoomInTime, holdTime, zoomOutTime));
    }

    // 전투 종료 UI 표시
    public void ShowResult(BattleReward reward, List<PlayerController> partyMembers, 
                        Dictionary<PlayerController, (int oldLv, int oldExp, int oldMaxExp)> preBattleStates, 
                        Action onCloseCallback)
    {
        resultUI.Show(reward, partyMembers, preBattleStates, onCloseCallback);
    }

    public IEnumerator ShowGameOverSequence(MonsterController killer, Action onComplete)
    {
        // 초기화 및 활성화
        gameOverPanel.SetActive(true);
        gameOverFadeOverlay.color = Color.black;
        gameOverMessageText.text = "";
        gameOverMessageText.maxVisibleCharacters = 0;

        if (gameOverTitleText != null) 
        {
            Color titleColor = gameOverTitleText.color;
            titleColor.a = 0f;
            gameOverTitleText.color = titleColor;
            gameOverTitleText.gameObject.SetActive(true);
        }

        // 이미지 비율 유지
        if (gameOverMonsterImage != null)
            gameOverMonsterImage.preserveAspect = true;

        // 혹시 기존에 돌고 있던 애니메이션이 있다면 안전하게 정지
        if (monsterAnimCoroutine != null)
        {
            StopCoroutine(monsterAnimCoroutine);
            monsterAnimCoroutine = null;
        }

        // 몬스터 이미지 세팅 및 애니메이션 시작
        if (killer != null && killer.sourceData != null && killer.sourceData.image != null && killer.sourceData.image.Length > 0)
        {
            gameOverMonsterImage.sprite = killer.sourceData.image[0];
            gameOverMonsterImage.color = new Color(1, 1, 1, 0); // 투명하게 시작
            gameOverMonsterImage.gameObject.SetActive(true);

            // 이미지가 2장 이상일 경우에만 애니메이션 코루틴 실행
            if (killer.sourceData.image.Length > 1)
            {
                monsterAnimCoroutine = StartCoroutine(AnimateMonsterSprite(killer.sourceData));
            }
        }
        else
        {
            gameOverMonsterImage.gameObject.SetActive(false);
        }

        // 패널 전체 페이드 인
        yield return gameOverFadeOverlay.DOFade(0f, 1f).WaitForCompletion();

        // 마지막 타격을 가한 몬스터 이미지 페이드 인
        if (gameOverMonsterImage.gameObject.activeSelf)
        {
            yield return gameOverMonsterImage.DOFade(1f, 1.5f).WaitForCompletion();
        }

        // 메시지 타이핑 애니메이션
        string msg = "건방진 녀석. 날 이기려면 백 년은 멀었다."; 
        if (killer != null && killer.sourceData != null && !string.IsNullOrEmpty(killer.sourceData.CondolenceText))
        {
            msg = killer.sourceData.CondolenceText;
        }

        gameOverMessageText.text = msg;
        
        float typingDuration = msg.Length * 0.05f;
        yield return DOTween.To(() => gameOverMessageText.maxVisibleCharacters, 
                                x => gameOverMessageText.maxVisibleCharacters = x, 
                                msg.Length, typingDuration)
                            .SetEase(Ease.Linear)
                            .WaitForCompletion();

        if (gameOverTitleText != null)
        {
            yield return gameOverTitleText.DOFade(1f, 1.5f).SetEase(Ease.InOutQuad).WaitForCompletion();
        }
        // 연출 완료 콜백
        onComplete?.Invoke();
    }

    // 몬스터 애니메이션 무한 반복 코루틴
    private IEnumerator AnimateMonsterSprite(MonsterDatabase.MonsterEntry data)
    {
        int frameIndex = 0;
        
        // animInterval이 0이거나 너무 작을 경우 엔진의 무한루프를 방지하기 위한 최소값 세팅
        float interval = Mathf.Max(0.05f, data.animInterval); 
        WaitForSeconds wait = new WaitForSeconds(interval);

        // 게임 오버 패널과 이미지가 켜져 있는 동안만 반복 작동하도록
        while (gameOverPanel.activeSelf && gameOverMonsterImage.gameObject.activeSelf)
        {
            gameOverMonsterImage.sprite = data.image[frameIndex];
            
            // 인덱스를 1씩 증가시키되, 배열 길이에 도달하면 다시 0으로 순환시킴
            frameIndex = (frameIndex + 1) % data.image.Length;
            
            yield return wait;
        }
    }
}