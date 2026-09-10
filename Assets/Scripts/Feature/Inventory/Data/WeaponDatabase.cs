using RPGProject.Infrastructure.DataAccess;
using UnityEngine;

namespace RPGProject.Feature.Inventory
{
    [CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Game Data/Database/Weapon Database")]
    public class WeaponDatabase : BaseDatabase<WeaponData> { }
}