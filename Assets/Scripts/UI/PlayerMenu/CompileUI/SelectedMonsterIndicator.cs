using Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerMenu
{
    public class SelectedMonsterIndicator : MonoBehaviour
    {
        public TextMeshProUGUI nameTxt;
        public TextMeshProUGUI alignTxt;
        public TextMeshProUGUI hpTxt;
        public TextMeshProUGUI mpTxt;
        public Slider hpSlider;
        public Slider mpSlider;
        
        public void SetUI(RuntimeCharacterData data)
        {
            if (data == null) return;
            if (nameTxt != null) nameTxt.text = data.name;
            if (alignTxt != null) alignTxt.text = data.align.ToString().ToUpper();
            if (hpTxt != null) hpTxt.text = data.currentHp.ToString();
            if (mpTxt != null) mpTxt.text = data.currentMp.ToString();
            if (hpSlider != null) hpSlider.value = (float)data.currentHp / data.maxHp;
            if (mpSlider != null) mpSlider.value = (float)data.currentMp / data.maxMp;
        }

        public void ResetUI()
        {
            if (nameTxt != null) nameTxt.text = "---";
            if (alignTxt != null) alignTxt.text = "---";
            if (hpTxt != null) hpTxt.text = "0";
            if (mpTxt != null) mpTxt.text = "0";
            if (hpSlider != null) hpSlider.value = 0;
            if (mpSlider != null) mpSlider.value = 0;
        }
    }
}

