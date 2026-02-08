using UnityEngine;
using TMPro;

namespace Controller
{
    public class SimpleListItemController : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI valueText;

        public void SetData(string name, int value)
        {
            nameText.text = name;
            valueText.text = $"{value}"; 
        }
    }
}