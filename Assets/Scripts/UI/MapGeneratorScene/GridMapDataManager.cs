using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using System.IO;
using TMPro;
using Data;

namespace UI.MapGeneratorScene
{
    [System.Serializable]
    public class GridMapData
    {
        public string name;
        public int[,] map;
    }

    public class GridMapDataManager : MonoBehaviour
    {
        public GameObject cellPrefab; // Scene에서 할당됨 
        public Button exportButton; // 저장하기 버튼 Scene에서 할당됨
        public Button importButton; // 불러오기 버튼 Scene에서 할당됨
        public Button newMapButton; // 새로운맵 버튼 Scene에서 할당됨
        public GameObject mapNameField; // 현재의 맵 이름이 담길 TMP_TEXT

        public Sprite[] iconImages;
        public string mapName = "new_map";

        public int mapWidth = 11;
        public int mapHeight = 11;

        private GridMapGenerator gridGenerator;

        void Start()
        {
            GameObject mapGO = new GameObject("GridMapGenerator");
            gridGenerator = mapGO.AddComponent<GridMapGenerator>();
            gridGenerator.SetIconImages(iconImages);
            gridGenerator.cellPrefab = cellPrefab;

            exportButton.onClick.AddListener(ExportMap);
            importButton.onClick.AddListener(OpenFileBrowser);
            newMapButton.onClick.AddListener(GenerateNewMap);
        }

        private void GenerateNewMap()
        {
            mapNameField.GetComponent<TMP_Text>().SetText(mapName);

            gridGenerator.mapWidth = mapWidth;
            gridGenerator.mapHeight = mapHeight;

            gridGenerator.GenerateNewMap(mapWidth, mapHeight);
            Debug.Log("new map generated!");
        }

        private void OpenFileBrowser()
        {
            string path = GetSelectedFilePath("Select JSON File", "", "json");
            if (!string.IsNullOrEmpty(path)) {
                LoadMapFromJson(path);
            }
        }

        private string GetSelectedFilePath(string title, string defaultPath, string extension)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFilePanel(title, defaultPath, extension);
#else
        return string.Empty;
#endif
        }

        private void LoadMapFromJson(string path)
        {
            string json = File.ReadAllText(path);
            GridMapData mapData = JsonConvert.DeserializeObject<GridMapData>(json);

            if (mapData != null) {
                mapWidth = mapData.map.GetLength(0);
                mapHeight = mapData.map.GetLength(1);
                gridGenerator.mapWidth = mapWidth;
                gridGenerator.mapHeight = mapHeight;

                mapName = mapData.name;
                mapNameField.GetComponent<TMP_Text>().SetText(mapName);
                gridGenerator.SetData(mapData);
            }
        }

        void ExportMap()
        {
            GridMapData mapData = new GridMapData();
            int width = gridGenerator.mapWidth;
            int height = gridGenerator.mapHeight;
            int[,] newMap = new int[width, height];

            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    var cell = gridGenerator.GetMapCell(x * height + y);
                    newMap[x, y] = cell.GetComponent<Cell>().typeIdx;
                }
            }

            var tmpText = mapNameField.GetComponent<TMP_Text>();
            if (mapName != tmpText.text) tmpText.SetText(mapName);

            mapData.map = newMap;
            mapData.name = tmpText.text;
            
            string json = JsonConvert.SerializeObject(mapData);

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = mapData.name + "_" + timestamp + ".json";

            string path = Path.Combine(Application.dataPath, "GeneratedMap", filename);
            File.WriteAllText(path, json);
            Debug.Log(mapName + ".json saved successfully");
        }
    }

}
