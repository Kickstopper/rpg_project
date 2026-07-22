using System.Collections.Generic;
using UnityEngine;
using Data;
using System.Linq;

namespace Manager
{
    public class InventoryItem
    {
        public BaseItemData baseData;
        public int amount;

        public InventoryItem(BaseItemData baseData, int amount)
        {
            this.baseData = baseData;
            this.amount = amount;
        }
    }

    public class InventoryManager : MonoBehaviour
    {
        // 소지금
        private int money = 0;

        // 아이템 ID와 수량을 저장하는 딕셔너리
        public Dictionary<string, int> inventoryDict = new Dictionary<string, int>();

        [Header("Economy Settings")]
        [Tooltip("매월 고정적으로 나가는 기기 렌탈비")]
        public int baseRentalFee = 1000;
        [Tooltip("고용인(파트너)의 급여 PER LEVEL")]
        public int salaryPerPartner = 200;

        // (편의용) 전투 테스트를 위한 시작 아이템 리스트
        public List<ConsumableItemData> startingItems;

        public event System.Action OnMoneyChanged;

        // 보유 중인 모든 아이템 ID 반환
        public List<string> GetAllItemIds() => inventoryDict.Keys.ToList();
        
        public bool HasItem(string id) => inventoryDict.ContainsKey(id) && inventoryDict[id] > 0;

        public int GetItemCount(string id) => inventoryDict.ContainsKey(id) ? inventoryDict[id] : 0;

        public int GetMoney() => this.money;

        void InitializeInventory()
        {
            inventoryDict.Clear();
            
            // 테스트용 아이템 지급
            if (startingItems != null)
            {
                foreach(var item in startingItems)
                {
                    AddItem(item.id, 3); // 각 3개씩 지급
                }
            }
        }

        public List<ItemSaveEntry> GetSaveData()
        {
            List<ItemSaveEntry> list = new List<ItemSaveEntry>();
            foreach (var pair in inventoryDict)
            {
                list.Add(new ItemSaveEntry(pair.Key, pair.Value));
            }
            return list;
        }

        public void LoadFromSaveData(List<ItemSaveEntry> savedList)
        {
            inventoryDict.Clear();
            foreach (var entry in savedList)
            {
                if (inventoryDict.ContainsKey(entry.itemId))
                    inventoryDict[entry.itemId] = entry.count;
                else
                    inventoryDict.Add(entry.itemId, entry.count);
            }
        }

        public List<InventoryItem> GetSellableItems(ItemCategory category)
        {
            List<InventoryItem> list = new();
            foreach(var pair in inventoryDict)
            {
                BaseItemData data = ManagerRoot.Database.GetItem(pair.Key);
                if (data == null || !data.isSellable) continue;

                if (category == ItemCategory.Weapon)
                {
                    if (data is WeaponData || data is AmmoData)
                    {
                        list.Add(new InventoryItem(data, pair.Value));
                    }
                }
                else if (category == ItemCategory.Armor)
                {
                    if (data is ArmorData)
                    {
                        list.Add(new InventoryItem(data, pair.Value));
                    }
                }
                else 
                {
                    if (!(data is WeaponData || data is AmmoData || data is ArmorData))
                    {
                        list.Add(new InventoryItem(data, pair.Value));
                    }
                }
            }

            return list;
        }
        
        public void AddMoney(int money)
        {
            this.money += money;
            OnMoneyChanged?.Invoke();
        }

        public void SubMoney(int money)
        {
            this.money -= money;
            if (this.money < 0) this.money = 0;
            OnMoneyChanged?.Invoke();
        }

        public void AddItem(string id, int amount = 1)
        {
            if (inventoryDict.ContainsKey(id)) inventoryDict[id] += amount;
            else inventoryDict.Add(id, amount);
        }

        public void RemoveItem(string itemID, int quantity)
        {
            if (HasItem(itemID))
            {
                int current = inventoryDict[itemID];
                if (current > quantity)
                {
                    inventoryDict[itemID] -= quantity;
                }
                else
                {
                    inventoryDict.Remove(itemID);
                }
            }
        }

        public bool UseItem(string id)
        {
            if (HasItem(id))
            {
                inventoryDict[id]--;
                if (inventoryDict[id] <= 0) inventoryDict.Remove(id);
                return true;
            }
            return false;
        }

        public void SetMoney(int money)
        {
            this.money = money;
            OnMoneyChanged?.Invoke();
        } 
        
        public void ClearInventory()
        {
            InitializeInventory();
            money = 0;
            OnMoneyChanged?.Invoke();
        }

        // 매달 청구될 총 지출 예상 금액을 계산하여 반환
        public int CalculateMonthlyExpense()
        {
            int totalExpense = baseRentalFee;

            if (ManagerRoot.Party != null && ManagerRoot.Party.partyData != null)
            {
                int payForPartners = 0;
                
                // 플레이어(커맨더)를 제외한 순수 고용인(파트너)의 수만 계산
                foreach (var member in ManagerRoot.Party.partyData)
                {
                    if (member.isCommander || member.isMonster || !member.isRegular) continue;
                    payForPartners += (member.stats.level * salaryPerPartner);
                }
                
                totalExpense += payForPartners;
            }

            return totalExpense;
        }
    }
}