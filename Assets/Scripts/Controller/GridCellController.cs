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
        public Color corridorColor;
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
            if (wallId == -1 || (illusions.Contains(wallId))) return noWallColor;
            if (doors != null && doors.Contains(wallId)) return doorWallColor;
            return defaultWallColor;
        }

        Color GetFaceColor(int cellValue)
        {
            CellType type = (CellType)cellValue;
            return type switch
            {
                CellType.Void_Floor => noFloorColor,
                CellType.Corridor => corridorColor,
                CellType.Void_Ceil => noCeilColor,
                CellType.Weapon_Shop => shopColor,
                CellType.Armor_Shop => shopColor,
                CellType.Item_Shop => shopColor,
                CellType.Heal_Spot => shopColor,
                CellType.Terminal => terminalColor,
                CellType.Office => officeColor,
                CellType.Downstairs => dnstairsColor,
                CellType.Upstairs => upstairsColor,
                CellType.Elevator => elevatorColor,
                CellType.Entrance => entranceColor,
                _ => noCellColor
            };
        }

        private string GetText(int cellValue)
        {
            CellType type = (CellType)cellValue;
            return type switch
            {
                CellType.Weapon_Shop => "W",
                CellType.Armor_Shop => "A",
                CellType.Item_Shop => "I",
                CellType.Heal_Spot => "H",
                CellType.Terminal => "T",
                CellType.Office => "O",
                CellType.Downstairs => "▼",
                CellType.Upstairs => "▲",
                CellType.Elevator => "V",
                CellType.Entrance => "E",
                _ => string.Empty
            };
            
        }
    }
    
}

