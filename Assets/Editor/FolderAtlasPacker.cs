using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class FolderAtlasPacker : EditorWindow
{
    private DefaultAsset targetRootFolder;
    private int columns = 4;
    private int rows = 4;
    private int spriteWidth = 64;
    private int spriteHeight = 64;

    [MenuItem("Tools/Folder Atlas Packer")]
    public static void ShowWindow()
    {
        GetWindow<FolderAtlasPacker>("Atlas Packer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Folder to Atlas Packer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 설정 UI
        targetRootFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Target Root Folder", 
            targetRootFolder, 
            typeof(DefaultAsset), 
            false
        );

        columns = EditorGUILayout.IntField("Columns (가로 개수)", columns);
        rows = EditorGUILayout.IntField("Rows (세로 개수)", rows);
        spriteWidth = EditorGUILayout.IntField("Sprite Width (px)", spriteWidth);
        spriteHeight = EditorGUILayout.IntField("Sprite Height (px)", spriteHeight);

        EditorGUILayout.Space();

        if (GUILayout.Button("Pack Atlases", GUILayout.Height(40)))
        {
            ProcessPacking();
        }
    }

    private void ProcessPacking()
    {
        if (targetRootFolder == null)
        {
            EditorUtility.DisplayDialog("Error", "대상 루트 폴더를 지정해주세요.", "OK");
            return;
        }

        string rootPath = AssetDatabase.GetAssetPath(targetRootFolder);
        if (!Directory.Exists(rootPath))
        {
            EditorUtility.DisplayDialog("Error", "올바른 폴더 경로가 아닙니다.", "OK");
            return;
        }

        // 선택한 루트 폴더 바로 하위의 폴더들 가져오기
        string[] subDirectories = Directory.GetDirectories(rootPath);

        if (subDirectories.Length == 0)
        {
            EditorUtility.DisplayDialog("Warning", "하위 폴더를 찾을 수 없습니다.", "OK");
            return;
        }

        int successCount = 0;

        foreach (string dirPath in subDirectories)
        {
            string folderName = Path.GetFileName(dirPath);
            
            // 폴더 내 PNG 파일들을 가져와 이름순 정렬
            string[] pngFiles = Directory.GetFiles(dirPath, "*.png")
                .OrderBy(f => f)
                .ToArray();

            if (pngFiles.Length == 0)
                continue;

            PackFolderToAtlas(dirPath, folderName, pngFiles);
            successCount++;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Success", $"{successCount}개의 아틀라스 생성이 완료되었습니다.", "OK");
    }

    private void PackFolderToAtlas(string dirPath, string folderName, string[] pngFiles)
    {
        // 아틀라스 해상도 계산
        int atlasWidth = columns * spriteWidth;
        int atlasHeight = rows * spriteHeight;

        // 투명 텍스처 생성
        Texture2D atlasTex = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
        
        // 배경을 투명하게 초기화
        Color[] clearColors = Enumerable.Repeat(Color.clear, atlasWidth * atlasHeight).ToArray();
        atlasTex.SetPixels(clearColors);

        // 이미지들을 순서대로 배치 (왼쪽 상단부터 배치하기 위한 좌표 계산)
        for (int i = 0; i < pngFiles.Length; i++)
        {
            if (i >= columns * rows)
            {
                Debug.LogWarning($"[{folderName}] 지정된 Grid 크기({columns}x{rows})를 초과한 이미지는 제외되었습니다: {pngFiles[i]}");
                break;
            }

            // Grid 상의 인덱스 계산
            int gridX = i % columns;
            int gridY = i / columns;

            // 텍스처 공간상 좌표 (Unity Texture2D는 좌하단이 0,0이므로 Y축을 뒤집어 좌상단 기준 배치)
            int pixelX = gridX * spriteWidth;
            int pixelY = atlasHeight - ((gridY + 1) * spriteHeight);

            // 개별 PNG 로드
            byte[] fileData = File.ReadAllBytes(pngFiles[i]);
            Texture2D sourceTex = new Texture2D(2, 2);
            sourceTex.LoadImage(fileData); // 내부 크기 자동 리사이즈됨

            // 크기가 다를 경우를 대비한 Bilinear 리사이즈
            if (sourceTex.width != spriteWidth || sourceTex.height != spriteHeight)
            {
                RenderTexture rt = RenderTexture.GetTemporary(spriteWidth, spriteHeight);
                RenderTexture.active = rt;
                Graphics.Blit(sourceTex, rt);
                
                Texture2D resizedTex = new Texture2D(spriteWidth, spriteHeight, TextureFormat.RGBA32, false);
                resizedTex.ReadPixels(new Rect(0, 0, spriteWidth, spriteHeight), 0, 0);
                resizedTex.Apply();

                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                DestroyImmediate(sourceTex);
                sourceTex = resizedTex;
            }

            // 개별 스프라이트 픽셀 복사
            Color[] spritePixels = sourceTex.GetPixels();
            atlasTex.SetPixels(pixelX, pixelY, spriteWidth, spriteHeight, spritePixels);
            
            DestroyImmediate(sourceTex);
        }

        atlasTex.Apply();

        // PNG 파일로 저장
        byte[] atlasBytes = atlasTex.EncodeToPNG();
        string exportPath = Path.Combine(dirPath, $"{folderName}_pack.png");
        File.WriteAllBytes(exportPath, atlasBytes);
        
        DestroyImmediate(atlasTex);

        // 에디터에 파일 인식 및 Import 설정 변경
        string relativePath = "Assets" + exportPath.Substring(Application.dataPath.Length);
        AssetDatabase.ImportAsset(relativePath);

        TextureImporter importer = AssetImporter.GetAtPath(relativePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; // 픽셀아트 스타일 (필요시 변경)
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            
            // 변경사항을 한 번 적용해줍니다.
            importer.SaveAndReimport();
            
            // 팩토리 대신 텍스처 임포터 자체에서 바로 DataProvider를 가져옵니다.
            var dataProviderFactory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            dataProviderFactory.Init();
            var dataProvider = dataProviderFactory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            // 슬라이싱할 그리드 정보 생성
            var spriteRects = new System.Collections.Generic.List<SpriteRect>();
            for (int i = 0; i < Mathf.Min(pngFiles.Length, columns * rows); i++)
            {
                int gridX = i % columns;
                int gridY = i / columns;
                int pixelX = gridX * spriteWidth;
                int pixelY = atlasHeight - ((gridY + 1) * spriteHeight);

                var rect = new SpriteRect
                {
                    name = $"{folderName}_{i}",
                    rect = new Rect(pixelX, pixelY, spriteWidth, spriteHeight),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
                spriteRects.Add(rect);
            }

            // 데이터 적용 및 저장
            dataProvider.SetSpriteRects(spriteRects.ToArray());
            dataProvider.Apply();
            
            // 임포터에 최종 바인딩 후 다시 재임포트
            var assetImporter = dataProvider.targetObject as AssetImporter;
            assetImporter.SaveAndReimport();
        }
    }
}