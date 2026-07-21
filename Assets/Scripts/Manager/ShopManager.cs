using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Manager
{
    public class ShopManager : MonoBehaviour
    {
        [Header("Reference Data")]
        public List<ShopData> shopDatas;

        public ShopData GetShopData(string shopID)
        {
            return shopDatas.Find(shop => shop.shopID == shopID);
        }

    }
}