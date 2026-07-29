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
        [Header("Transition UI")]
        public CanvasGroup fadeOverlay;
        public Image stairGraphic;     
        public Sprite spriteUpstairs;  
        public Sprite spriteDownstairs;
        
        public void SetSprites(Sprite upstair, Sprite downstair)
        {
            spriteUpstairs = upstair;
            spriteDownstairs = downstair;
        }

        public IEnumerator ExecuteStairTransitionRoutine(StairType stairType, Action onMapLoadAction)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.blocksRaycasts = true;

            // 암전
            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();

            // 계단 이미지 세팅
            stairGraphic.sprite = (stairType == StairType.Upstairs) ? spriteUpstairs : spriteDownstairs;
            stairGraphic.gameObject.SetActive(true);

            // 계단 보여주기 (페이드 인)
            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();

            // 발소리 재생
            // ManagerRoot.Sound.PlaySFX(SfxID.FootstepStairs);
            
            // 계단 감상 대기
            yield return new WaitForSeconds(1f);

            // 다시 암전
            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();
            stairGraphic.gameObject.SetActive(false);

            // 완전히 암전된 틈을 타서 RaycastingController가 넘겨준 맵 로드 로직 실행
            onMapLoadAction?.Invoke();

            // 새로운 맵 페이드 인
            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();
            fadeOverlay.blocksRaycasts = false;
        }
    }
}
