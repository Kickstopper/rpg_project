using System.Collections.Generic;
using UnityEngine;
using Data;
using System.Linq;

namespace Manager
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance;

        // 아이템 ID와 수량을 저장하는 딕셔너리
        public Dictionary<string, int> inventoryDict = new Dictionary<string, int>();

        // (편의용) 전투 테스트를 위한 시작 아이템 리스트
        public List<ConsumableItemData> startingItems;
        
        private int gold;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                InitializeInventory();
            }
            else Destroy(gameObject);
        }

        void InitializeInventory()
        {
            // 테스트용 아이템 지급
            foreach(var item in startingItems)
            {
                AddItem(item.id, 3); // 각 3개씩 지급
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
        
        public void AddGold(int gold)
        {
            this.gold += gold;
        }

        public void AddItem(string id, int amount = 1)
        {
            if (inventoryDict.ContainsKey(id)) inventoryDict[id] += amount;
            else inventoryDict.Add(id, amount);
        }

        public bool UseItem(string id)
        {
            if (inventoryDict.ContainsKey(id) && inventoryDict[id] > 0)
            {
                inventoryDict[id]--;
                if (inventoryDict[id] <= 0) inventoryDict.Remove(id);
                return true;
            }
            return false;
        }

        public bool HasItem(string id) => inventoryDict.ContainsKey(id);

        public int GetItemCount(string id) => inventoryDict.ContainsKey(id) ? inventoryDict[id] : 0;

        public int GetGold() => this.gold;

        public void SetGold(int gold) => this.gold = gold;
        
        // 보유 중인 모든 아이템 ID 반환
        public List<string> GetAllItemIds() => inventoryDict.Keys.ToList();

        public void ClearInventory() => inventoryDict.Clear();
    }
}