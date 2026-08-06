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

        // 매달 청구될 파트너의 급여를 계산하여 반환
        public int GetMonthlyPayForPartners()
        {
            int monthlyPay = 0;

            if (ManagerRoot.Party != null && ManagerRoot.Party.partyData != null)
            {
                int payForPartners = 0;
                
                // 플레이어(커맨더)를 제외한 순수 고용인(파트너)의 수만 계산
                foreach (var member in ManagerRoot.Party.partyData)
                {
                    if (member.isCommander || member.isMonster || !member.isRegular) continue;
                    payForPartners += (member.stats.level * SalaryPerPartner);
                }
                
                monthlyPay += payForPartners;
            }

            return monthlyPay;
        }
    }
}


