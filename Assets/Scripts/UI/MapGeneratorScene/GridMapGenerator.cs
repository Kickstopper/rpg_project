using UnityEngine;
namespace UI.MapGeneratorScene
{
    public class GridMapGenerator : MonoBehaviour
    {
        [HideInInspector] public int mapWidth = 11; // 격자의 기본 너비
        [HideInInspector] public int mapHeight = 11; // 격자의 기본 높이
        [HideInInspector] public string mapName = "new_map";

        public GameObject cellPrefab; // 격자 셀 프리팹. scene에서 할당
        private Sprite[] _iconImages;
        private GameObject mapContainer;
        private float iconWidth = 1f;
        private float iconHeight = 1f;

        void Start()
        {
            mapContainer = new GameObject("Map");
        }

        public void SetIconImages(Sprite[] icons)
        {
            _iconImages = icons;
            var img = _iconImages[0];
            iconWidth = img.textureRect.width;
            iconHeight = img.textureRect.height;
        }

        /*
         * 인덱스에 해당하는 맵의 셀(그리드 한 칸)을 반환
         */
        public GameObject GetMapCell(int index)
        {
            return mapContainer.transform.GetChild(index).gameObject;
        }

        /*
         * 맵 상의 모든 셀을 제거한다
         */
        public void ClearCells()
        {
            if (mapContainer != null) {
                foreach (Transform child in mapContainer.transform) {
                    Destroy(child.gameObject);
                }
                mapContainer.transform.localScale = Vector3.one;
                mapContainer.transform.localPosition = Vector3.zero;
            }
            mapName = string.Empty;
        }

        public void SetData(GridMapData data)
        {
            var map = data.map;
            if (map != null) {
                ClearCells();

                mapName = data.name;

                int width = map.GetLength(0);
                int height = map.GetLength(1);
                GenerateNewMap(width, height, map);

                Debug.Log("map loaded successfully");
            }
            else Debug.LogWarning("map is null!");
        }

        /* 
         * 기존의 맵을 날리고 지정된 크기의 새로운 맵을 만든다 
         */
        public void GenerateNewMap(int width, int height, int[,] cellData = null)
        {
            ClearCells(); // 기존의 맵을 제거한다


            mapWidth = width;
            mapHeight = height;

            float startX = -width / 2f;
            float startZ = -height / 2f;

            float gridSize = iconWidth > iconHeight ? iconHeight : iconWidth;

            float scale = 100f / gridSize;
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    Vector3 position = new Vector3(startX + x, startZ + y);
                    var cell = Instantiate(cellPrefab, position, Quaternion.identity, mapContainer.transform);
                    cell.transform.SetParent(mapContainer.transform);
                    
                    cell.transform.localScale = Vector3.one * scale;

                    var cellComponent = cell.GetComponent<Cell>();
                    cellComponent.icons = _iconImages;

                    int gridType = 0;

                    if (cellData != null) gridType = cellData[x, y];

                    cellComponent.SetType(gridType);
                }
            }
            AdjustScale();
        }

        void AdjustScale()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            float maxDimension = Mathf.Max(mapWidth + .5f, mapHeight + .5f);
            float scale = camera.orthographicSize * 2 / maxDimension;
            mapContainer.transform.localScale = new Vector3(scale, scale);
            mapContainer.transform.localPosition = Vector3.one * (scale / 2);
        }
    }
}

