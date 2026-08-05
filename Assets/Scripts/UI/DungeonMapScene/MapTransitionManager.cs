using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Data;
using System;

namespace UI.DungeonMapScene
{
    public class MapTransitionManager : MonoBehaviour
    {
        [Header("Common Transition UI")]
        public CanvasGroup fadeOverlay;

        [Header("Stair Transition UI")]
        public Image stairGraphic;      
        public Sprite spriteUpstairs;   
        public Sprite spriteDownstairs;

        public void SetSprites(Sprite upstair, Sprite downstair)
        {
            spriteUpstairs = upstair;
            spriteDownstairs = downstair;
        }

        // 계단 이동 연출 (층간 이동)
        public IEnumerator ExecuteStairTransitionRoutine(StairType stairType, Action onMapLoadAction)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.blocksRaycasts = true;

            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();

            stairGraphic.sprite = (stairType == StairType.Upstairs) ? spriteUpstairs : spriteDownstairs;
            stairGraphic.gameObject.SetActive(true);
            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();
            
            yield return YieldCache.WaitForSeconds(1f);

            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();
            stairGraphic.gameObject.SetActive(false);

            onMapLoadAction?.Invoke();

            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();
            fadeOverlay.blocksRaycasts = false;
        }
    }
}