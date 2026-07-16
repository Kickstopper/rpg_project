using System.Collections.Generic;
using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "NewShop", menuName = "Dungeon/ShopData")]
    public class ShopData : ScriptableObject
    {
        [Header("기본 정보")]
        public string shopID;
        public BgmID bgmID;
        public Sprite BackgroundImage;
        public string displayName;
        public List<BaseItemData> itemsForSale;
        
        [Header("상인 정보")]
        public Sprite characterImage;
        public string characterName;
        public string startMessage;
        public string endMessage;
        public string buyMessage;
        public string sellMessage;
        public string equipMessage;
        public string cancelMessage;
    }
}