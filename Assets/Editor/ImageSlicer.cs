using UnityEngine;
using UnityEditor;
using System.IO;

public class ImageSlicer : EditorWindow
{
    private int columns = 4;
    private int rows = 2;

    [MenuItem("Tools/Image Slicer")]
    public static void ShowWindow()
    {
        GetWindow<ImageSlicer>("Image Slicer");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Slice Settings", EditorStyles.boldLabel);
        
        columns = EditorGUILayout.IntField("Columns (열)", columns);
        rows = EditorGUILayout.IntField("Rows (행)", rows);

        GUILayout.Space(20);

        if (GUILayout.Button("Slice Selected Textures", GUILayout.Height(30)))
        {
            SliceSelectedTextures();
        }
        
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Project 창에서 분할할 이미지(Texture2D)를 하나 이상 선택한 후 버튼을 누르세요.", MessageType.Info);
    }

    private void SliceSelectedTextures()
    {
        if (columns <= 0 || rows <= 0)
        {
            Debug.LogError("Column과 Row는 1 이상이어야 합니다.");
            return;
        }

        // 프로젝트 창에서 선택된 Texture2D 에셋만 필터링해서 가져옴
        Object[] selectedObjects = Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets);

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("선택된 텍스처가 없습니다. Project 창에서 이미지를 선택해주세요.");
            return;
        }

        int processedCount = 0;

        foreach (Object obj in selectedObjects)
        {
            Texture2D tex = obj as Texture2D;
            if (tex == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(tex);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer == null) continue;

            // 픽셀 데이터를 읽기 위해 임시로 Read/Write Enable 활성화
            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            int sliceWidth = tex.width / columns;
            int sliceHeight = tex.height / rows;

            string directory = Path.GetDirectoryName(assetPath);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);

            // Row와 Column에 맞춰 이미지 분할
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    // 유니티 텍스처 좌표계는 좌측 하단이 (0,0)이므로, 
                    // 위에서 아래로 자르기 위해 Y 좌표를 반전시켜 계산
                    int pixelX = x * sliceWidth;
                    int pixelY = (rows - 1 - y) * sliceHeight;

                    // EncodeToPNG를 위해 압축되지 않은 포맷(RGBA32)으로 새 텍스처 생성
                    Texture2D slice = new Texture2D(sliceWidth, sliceHeight, TextureFormat.RGBA32, false);
                    slice.SetPixels(tex.GetPixels(pixelX, pixelY, sliceWidth, sliceHeight));
                    slice.Apply();

                    byte[] bytes = slice.EncodeToPNG();
                    
                    // 네이밍 규칙: 원본이름_Row_Column.png
                    string newFileName = $"{fileName}_{y}_{x}.png";
                    string fullPath = Path.Combine(directory, newFileName);

                    File.WriteAllBytes(fullPath, bytes);
                    DestroyImmediate(slice);
                }
            }

            // 원본 텍스처의 Read/Write 설정을 원래대로 복구
            if (!wasReadable)
            {
                importer.isReadable = false;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            processedCount++;
        }

        // 새로 생성된 파일들이 에디터에 즉시 반영되도록 새로고침
        AssetDatabase.Refresh();
        Debug.Log($"[Image Slicer] {processedCount}개의 이미지를 성공적으로 분할했습니다!");
    }
}