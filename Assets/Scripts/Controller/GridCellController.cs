using UnityEngine;
using Data;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
namespace Controller
{
    public class GridCellController : MonoBehaviour
    {
        [Header("Wall Objects")]
        public Image leftWall;
        public Image upWall;
        public Image rightWall;
        public Image downWall;
        public Image face;
        public TextMeshProUGUI text; 

        [Header("Wall Color")]
        public Color defaultWallColor;
        
        public Color noCellWallColor;
        public Color noWallColor;
        
        [Header("Ceil & Floor Color")]
        public Color defaultFloorColor;
        public Color roomColor;
        public Color noFloorColor;
        public Color noCeilColor;
        public Color noCellColor;

        public void UpdateWallState(CellData data, HashSet<int> illusions)
        {
            if (data != null && data.wallTextureIDs != null && data.wallTextureIDs.Length > 3)
            {
                int[] wallIds = data.wallTextureIDs;
                
                upWall.color = GetWallColor(wallIds[0], illusions);
                rightWall.color = GetWallColor(wallIds[1], illusions);
                downWall.color = GetWallColor(wallIds[2], illusions);
                leftWall.color = GetWallColor(wallIds[3], illusions);

                face.color = GetFaceColor(data.value);
                text.text = GetText(data.value);
            }
            else
            {
                leftWall.color = noCellWallColor;
                upWall.color = noCellWallColor;
                rightWall.color = noCellWallColor;
                downWall.color = noCellWallColor;
                face.color = noFloorColor;
                text.text = string.Empty;
            }
        }

        Color GetWallColor(int wallId, HashSet<int> illusions)
        {

            if (wallId == -1 || (illusions != null && illusions.Contains(wallId))) return noWallColor;

            return defaultWallColor;
        }

        Color GetFaceColor(int cellValue)
        {
            if (cellValue == -1 || cellValue >= 99) return noFloorColor;
            if (cellValue == 0) return defaultFloorColor;
            if (cellValue == 1) return noCeilColor;
            if (cellValue == 2) return roomColor;

            return noCellColor;
        }

        string GetText(int cellValue)
        {
            if (cellValue == 2) return "W";
            if (cellValue == 3) return "A";
            if (cellValue == 4) return "I";
            if (cellValue == 5) return "H";
            if (cellValue == 6) return "L";
            if (cellValue == 7) return "T";
            return string.Empty;
        }
    }
    
}

