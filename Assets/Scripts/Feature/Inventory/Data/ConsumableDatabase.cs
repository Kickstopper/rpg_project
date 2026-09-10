using RPGProject.Infrastructure.DataAccess;
using UnityEngine;

namespace RPGProject.Feature.Inventory
{
    [CreateAssetMenu(fileName = "ConsumableDatabase", menuName = "Game Data/Database/Consumable Database")]
    public class ConsumableDatabase : BaseDatabase<ConsumableItemData> { }
}