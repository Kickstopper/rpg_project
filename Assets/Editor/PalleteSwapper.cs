using UnityEngine;
using UnityEditor;
using System.IO;

public enum PalleteFallbackMode
{
    Pallete_512_Colors,
    Pallete_64_Colors
}

public class PaletteSwapper : EditorWindow
{
    public Texture2D[] sourceImages = new Texture2D[0];
    public Color[] palette = new Color[0];
    public PalleteFallbackMode fallbackMode = PalleteFallbackMode.Pallete_64_Colors;
    public string saveSuffix = "_paletted";

    [MenuItem("Tools/Palette Swapper")]
    public static void ShowWindow()
    {
        GetWindow<PaletteSwapper>("Palette Swapper");
    }

    private void OnGUI()
    {
        GUILayout.Label("Multi-Image Palette Swapper", EditorStyles.boldLabel);

        ScriptableObject target = this;
        SerializedObject so = new SerializedObject(target);

        // 소스 이미지 배열
        SerializedProperty imagesProperty = so.FindProperty("sourceImages");
        EditorGUILayout.PropertyField(imagesProperty, true);

        GUILayout.Space(10);
        GUILayout.Label("Fallback Settings", EditorStyles.boldLabel);

        // Fallback 모드 선택 드롭다운
        fallbackMode = (PalleteFallbackMode)EditorGUILayout.EnumPopup("Fallback Palette", fallbackMode);

        string helpMessage = fallbackMode == PalleteFallbackMode.Pallete_512_Colors 
            ? "Palette가 비어있으면 전체 512색 마스터 팔레트가 매핑됩니다." 
            : "Palette가 비어있으면 균등 분배된 64색(RGB 각 4단계) 팔레트가 매핑됩니다.";
        
        EditorGUILayout.HelpBox(helpMessage, MessageType.Info);

        GUILayout.Space(10);
        
        // 커스텀 팔레트 배열
        SerializedProperty paletteProperty = so.FindProperty("palette");
        EditorGUILayout.PropertyField(paletteProperty, true);
        
        so.ApplyModifiedProperties();

        GUILayout.Space(10);
        saveSuffix = EditorGUILayout.TextField("Save Suffix", saveSuffix);

        GUILayout.Space(15);

        if (GUILayout.Button("Process and Export All PNGs"))
        {
            if (sourceImages == null || sourceImages.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "변환할 Source Images 배열에 이미지를 등록해 주세요.", "OK");
                return;
            }

            ProcessAllImages();
        }
    }

    private void ProcessAllImages()
    {
        int totalImages = sourceImages.Length;
        int processedCount = 0;

        // 사용할 팔레트 결정 (커스텀 팔레트가 비어있다면 Fallback 사용)
        Color[] activePalette = palette;
        if (activePalette == null || activePalette.Length == 0)
        {
            if (fallbackMode == PalleteFallbackMode.Pallete_512_Colors)
            {
                activePalette = CreatePallete512Palette();
            }
            else
            {
                activePalette = CreatePallete64Palette();
            }
            
            Debug.LogWarning($"[Palette Swapper] 커스텀 팔레트가 비어있어 {fallbackMode} 모드로 동작합니다.");
        }

        try
        {
            for (int i = 0; i < totalImages; i++)
            {
                Texture2D sourceImage = sourceImages[i];
                if (sourceImage == null) continue;

                float progress = (float)i / totalImages;
                EditorUtility.DisplayProgressBar("Processing Images", $"{sourceImage.name} 변환 중... ({i + 1}/{totalImages})", progress);

                string path = AssetDatabase.GetAssetPath(sourceImage);
                if (string.IsNullOrEmpty(path)) continue;

                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

                int width = sourceImage.width;
                int height = sourceImage.height;

                Texture2D newTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                newTexture.filterMode = FilterMode.Point;

                Color[] pixels = sourceImage.GetPixels();
                Color[] newPixels = new Color[pixels.Length];

                for (int j = 0; j < pixels.Length; j++)
                {
                    if (pixels[j].a == 0)
                    {
                        newPixels[j] = pixels[j];
                        continue;
                    }
                    newPixels[j] = GetClosestColor(pixels[j], activePalette);
                }

                newTexture.SetPixels(newPixels);
                newTexture.Apply();

                byte[] pngData = newTexture.EncodeToPNG();

                string directory = Path.GetDirectoryName(path);
                string filename = Path.GetFileNameWithoutExtension(path);
                string newPath = Path.Combine(directory, filename + saveSuffix + ".png");

                File.WriteAllBytes(newPath, pngData);
                processedCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Success", $"{processedCount}개의 이미지가 성공적으로 변환되어 저장되었습니다.", "OK");
    }

    // 512색 마스터 팔레트 (RGB 각 8단계)
    private Color[] CreatePallete512Palette()
    {
        Color[] PalletePalette = new Color[512];
        int index = 0;

        for (int b = 0; b <= 7; b++)
        {
            for (int g = 0; g <= 7; g++)
            {
                for (int r = 0; r <= 7; r++)
                {
                    float rValue = Mathf.RoundToInt(r * 255f / 7f) / 255f;
                    float gValue = Mathf.RoundToInt(g * 255f / 7f) / 255f;
                    float bValue = Mathf.RoundToInt(b * 255f / 7f) / 255f;
                    
                    PalletePalette[index++] = new Color(rValue, gValue, bValue);
                }
            }
        }
        return PalletePalette;
    }

    // 64색 균등 팔레트 (RGB 각 4단계: 레벨 0, 2, 5, 7 추출)
    private Color[] CreatePallete64Palette()
    {
        Color[] PalletePalette = new Color[64];
        int[] levels = { 0, 2, 5, 7 }; // 0부터 7사이에서 가장 균등한 간격을 이루는 4개 레벨
        int index = 0;

        for (int b = 0; b < 4; b++)
        {
            for (int g = 0; g < 4; g++)
            {
                for (int r = 0; r < 4; r++)
                {
                    float rValue = Mathf.RoundToInt(levels[r] * 255f / 7f) / 255f;
                    float gValue = Mathf.RoundToInt(levels[g] * 255f / 7f) / 255f;
                    float bValue = Mathf.RoundToInt(levels[b] * 255f / 7f) / 255f;
                    
                    PalletePalette[index++] = new Color(rValue, gValue, bValue);
                }
            }
        }
        return PalletePalette;
    }

    private Color GetClosestColor(Color target, Color[] targetPalette)
    {
        Color closestColor = targetPalette[0];
        float minDistanceSq = float.MaxValue;

        foreach (Color pColor in targetPalette)
        {
            float rDiff = target.r - pColor.r;
            float gDiff = target.g - pColor.g;
            float bDiff = target.b - pColor.b;

            float distanceSq = (rDiff * rDiff) + (gDiff * gDiff) + (bDiff * bDiff);

            if (distanceSq < minDistanceSq)
            {
                minDistanceSq = distanceSq;
                closestColor = pColor;
            }
        }

        return closestColor;
    }
}