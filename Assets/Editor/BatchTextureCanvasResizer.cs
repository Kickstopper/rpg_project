using System.IO;
using UnityEditor;
using UnityEngine;

public class BatchTextureCanvasResizer : EditorWindow
{
    private DefaultAsset sourceFolder;
    private Vector2Int newSize = new Vector2Int(64, 64);
    private TextAnchor alignment = TextAnchor.LowerCenter;
    private bool overwriteOriginal = false;

    [MenuItem("Tools/Image/Batch Texture Canvas Resizer")]
    public static void ShowWindow()
    {
        GetWindow<BatchTextureCanvasResizer>("Batch Resizer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Batch Resize Texture Canvas", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 설정 UI
        sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Source Folder", 
            sourceFolder, 
            typeof(DefaultAsset), 
            false
        );

        newSize = EditorGUILayout.Vector2IntField("New Canvas Size", newSize);
        alignment = (TextAnchor)EditorGUILayout.EnumPopup("Alignment", alignment);
        
        EditorGUILayout.Space();
        overwriteOriginal = EditorGUILayout.Toggle("Overwrite Original", overwriteOriginal);
        EditorGUILayout.HelpBox(overwriteOriginal ? "경고: 원본 파일이 새로운 캔버스로 덮어씌워집니다." : "원본 파일은 유지되며, '_resized'가 붙은 새 파일이 생성됩니다.", MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("Batch Resize", GUILayout.Height(40)))
        {
            ProcessBatch();
        }
    }

    private void ProcessBatch()
    {
        if (sourceFolder == null)
        {
            EditorUtility.DisplayDialog("Error", "Source Folder를 지정해주세요.", "OK");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(sourceFolder);
        if (!Directory.Exists(folderPath))
        {
            EditorUtility.DisplayDialog("Error", "유효한 폴더가 아닙니다.", "OK");
            return;
        }

        // 지정된 폴더 안의 모든 PNG 파일 찾기 (하위 폴더 제외)
        string[] pngFiles = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);

        if (pngFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("Info", "선택한 폴더에 PNG 파일이 없습니다.", "OK");
            return;
        }

        int successCount = 0;
        int skipCount = 0;

        try
        {
            for (int i = 0; i < pngFiles.Length; i++)
            {
                string filePath = pngFiles[i];
                string fileName = Path.GetFileName(filePath);

                // 이미 _resized가 붙은 파일은 다시 처리하지 않음 (무한루프/중복 방지)
                if (!overwriteOriginal && fileName.EndsWith("_resized.png"))
                {
                    skipCount++;
                    continue;
                }

                // 진행 상태 표시
                float progress = (float)i / pngFiles.Length;
                EditorUtility.DisplayProgressBar("Batch Resizing", $"Processing {fileName} ({i + 1}/{pngFiles.Length})", progress);

                if (ProcessSingleImage(filePath))
                {
                    successCount++;
                }
                else
                {
                    skipCount++;
                }
            }
        }
        finally
        {
            // 작업이 끝나거나 에러가 나도 프로그레스 바는 무조건 제거
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Complete", $"배치 작업 완료!\n성공: {successCount}개\n건너뜀/실패: {skipCount}개", "OK");
    }

    private bool ProcessSingleImage(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D sourceTex = new Texture2D(2, 2);
        sourceTex.LoadImage(fileData);

        // 새 캔버스가 원본보다 작으면 건너뜀
        if (newSize.x < sourceTex.width || newSize.y < sourceTex.height)
        {
            Debug.LogWarning($"[{Path.GetFileName(filePath)}] 캔버스 크기({newSize.x}x{newSize.y})가 원본 이미지({sourceTex.width}x{sourceTex.height})보다 작아서 건너뜁니다.");
            DestroyImmediate(sourceTex);
            return false;
        }

        // 새로운 투명 텍스처 생성
        Texture2D newTex = new Texture2D(newSize.x, newSize.y, TextureFormat.RGBA32, false);
        Color[] clearColors = new Color[newSize.x * newSize.y];
        for (int i = 0; i < clearColors.Length; i++) clearColors[i] = Color.clear;
        newTex.SetPixels(clearColors);

        // 정렬 기준에 따른 위치 계산
        int startX = 0, startY = 0;

        switch (alignment)
        {
            case TextAnchor.LowerLeft: startX = 0; startY = 0; break;
            case TextAnchor.LowerCenter: startX = (newSize.x - sourceTex.width) / 2; startY = 0; break;
            case TextAnchor.LowerRight: startX = newSize.x - sourceTex.width; startY = 0; break;
            case TextAnchor.MiddleLeft: startX = 0; startY = (newSize.y - sourceTex.height) / 2; break;
            case TextAnchor.MiddleCenter: startX = (newSize.x - sourceTex.width) / 2; startY = (newSize.y - sourceTex.height) / 2; break;
            case TextAnchor.MiddleRight: startX = newSize.x - sourceTex.width; startY = (newSize.y - sourceTex.height) / 2; break;
            case TextAnchor.UpperLeft: startX = 0; startY = newSize.y - sourceTex.height; break;
            case TextAnchor.UpperCenter: startX = (newSize.x - sourceTex.width) / 2; startY = newSize.y - sourceTex.height; break;
            case TextAnchor.UpperRight: startX = newSize.x - sourceTex.width; startY = newSize.y - sourceTex.height; break;
        }

        // 픽셀 복사 및 적용
        Color[] sourcePixels = sourceTex.GetPixels();
        newTex.SetPixels(startX, startY, sourceTex.width, sourceTex.height, sourcePixels);
        newTex.Apply();

        // 저장 경로 설정
        string exportPath;
        if (overwriteOriginal)
        {
            exportPath = filePath; // 원본 덮어쓰기
        }
        else
        {
            string directory = Path.GetDirectoryName(filePath);
            string filename = Path.GetFileNameWithoutExtension(filePath);
            exportPath = Path.Combine(directory, $"{filename}_resized.png");
        }

        // 파일 쓰기
        byte[] pngBytes = newTex.EncodeToPNG();
        File.WriteAllBytes(exportPath, pngBytes);

        // 메모리 해제
        DestroyImmediate(sourceTex);
        DestroyImmediate(newTex);

        return true;
    }
}