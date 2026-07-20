using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Data.Database.CharacterDatabase;
namespace UI.Office
{
    public class PartnerSlotUI : MonoBehaviour
    {
        public Image background;
        public Color normalColor = Color.white;
        public Color selectColor = Color.gold;
        public string characterID;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI feeText;

        private bool isParty;
        public bool IsParty => isParty;

        public void Deselect()
        {
            nameText.color = normalColor;
        }
        
        public void Select()
        {
            nameText.color = selectColor;
        }

        private void SetText(string name, int level, int fee)
        {
            levelText.text = $"LV {level}";
            nameText.text = name;
            feeText.text = fee.ToString();
        }

        public void Setup(CharacterEntry entry, bool isCurrentlyInParty)
        {
            isParty = isCurrentlyInParty;

            characterID = entry.id;
            int level = entry.stats.level;
            int fee = level * 100;
            SetText(entry.name, level, fee);
            if (isCurrentlyInParty) Select();
            else Deselect();
            
        } 
    }
}

