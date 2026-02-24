using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using Controller;
using System;
using UI;
using Manager;
using System.Collections;
using static Controller.BattleManager;

public class BattleUIController : MonoBehaviour
{
    [Header("Basic UI")]
    public GameObject raycastScreen;
    public GameObject BattleUIContainer;
    public Slider qteTimingSlider; // 타이머 슬라이더. 인스펙터에서 할당
    public Button autoModeButton; // 오토 모드의 트리거
    public BattleResultUI resultUI;
    public Image background;
    public RectTransform targetCursor; // 손가락 커서 이미지

    public Image screenFlashImage;       // 화면 번쩍임 효과용 이미지

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
    public BattleSkillUIController battleSkillUI; // 인스펙터에서 할당
    public BattleItemUIController battleItemUI; // 인스펙터에서 할당
    public GameObject commandPanel;     // 커맨드 버튼들
    
    [Header("Log & Message")]
    public GameObject logPanel;
    public TextMeshProUGUI logText;
    public GameObject messagePanel;
    public TextMeshProUGUI messageText;
    public List<Button> activeFightButtons = new List<Button>();
    public List<Button> currentMenuButtons = new List<Button>(); // 현재 화면에 표시/조작 중인 버튼 리스트
        
    public bool IsItemUIVisible => battleItemUI != null && battleItemUI.gameObject.activeSelf;
    public bool IsSkillUIVisible => battleSkillUI != null && battleSkillUI.gameObject.activeSelf;
    public bool IsCmdPanelVisible => commandPanel != null && commandPanel.activeSelf;
    // 초기화: 전투 시작 시 UI 숨김 처리 등
    public void Initialize()
    {
        if (raycastScreen == null) raycastScreen = GameStateManager.Instance.explorationCanvas;
        baseCmdContainer.SetActive(false);
        fightCmdContainer.SetActive(false);
        commandPanel.SetActive(false);
        fightSubMenuContainer.SetActive(false);
        HideLog();
        HideMessage();
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
        fightSubMenuContainer.transform.parent.transform.localPosition = new Vector3(0, posY, 0);
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


    // 공식: (버튼 개수 * 30) + 10
    private void ResizeContainer(RectTransform container, int count)
    {
        if (container != null)
        {
            float newHeight = (count * 30f) + 10f;
            container.sizeDelta = new Vector2(container.sizeDelta.x, newHeight);
        }
    }

    public void ShowBattleStartAnimation(Action onCompleteCallback)
    {
        float screenPy = 216f;
        float duration = 0.3f;

        // 초기 위치 설정
        BattleUIContainer.transform.localPosition = new Vector3(0, -screenPy, 0);
        raycastScreen.transform.localPosition = Vector3.zero;

        DOTween.Sequence()
            .Join(raycastScreen.transform.DOLocalMoveY(screenPy, duration).SetEase(Ease.OutBounce))
            .Join(BattleUIContainer.transform.DOLocalMoveY(0f, duration).SetEase(Ease.OutBounce))
            .OnComplete(() => 
            {
                // 종료 후 위치 확정 (Floating point 오차 방지)
                raycastScreen.transform.localPosition = new Vector3(0, screenPy, 0);
                BattleUIContainer.transform.localPosition = Vector3.zero;
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
        .Join(raycastScreen.transform.DOLocalMoveY(0f, duration).SetEase(Ease.OutSine))
        .Join(BattleUIContainer.transform.DOLocalMoveY(battleUIPy, duration).SetEase(Ease.OutSine))
        .OnComplete(() => 
        {
            raycastScreen.transform.localPosition = Vector3.zero;
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

    // 전투 종료 UI 표시
    public void ShowResult(BattleReward reward, List<PlayerController> partyMembers, 
                        Dictionary<PlayerController, (int oldLv, int oldExp, int oldMaxExp)> preBattleStates, 
                        Action onCloseCallback)
    {
        resultUI.Show(reward, partyMembers, preBattleStates, onCloseCallback);
    }
}