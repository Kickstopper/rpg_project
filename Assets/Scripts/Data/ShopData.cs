using System.Collections.Generic;
using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "NewShop", menuName = "Dungeon/ShopData")]
    public class ShopData : ScriptableObject
    {
        [Header("기본 정보")]
        public string shopID;
        public string displayName;
        public List<BaseItemData> itemsForSale;
    }
}