using UnityEngine;
using System.Collections;
using Data;
using UnityEngine.UI;

namespace UI
{
    public class ElevatorUIManager : MonoBehaviour
    {
        public static ElevatorUIManager Instance;
        
        [Header("Visual")]
        public Image characterImg; // 인스펙터에서 설정

        public bool IsSelectionComplete { get; private set; }
        public bool IsAnimationFinished { get; private set; }
        public FloorData SelectedFloor { get; private set; }

        private void Awake() 
        {
            Instance = this;
            gameObject.SetActive(false); 
        }

        // 엘리베이터 UI 열기
        public void OpenElevator(ElevatorData elevatorData)
        {
            gameObject.SetActive(true);
            IsSelectionComplete = false;
            IsAnimationFinished = false;

            // TODO: 버튼 생성 
            // elevatorData.floorData 배열을 순회하여 버튼들을 생성하고
            // 각 버튼의 onClick 이벤트에 해당 층 데이터를 세팅
        }

        // 플레이어가 특정 층 버튼을 클릭했을 때 호출
        public void SelectFloor(FloorData floor)
        {
            SelectedFloor = floor;
            IsSelectionComplete = true;

            // TODO: 버튼들을 숨기고, 엘리베이터 문이 닫히거나 층수가 올라가는 애니메이션 재생
            StartCoroutine(ElevatorMovingAnimation());
        }

        private IEnumerator ElevatorMovingAnimation()
        {
            // SoundManager.Instance.PlaySFX(SfxID.Elevator_Move);
            
            yield return new WaitForSeconds(2.0f);

            IsAnimationFinished = true;
        }

        // UI 닫기
        public void CloseElevator()
        {
            gameObject.SetActive(false);
        }
    }
}
