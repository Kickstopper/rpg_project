using UnityEngine;

namespace Manager
{
    public class FinanceManager : MonoBehaviour
    {
        public int CurrentMoney {get; private set;}

        [Header("Pay Settings")]
        [Tooltip("이 금액 이하로 소지금이 떨어지면 게임 오버 처리됩니다.")]
        public int MaxDebtLimit {get; private set; } = -10000;
        [Tooltip("매월 고정적으로 나가는 기기 렌탈비")]
        public int DeviceRentalFee {get; private set; } = 1000;
        [Tooltip("고용인(파트너)의 급여 PER LEVEL")]
        public int SalaryPerPartner {get; private set; } = 200;

        public event System.Action OnMoneyChanged;

        
        public void AddMoney(int money)
        {
            CurrentMoney += money;
            OnMoneyChanged?.Invoke();
        }

        public void SubMoney(int money)
        {
            CurrentMoney -= money;
            //if (this.money < 0) this.money = 0;
            OnMoneyChanged?.Invoke();
        }

        public void SetMoney(int money)
        {
            CurrentMoney = money;
            OnMoneyChanged?.Invoke();
        } 
        
        public void Reset()
        {
            CurrentMoney = 0;
            OnMoneyChanged?.Invoke();
        }

        public int GetMonthlyTotalExpense()
        {
            int expense = DeviceRentalFee;
            expense += GetMonthlyPayForPartners();
            return expense;
        }

        // 오피스에서 새로운 파트너 고용 시 요구되는 10일 치 착수금 계산
        public int GetHiringAdvancePayment(int partnerLevel)
        {
            // 기본 한 달 급여 계산
            int fullMonthlySalary = partnerLevel * SalaryPerPartner;
            
            // 30일 중 10일 치 (1/3) 계산 후 반올림
            float advanceRatio = 10f / 30f; 
            return Mathf.RoundToInt(fullMonthlySalary * advanceRatio);
        }

        // 월말 정산 시 청구될 파트너의 일할 계산된 급여 반환
        public int GetMonthlyPayForPartners()
        {
            int monthlyPay = 0;

            if (ManagerRoot.Party != null && ManagerRoot.Party.partyData != null)
            {
                int payForPartners = 0;
                
                foreach (var member in ManagerRoot.Party.partyData)
                {
                    if (member.isCommander || member.isMonster || !member.isRegular) continue;
                    
                    // 해당 파트너의 100% 월급
                    int fullSalary = (member.stats.level * SalaryPerPartner);
                    
                    // 실제 일한 날짜 비율 (근무일 / 한 달 일수 30일)
                    float workRatio = (float)member.workedDays / 30f;
                    
                    // 비율을 곱해서 합산
                    payForPartners += Mathf.RoundToInt(fullSalary * workRatio);
                }
                
                monthlyPay += payForPartners;
            }

            return monthlyPay;
        }
    }
}


