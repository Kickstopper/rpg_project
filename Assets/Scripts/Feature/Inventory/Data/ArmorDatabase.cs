using RPGProject.Infrastructure.DataAccess;
using UnityEngine;

namespace RPGProject.Feature.Inventory
{
    [CreateAssetMenu(fileName = "ArmorDatabase", menuName = "Game Data/Database/Armor Database")]
    public class ArmorDatabase : BaseDatabase<ArmorData> { }
}


