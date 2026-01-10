using UnityEngine;
namespace UI.MapGeneratorScene
{
    public class Cell : MonoBehaviour
    {
        [HideInInspector] public Sprite[] icons;
        public int typeIdx = 0;
        public string typeName; // 저장될 셀의 타입 

        void OnMouseOver()
        {
            var renderer = GetComponent<SpriteRenderer>();
            renderer.color = Color.cyan;
        }

        void OnMouseExit()
        {
            var renderer = GetComponent<SpriteRenderer>();
            renderer.color = Color.white;
        }

        void OnMouseDown()
        {
            typeIdx = (typeIdx + 1) % icons.Length;
            SetIcon();
        }

        private void SetIcon()
        {
            Sprite s = icons[typeIdx];
            GetComponent<SpriteRenderer>().sprite = s;
        }

        public void SetType(int type)
        {
            typeIdx = type;
            SetIcon();
        }
    }

}
