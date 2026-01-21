using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public enum CommandType { Attack, Gun, Skill, Item, Move, Guard, Union_Attack, Last_Stand, Next }
    public class CommandButton : MonoBehaviour
    {
        public CommandType type; // 인스펙터에서 설정
        public Button button;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
        }
    }
    
}
