using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Data;
using System;
using TMPro;

namespace UI.DungeonMapScene
{
    public class MapTransitionManager : MonoBehaviour
    {
        [Header("Common Transition UI")]
        public CanvasGroup fadeOverlay; //

        [Header("Stair Transition UI")]
        public Image stairGraphic;      //
        public Sprite spriteUpstairs;   //
        public Sprite spriteDownstairs;
        
        [Header("Road Transition UI (Pseudo 3D)")]
        public GameObject roadContainer; 
        public Pseudo3DRoad roadScroller; 
        public Slider progressBar;
        public TextMeshProUGUI distanceText;
        public TextMeshProUGUI timeText;
        public float roadTransitionRealTime = 3.0f;

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

            // 암전
            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();

            // 계단 연출 세팅 및 재생
            stairGraphic.sprite = (stairType == StairType.Upstairs) ? spriteUpstairs : spriteDownstairs;
            stairGraphic.gameObject.SetActive(true);
            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();
            
            // 발소리 재생 등 (ManagerRoot.Sound.PlaySFX)
            yield return new WaitForSeconds(1f);

            // 다시 암전
            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();
            stairGraphic.gameObject.SetActive(false);

            // 맵 로드 로직 실행
            onMapLoadAction?.Invoke();

            // 새로운 맵 페이드 인
            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();
            fadeOverlay.blocksRaycasts = false;
        }

        // 도로 이동 연출 (던전 간 이동)
        public IEnumerator ExecuteRoadTransitionRoutine(float totalDistance, float totalGameHours, Action onMapLoadAction)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.blocksRaycasts = true;

            // 암전
            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();

            // 도로 연출 세팅
            roadContainer.SetActive(true);
            roadScroller.isMoving = true;
            progressBar.value = 0f;
            distanceText.text = $"0.0 km / {totalDistance:F1} km";
            timeText.text = "경과 시간: 0 시간";

            // 도로 보여주기 (페이드 인)
            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();

            Material roadMat = roadScroller.GetComponent<RawImage>().material;

            // 예시: 1초 동안 우회전 (커브 강도를 0.4로 서서히 변경)
            roadMat.DOFloat(0.4f, "_CurveAmount", 1.0f).SetEase(Ease.InOutSine);

            // 회전 상태 유지 대기
            yield return new WaitForSeconds(1.0f);

            // 다시 1초 동안 직진 (커브 강도를 0으로 원복)
            roadMat.DOFloat(0.0f, "_CurveAmount", 1.0f).SetEase(Ease.InOutSine);

            // 진행도 및 텍스트 애니메이션
            float progressValue = 0f;
            Tween progressTween = DOTween.To(() => progressValue, x => 
            {
                progressValue = x;
                progressBar.value = progressValue;
                
                float currentDist = Mathf.Lerp(0, totalDistance, progressValue);
                float currentHours = Mathf.Lerp(0, totalGameHours, progressValue);
                
                distanceText.text = $"{currentDist:F1} km / {totalDistance:F1} km";
                timeText.text = $"경과 시간: {Mathf.FloorToInt(currentHours)} 시간";
            }, 1f, roadTransitionRealTime).SetEase(Ease.InOutSine); // 부드러운 가감속 추가

            // 트윈이 끝날 때까지 대기
            yield return progressTween.WaitForCompletion();

            // 도착 후 잠시 대기
            yield return new WaitForSeconds(0.5f);

            // 다시 암전
            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();
            
            roadScroller.isMoving = false;
            roadContainer.SetActive(false);

            // 맵 로드 로직 실행 (기존 로직 공유)
            onMapLoadAction?.Invoke();

            // 새로운 맵 페이드 인
            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();
            fadeOverlay.blocksRaycasts = false;
        }
    }
}