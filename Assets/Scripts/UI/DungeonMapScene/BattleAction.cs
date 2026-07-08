using UnityEngine;
using Data;
namespace UI.DungeonMapScene
{
    [System.Serializable]
    public class BattleAction
    {
        public GameObject actor;    // 행동하는 사람 (아군 or 적)
        public GameObject target;   // 맞는 사람
        
        public ActionType type;

        // 스킬이나 아이템일 경우 상세 데이터
        public BaseRootData actionData;

        public int speed; // 행동 속도 (AGI + 스킬 보정치 + 랜덤 변수)

        // 생성자 (편의용)
        public BattleAction(GameObject _actor, GameObject _target, ActionType _type, int _speed)
        {
            actor = _actor;
            target = _target;
            type = _type;
            speed = _speed;
        }
    }
}