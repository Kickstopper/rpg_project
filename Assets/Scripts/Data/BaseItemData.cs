using UnityEngine;

// 속성 타입을 열거형으로 정의 (ResistanceData 필드와 매칭용)
public enum ElementType { Physical, Fire, Ice, Elec, Force, Havoc, None }

// 행동 범위
public enum TargetScope 
{ 
    FrontSingle,        // 전열 1명 지정
    AnySingle,          // 전열/후열 1명 지정
    FrontRandom,        // 전열 랜덤 (1~N회)
    AnyRandom,          // 전체 랜덤 (1~N회)
    FrontAll,           // 전열 전체
    AnyAll,             // 적 전체
    Self,               // 자신 (버프 등)
    OneAlly,            // 아군 1명
    AllAllies,          // 아군 전체
    DeadAlly,           // 죽은 아군
    OneEnemy,           // 적 1명
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
