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
        private Color doorWallColor = Color.ghostWhite;
        
        public Color noCellWallColor;
        public Color noWallColor;
        
        [Header("Ceil & Floor Color")]
        public Color defaultFloorColor;
        private Color entranceColor = Color.darkGreen;
        public Color roomColor;
        private Color shopColor = Color.crimson;
        private Color terminalColor = Color.navyBlue;
        private Color officeColor = Color.darkBlue;
        private Color upstairsColor = Color.black;
        private Color dnstairsColor = Color.black;
        private Color elevatorColor = Color.coral;
        public Color noFloorColor;
        public Color noCeilColor;
        public Color noCellColor;

        public void UpdateWallState(CellData data, HashSet<int> illusions, HashSet<int> doors)
        {
            if (data != null && data.wallTextureIDs != null && data.wallTextureIDs.Length > 3)
            {
                int[] wallIds = data.wallTextureIDs;
                
                upWall.color = GetWallColor(wallIds[0], illusions, doors);
                rightWall.color = GetWallColor(wallIds[1], illusions, doors);
                downWall.color = GetWallColor(wallIds[2], illusions, doors);
                leftWall.color = GetWallColor(wallIds[3], illusions, doors);

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

        Color GetWallColor(int wallId, HashSet<int> illusions, HashSet<int> doors)
        {

            if (wallId == -1 || (illusions != null && illusions.Contains(wallId))) return noWallColor;
            if (doors != null && doors.Contains(wallId)) return doorWallColor;
            return defaultWallColor;
        }

        Color GetFaceColor(int cellValue)
        {
            if (cellValue == -1 || cellValue >= 99) return noFloorColor;
            if (cellValue == 0) return defaultFloorColor;
            if (cellValue == 1) return noCeilColor;
            if (cellValue >=2 && cellValue <= 5) return shopColor;
            if (cellValue == 6) return terminalColor;
            if (cellValue == 7) return officeColor;

            if (cellValue == 8) return dnstairsColor;
            if (cellValue == 9) return upstairsColor;
            if (cellValue == 10) return elevatorColor;
            
            if (cellValue == 11) return entranceColor; // 출입구
            return noCellColor;
        }

        string GetText(int cellValue)
        {
            if (cellValue == 2) return "W";
            if (cellValue == 3) return "A";
            if (cellValue == 4) return "I";
            if (cellValue == 5) return "H";
            if (cellValue == 6) return "T";
            if (cellValue == 7) return "O";

            if (cellValue == 8) return "▼";
            if (cellValue == 9) return "▲";
            if (cellValue == 10) return "V"; // 엘리베이터
            
            if (cellValue == 11) return "E"; // 출입구
            return string.Empty;
        }
    }
    
}

