using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Data;
using Manager;
using Helper;
namespace Controller
{
    public class ResultMemberSlot : MonoBehaviour
    {
        [Header("UI References")]
        public Image portraitImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI levelText;
        public Slider expSlider;
        public TextMeshProUGUI expGainText; // "+ 500 EXP" 표시

        // 레벨업 연출용
        public GameObject levelUpEffect; // 레벨업 시 켜질 텍스트나 이펙트

        public void Setup(PlayerController pc, int earnedExp, int oldLevel, int oldExp, int oldMaxExp)
        {
            if (pc.portraitImage != null) portraitImage.sprite = pc.portraitImage.sprite;
            nameText.text = pc.entityName;
            expGainText.text = $"+ {earnedExp} EXP";
            levelUpEffect.SetActive(false);

            levelText.text = $"Lv.{oldLevel}";
            
            expSlider.value = (float)oldExp / oldMaxExp;

            // 경험치 바 애니메이션 실행
            StartCoroutine(AnimateExpBar(pc, earnedExp, oldLevel, oldExp, oldMaxExp));
        }

        private System.Collections.IEnumerator AnimateExpBar(PlayerController pc, int earnedExp, int startLevel, int startExp, int startMaxExp)
        {
            int currentSimLevel = startLevel;
            int currentSimExp = startExp;
            int currentSimMaxExp = startMaxExp;
            int remainingEarnedExp = earnedExp;

            yield return new WaitForSeconds(0.5f);

            while (remainingEarnedExp > 0)
            {
                int expToNextLevel = currentSimMaxExp - currentSimExp;
                int expToAdd = Mathf.Min(remainingEarnedExp, expToNextLevel);

                // 슬라이더 애니메이션
                float targetRatio = (float)(currentSimExp + expToAdd) / currentSimMaxExp;
                yield return expSlider.DOValue(targetRatio, 0.5f).SetEase(Ease.OutQuad).WaitForCompletion();

                remainingEarnedExp -= expToAdd;
                currentSimExp += expToAdd;

                // 레벨업 체크
                if (currentSimExp >= currentSimMaxExp)
                {
                    currentSimLevel++;
                    levelText.text = $"Lv.{currentSimLevel}";
                    levelUpEffect.SetActive(true);
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                    
                    expSlider.value = 0;
                    currentSimExp = 0;
                    
                    currentSimMaxExp = BattleCalculator.GetMaxExpForLevel(currentSimLevel); 
                    
                    yield return new WaitForSeconds(0.2f);
                }
            }

            levelText.text = $"Lv.{pc.sourceData.stats.level}";
            float finalRatio = (float)pc.sourceData.currentExp / BattleCalculator.GetMaxExpForLevel(pc.sourceData.stats.level);
            expSlider.value = finalRatio;
        }
    }
}
