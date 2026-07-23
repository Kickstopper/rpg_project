using UnityEngine;
using UnityEditor;
using System.IO;

public class PixelArtBatchConverter : EditorWindow
{
    private DefaultAsset sourceFolder;
    private DefaultAsset destinationFolder;
    
    [Range(2, 64)]
    public int downscaleFactor = 4; // 축소 비율 (예: 4배 축소)
    
    [Range(2, 64)]
    public int colorSteps = 8; // 색상 단순화 단계 (작을수록 색상이 적어짐)

    [MenuItem("Tools/Image/Pixel Art Batch Converter")]
    public static void ShowWindow()
    {
        GetWindow<PixelArtBatchConverter>("Pixel Art Converter");
    }

    private void OnGUI()
    {
        GUILayout.Label("16-bit 픽셀 아트 일괄 변환기", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField("원본 폴더 (Source)", sourceFolder, typeof(DefaultAsset), false);
        destinationFolder = (DefaultAsset)EditorGUILayout.ObjectField("저장 폴더 (Destination)", destinationFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space();
        downscaleFactor = EditorGUILayout.IntSlider("축소 비율 (Downscale Factor)", downscaleFactor, 1, 64);
        colorSteps = EditorGUILayout.IntSlider("색상 단계 (Color Palette Size)", colorSteps, 2, 64);

        EditorGUILayout.Space();
        if (GUILayout.Button("이미지 변환 시작", GUILayout.Height(40)))
        {
            if (sourceFolder == null || destinationFolder == null)
            {
                EditorUtility.DisplayDialog("오류", "원본 폴더와 저장 폴더를 모두 지정해주세요.", "확인");
                return;
            }
            ProcessImages();
        }
    }

    private void ProcessImages()
    {
        string sourcePath = AssetDatabase.GetAssetPath(sourceFolder);
        string destPath = AssetDatabase.GetAssetPath(destinationFolder);

        string[] files = Directory.GetFiles(sourcePath, "*.*");
        int processedCount = 0;

        foreach (string file in files)
        {
            // 메타 파일 제외 및 이미지 파일만 처리
            if (file.EndsWith(".meta") || (!file.EndsWith(".png") && !file.EndsWith(".jpg") && !file.EndsWith(".jpeg")))
                continue;

            byte[] fileData = File.ReadAllBytes(file);
            Texture2D originalTex = new Texture2D(2, 2);
            originalTex.LoadImage(fileData); // LoadImage를 사용하면 텍스처를 읽기 가능(Readable) 상태로 가져옵니다.

            Texture2D pixelArtTex = ConvertToPixelArt(originalTex);

            string fileName = Path.GetFileNameWithoutExtension(file);
            string savePath = Path.Combine(destPath, fileName + "_pixel.png");

            byte[] pngData = pixelArtTex.EncodeToPNG();
            File.WriteAllBytes(savePath, pngData);

            processedCount++;
            
            // 메모리 정리
            DestroyImmediate(originalTex);
            DestroyImmediate(pixelArtTex);
        }

        AssetDatabase.Refresh(); // 유니티 에디터 프로젝트 창 새로고침
        EditorUtility.DisplayDialog("완료", $"총 {processedCount}개의 이미지를 픽셀 아트로 변환했습니다.", "확인");
    }

    private Texture2D ConvertToPixelArt(Texture2D source)
    {
        int newWidth = Mathf.Max(1, source.width / downscaleFactor);
        int newHeight = Mathf.Max(1, source.height / downscaleFactor);

        Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        result.filterMode = FilterMode.Point; // 핵심: Nearest Neighbor 샘플링 적용

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                // 원본 텍스처에서 정확한 Nearest 좌표 계산
                float u = (float)x / (newWidth - 1);
                float v = (float)y / (newHeight - 1);
                
                int sourceX = Mathf.Clamp(Mathf.RoundToInt(u * (source.width - 1)), 0, source.width - 1);
                int sourceY = Mathf.Clamp(Mathf.RoundToInt(v * (source.height - 1)), 0, source.height - 1);

                Color originalColor = source.GetPixel(sourceX, sourceY);
                Color quantizedColor = QuantizeColor(originalColor, colorSteps);

                result.SetPixel(x, y, quantizedColor);
            }
        }

        result.Apply();
        return result;
    }

    // 색상 수를 제한하여 고전 게임 느낌을 내는 함수
    private Color QuantizeColor(Color c, int steps)
    {
        c.r = Mathf.Round(c.r * steps) / steps;
        c.g = Mathf.Round(c.g * steps) / steps;
        c.b = Mathf.Round(c.b * steps) / steps;
        return c;
    }
}