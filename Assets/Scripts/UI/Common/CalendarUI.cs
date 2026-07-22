using UnityEngine;
using TMPro;
using Manager;

namespace UI.Common
{
    public class CalendarUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI locationText;
        public TextMeshProUGUI dateText;
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI expenseText;

        [Header("Colors")]
        public Color normalMoneyColor = Color.gold;
        public Color debtMoneyColor = new Color(1f, 0.3f, 0.3f); // 경고색

        // [삭제됨] baseRentalFee, salaryPerPartner 변수 삭제

        private void Start()
        {
            if (ManagerRoot.Time != null) ManagerRoot.Time.OnTimeUpdated += UpdateUI;
            if (ManagerRoot.Inventory != null) ManagerRoot.Inventory.OnMoneyChanged += UpdateUI;

            UpdateUI();
        }

        private void OnDestroy()
        {
            if (ManagerRoot.Time != null) ManagerRoot.Time.OnTimeUpdated -= UpdateUI;
            if (ManagerRoot.Inventory != null) ManagerRoot.Inventory.OnMoneyChanged -= UpdateUI;
        }

        public void UpdateUI()
        {
            if (ManagerRoot.Dungeon == null || ManagerRoot.Time == null || ManagerRoot.Inventory == null) return;
            
            // 위치 정보 표시
            if (ManagerRoot.GameState.CurrentState == GameState.Exploration && ManagerRoot.Dungeon.CurrentDungeonData != null)
            {
                locationText.text = ManagerRoot.Dungeon.CurrentDungeonData.locationID;
            }
            else
            {
                locationText.text = "----";
            }
            
            // 현재 날짜 표시
            string monthStr = ManagerRoot.Time.Month.ToString("D2");
            string dayStr = ManagerRoot.Time.Day.ToString("D2");
            dateText.text = $"{monthStr}.{dayStr}";

            // 현재 소지금 표시 및 부채 확인
            int currentMoney = ManagerRoot.Inventory.GetMoney();
            moneyText.text = $"{currentMoney:N0}"; 
            moneyText.color = (currentMoney < 0) ? debtMoneyColor : normalMoneyColor;

            // 지출금 표시
            int expectedExpense = ManagerRoot.Inventory.CalculateMonthlyExpense();
            expenseText.text = $"-{expectedExpense:N0}";
        }
    }
}