using UnityEngine;

public enum WeaponType { Melee, Gun } // 무기 타입 구분
namespace Data
{
    [CreateAssetMenu(fileName = "New Weapon", menuName = "RPG/Item/Weapon")]
    public class WeaponData : BaseItemData
    {
        [Header("Weapon")]
        public WeaponType type; // 근접(Melee)인지 총(Gun)인지 설정
        public TargetScope attackRange = TargetScope.Front_Single_Enemy; // 공격 범위
        public int attackPower;       
        public int hitRateBonus;      
        
        [Header("Weapon Multi-Hit")]
        public int minHits = 1; // 최소 공격 횟수
        public int maxHits = 1; // 최대 공격 횟수 (1이면 단타, 10이면 최대 10연타)
    }
}
