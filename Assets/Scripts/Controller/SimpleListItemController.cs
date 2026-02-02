using UnityEngine;
using TMPro;

namespace Controller
{
    public class SimpleListItemController : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI costText;

        public void SetData(string name, int cost)
        {
            nameText.text = name;
            costText.text = $"{cost}"; 
        }
    }
}