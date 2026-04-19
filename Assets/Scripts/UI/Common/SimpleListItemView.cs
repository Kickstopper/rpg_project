using UnityEngine;
using TMPro;

namespace UI.Common
{
    public class SimpleListItemView : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI valueText;

        public void SetData(string name, int value)
        {
            nameText.text = name;
            valueText.text = $"{value}"; 
        }

        public void SetData(string name, string valueStr)
        {
            nameText.text = name;
            valueText.text = valueStr; 
        }

        public void SetNameTextColor(Color c)
        {
            nameText.color = c;
        }

        public void SetValueTextColor(Color c)
        {
            valueText.color = c;
        }
    }
}