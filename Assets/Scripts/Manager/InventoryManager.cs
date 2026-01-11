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
        public Dictionary<string, int> inventory = new Dictionary<string, int>();

        // (편의용) 전투 테스트를 위한 시작 아이템 리스트
        public List<ConsumableItemData> startingItems;

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

        public void AddItem(string id, int amount)
        {
            if (inventory.ContainsKey(id)) inventory[id] += amount;
            else inventory.Add(id, amount);
        }

        public bool UseItem(string id)
        {
            if (inventory.ContainsKey(id) && inventory[id] > 0)
            {
                inventory[id]--;
                if (inventory[id] <= 0) inventory.Remove(id);
                return true;
            }
            return false;
        }

        public bool HasItem(string id) => inventory.ContainsKey(id);

        public int GetItemCount(string id) => inventory.ContainsKey(id) ? inventory[id] : 0;
        
        // 보유 중인 모든 아이템 ID 반환
        public List<string> GetAllItemIds() => inventory.Keys.ToList();
    }
}