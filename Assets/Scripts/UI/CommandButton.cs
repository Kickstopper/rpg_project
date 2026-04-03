using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public enum ActionType { 
        Attack, 
        Shoot, Reload, 
        Skill, 
        Item, Move, Guard, Next,
        Menu_Gun,     // Gun ▶ (Shoot, Reload)
        Menu_Extra,   // Extra ▶ (Item, Move, Guard, Next)
        Menu_Tactics,  // Tactics ▶ (Union, Last_Stand)
        Union_Attack, Last_Stand, Rolling_Vulcan, 
        Penetration, Power_Charge, Anima, 
        Burner, Freezer, Stunner, Pulser,
        Talk,
    }
    public class CommandButton : MonoBehaviour
    {
        public ActionType type; // 인스펙터에서 설정
        public Button button;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
        }
    }
    
}
