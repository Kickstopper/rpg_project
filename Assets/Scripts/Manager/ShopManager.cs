using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Manager
{
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance;

        [Header("Reference Data")]
        public List<ShopData> shopDatas;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else Destroy(gameObject);
        }

        public ShopData GetShopData(string shopID)
        {
            return shopDatas.Find(shop => shop.shopID == shopID);
        }

    }
}