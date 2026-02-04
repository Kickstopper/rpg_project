using Data;
using TMPro;
using UnityEngine;
namespace Controller
{
    public class ItemInfoController : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descriptionText;
        public TextMeshProUGUI targetScopeText;

        public void UpdateInfo(BaseItemData item)
        {
            if (item == null) 
            {
                ResetText(); 
                return;
            }

            if (nameText) nameText.text = item.dataName;
            if (descriptionText)
            {
                EffectType fxType = item.effectType;
                int value = item.effectValue;
                string fxTypeStr = "EFFECT: " + fxType.ToString().ToUpper().Replace("_", " ");
                string valueStr = "VALUE: " + value.ToString(); 
                descriptionText.text = fxTypeStr +"\n" + valueStr; 
            }
            if (targetScopeText) targetScopeText.text = "TARGET: " + item.targetScope.ToString().ToUpper().Replace("_", " ");
        }

        public void ResetText()
        {
            if (nameText) nameText.text = string.Empty;
            if (descriptionText) descriptionText.text = string.Empty;
            if (targetScopeText) targetScopeText.text = string.Empty;; 
        }
        
    }
}
