using RPGProject.Infrastructure.DataAccess;
using UnityEngine;

namespace RPGProject.Feature.Inventory
{
    [CreateAssetMenu(fileName = "AmmoDatabase", menuName = "Game Data/Database/Ammo Database")]
    public class AmmoDatabase : BaseDatabase<AmmoData> { }
}