using UnityEngine;
using Data;
using UnityEngine.UI;
namespace Controller
{
    public class GridCellController : MonoBehaviour
    {
        [Header("Wall Objects")]
        public Image leftWall;
        public Image upWall;
        public Image rightWall;
        public Image downWall;
        public Image floor;

        [Header("Wall Color")]
        public Color defaultWallColor;
        public Color defaultFloorColor;
        
        public Color noCellWallColor;
        public Color noWallColor;
        public Color noFloorColor;
        

        public void UpdateWallState(CellData data)
        {
            if (data != null && data.wallTextureIDs != null && data.wallTextureIDs.Length > 3)
            {
                int[] wallIds = data.wallTextureIDs;
                
                upWall.color = GetWallColor(wallIds[0]);
                rightWall.color = GetWallColor(wallIds[1]);
                downWall.color = GetWallColor(wallIds[2]);
                leftWall.color = GetWallColor(wallIds[3]);

                floor.color = GetFloorColor(data.value);
            }
            else
            {
                leftWall.color = noCellWallColor;
                upWall.color = noCellWallColor;
                rightWall.color = noCellWallColor;
                downWall.color = noCellWallColor;
                floor.color = noFloorColor;
            }
        }

        Color GetWallColor(int wallId)
        {
            switch(wallId)
            {
                case -1: return noWallColor;

                default: return defaultWallColor;
            }
        }

        Color GetFloorColor(int floorId)
        {
            switch(floorId)
            {
                //case 0: return noFloorColor;

                default: return defaultFloorColor;
            }
        }

    }
    
}

