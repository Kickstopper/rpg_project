using UnityEngine;

// 속성 타입을 열거형으로 정의 (ResistanceData 필드와 매칭용)
public enum ElementType { Physical, Fire, Ice, Elec, Force, Psyche, None }
public enum UseType { All, Exploration, Battle, Passive }
public enum ItemCategory { Weapon, Armor, Etc }

// 행동 범위
public enum TargetScope 
{ 
    // --- [적 대상] ---
    Front_Single_Enemy,        // 전열 적 1명 지정
    Single_Enemy,          // 전체 적 중 1명 지정 (OneEnemy 통합)
    
    Random_Front_Enemy,        // 전열 적 랜덤 (다단 히트 등)
    Random_Enemy,          // 전체 적 랜덤 (다단 히트 등)
    
    Front_Enemies,           // 전열 적 전체 (광역기)
    All_Enemies,             // 전체 적 전체 (광역기)

    // --- [아군 대상] ---
    Self,               // 사용자 자신
    One_Ally,            // 아군 1명 (회복, 버프)
    All_Allies,          // 아군 전체 (광역 힐/버프)
    Dead_Ally,           // 죽은 아군 (부활)
    All_Dead_Allies,      // 모든 죽은 아군 (부활)
}

public enum EffectType 
{ 
    // [Atk Magic]
    Magic_Atk,

    // [Assistance Magic]
    Buff_Phys_Atk, Buff_Magic_Atk, Buff_Phys_Def, Buff_Mag_Def,             // 공격력 및 방어력 상승
    Debuff_Phys_Atk, Debuff_Magic_Atk, Debuff_Phys_Def, Debuff_Magic_Def,   // 공격력 및 방어력 하락
    Reflect_Phys, Reflect_Magic,                                            // 물리 및 마법 공격 반사
    Absorb_Phys, Absorb_Magic,                                              // 물리 및 마법 공격 흡수 
    
    // [Recover Magic]
    Recover_HP, Recover_MP,                 // HP 및 MP 상승 
    Revive_Empty, Revive_Fully,   // 부활만 시킴, HP와 MP가 최대 상태로 부활
    Recover_Bad_Status, Recover_Poison,
    Recover_Curse, Recover_Paralyze,

    // [Special]
    Special_Atk
}

namespace Data
{
    // ---------------------------------------------------------
    // [최상위 부모] 모든 데이터(아이템, 스킬, 몬스터 등)의 공통 분모
    // ---------------------------------------------------------
    public abstract class BaseRootData : ScriptableObject
    {
        [Header("Base Info")]
        public string id;              // 고유 ID (예: "S_FIREBALL", "W_SWORD_01")
        public string dataName;        // 게임 내 표시 이름
        [TextArea] public string description; // 설명
        public Sprite icon;            // 아이콘
        public EffectType effectType;
        public int effectValue; // 회복량 또는 데미지
        public ElementType element;
        public TargetScope targetScope;

        public UseType useType;

        public int actionDelay = 0; // 사용했을 때의 딜레이 (행동 속도 지연)

        [Header("Visual")]
        public GameObject effectPrefab; // 사용 시 이펙트 (옵션)
    }

    // ---------------------------------------------------------
    // [중간 부모] 상점에서 사고파는 '물건' 류 (무기, 방어구, 소모품)
    // ---------------------------------------------------------
    public abstract class BaseItemData : BaseRootData
    {
        [Header("Item Info")]
        public int price;              // 가격
        public bool isSellable = true; // 판매 가능 여부
    }
}
