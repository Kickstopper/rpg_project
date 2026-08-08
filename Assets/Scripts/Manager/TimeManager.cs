using System;
using UnityEngine;
using Data;

namespace Manager
{
    public class TimeManager : MonoBehaviour
    {
        [Header("Time Settings")]
        [Tooltip("하루가 지나기 위해 필요한 걸음 수")]
        public int stepsPerDay = 100; 
        
        [Tooltip("한 달을 며칠로 계산할 것인지 (정산 주기)")]
        public int daysPerMonth = 30; 

        // 현재 시간 데이터
        public int Year { get; private set; } = 1;
        public int Month { get; private set; } = 1;
        public int Day { get; private set; } = 1;
        public int CurrentSteps { get; private set; } = 0;

        // 이벤트 알림
        
        // 걸음 수가 변하거나 날짜가 바뀌어 달력 UI를 갱신해야 할 때 호출
        public event Action OnTimeUpdated;
        
        // 달이 바뀌어 디바이스 렌탈비 및 급여를 정산해야 할 때 호출
        public event Action OnPayday; 

        // 던전에서 1칸 이동할 때마다 RaycastingController에서 호출
        public void AddStep(int steps = 1)
        {
            CurrentSteps += steps;

            // 걸음 수가 하루 기준치를 넘었는지 확인
            if (CurrentSteps >= stepsPerDay)
            {
                int daysPassed = CurrentSteps / stepsPerDay;
                CurrentSteps %= stepsPerDay; // 나머지 걸음 수 보존

                // 한 번의 이동으로 여러 날이 지날 경우를 대비해 반복문 처리
                for (int i = 0; i < daysPassed; i++)
                {
                    AdvanceDay();
                }
            }
            
            // 걸음 수가 올랐으므로 UI 갱신 이벤트 발생
            OnTimeUpdated?.Invoke();
        }

        private void AdvanceDay()
        {
            Day++;

            // 하루가 지났으므로 정규 파트너들의 workedDays 1 증가
            if (ManagerRoot.Party != null && ManagerRoot.Party.partyData != null)
            {
                foreach (var member in ManagerRoot.Party.partyData)
                {
                    // 커맨더(플레이어)와 몬스터를 제외한 정규 파트너만 조건에 포함
                    if (!member.isCommander && !member.isMonster && member.isRegular)
                    {
                        member.workedDays++;
                    }
                }
            }

            // 설정한 daysPerMonth이 넘어가면 다음 달 1일로 변경
            if (Day > daysPerMonth)
            {
                Day = 1;
                Month++;

                if (Month > 12)
                {
                    Month = 1;
                    Year++;
                }

                Debug.Log($"[TimeManager] {Year}년 {Month}월이 되었습니다. 월간 정산을 시작합니다!");
                
                // 달이 바뀌었으므로 정산 이벤트(급여, 렌탈비 차감) 호출
                OnPayday?.Invoke();
            }
        }

        // 데이터 저장
        public void Save(SaveData data)
        {
            if (data.timeProgress == null) 
            {
                data.timeProgress = new TimeProgress();
            }

            // 현재 TimeManager의 상태를 SaveData 객체로 복사
            data.timeProgress.year = this.Year;
            data.timeProgress.month = this.Month;
            data.timeProgress.day = this.Day;
            data.timeProgress.currentSteps = this.CurrentSteps;

            Debug.Log($"[TimeManager] 시간 저장 완료: {Year}년 {Month}월 {Day}일 ({CurrentSteps}보 진행 중)");
        }

        // 데이터 불러오기
        public void Load(SaveData data)
        {
            if (data.timeProgress != null)
            {
                // 세이브 파일의 데이터를 덮어씌움 (최소 1년 1월 1일 보장)
                this.Year = Mathf.Max(1, data.timeProgress.year);
                this.Month = Mathf.Max(1, data.timeProgress.month);
                this.Day = Mathf.Max(1, data.timeProgress.day);
                this.CurrentSteps = Mathf.Max(0, data.timeProgress.currentSteps);
            }
            else
            {
                // 세이브 데이터가 없거나 처음 시작하는 경우 기본값 초기화
                this.Year = 1;
                this.Month = 1;
                this.Day = 1;
                this.CurrentSteps = 0;
            }

            Debug.Log($"[TimeManager] 시간 로드 완료: {Year}년 {Month}월 {Day}일 ({CurrentSteps}보 진행 중)");

            // 데이터를 성공적으로 불러온 후, 달력 UI 등이 알맞은 날짜로 갱신되도록 이벤트 호출
            OnTimeUpdated?.Invoke();
        }
    }
}