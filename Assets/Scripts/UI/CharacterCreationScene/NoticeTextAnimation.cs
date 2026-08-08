using Controller;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class NoticeTextAnimation : MonoBehaviour
{
    public GameObject noticePanel;
    public TextMeshProUGUI noticeText;
    public RetroTerminalController retroTerminal;
    void Start()
    {
        if (noticePanel != null) noticePanel.SetActive(true);
        if (noticeText != null)
        {
            noticeText.text = "이 게임에서 일어나는 모든 일들은 게임 속에서\n\n실제로 일어날 일들을 바탕으로 하고 있습니다.";
            Color color = noticeText.color;
            color.a = 0f;
            noticeText.color = color;
        }

        StartAnimation();
    }

    private void StartAnimation()
    {
        Sequence seq = DOTween.Sequence();
        seq.Append(noticeText.DOFade(1, 1f));
        seq.AppendInterval(3f);
        seq.Append(noticeText.DOFade(0, 1f));
        seq.OnComplete(() =>
        {
            noticePanel.SetActive(false);
            retroTerminal.StartAnimation();
        });
    }

}
