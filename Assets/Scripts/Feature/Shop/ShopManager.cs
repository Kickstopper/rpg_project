using System.Collections.Generic;
using UnityEngine;

namespace RPGProject.Feature.Shop
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