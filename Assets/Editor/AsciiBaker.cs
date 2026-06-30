using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class AsciiBaker : EditorWindow
{
    private Texture2D sourceTexture;
    private Vector2Int targetResolution = new Vector2Int(64, 64);
    private string asciiChars = " .:-=+*#%@"; // 명도 단계별 문자 (오른쪽으로 갈수록 어두움/밝음)

    [MenuItem("Tools/ASCII Art Baker")]
    public static void ShowWindow() => GetWindow<AsciiBaker>("ASCII Baker");

    private void OnGUI()
    {
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Monster Texture", sourceTexture, typeof(Texture2D), false);
        targetResolution = EditorGUILayout.Vector2IntField("Target Resolution", targetResolution);
        
        if (GUILayout.Button("Bake to TXT") && sourceTexture != null)
        {
            BakeTexture();
        }
    }

    private void BakeTexture()
    {
        // 텍스처를 임시로 크기 조절하여 읽기 위한 RenderTexture 활용
        RenderTexture rt = RenderTexture.GetTemporary(targetResolution.x, targetResolution.y);
        Graphics.Blit(sourceTexture, rt);
        RenderTexture.active = rt;

        Texture2D resultTex = new Texture2D(targetResolution.x, targetResolution.y);
        resultTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        resultTex.Apply();

        StringBuilder sb = new StringBuilder();

        // 아스키 텍스트는 위에서 아래로 읽히므로 y축은 역순으로 루프
        for (int y = targetResolution.y - 1; y >= 0; y--)
        {
            for (int x = 0; x < targetResolution.x; x++)
            {
                Color pixelColor = resultTex.GetPixel(x, y);
                
                // 알파값이 낮으면 공백 처리
                if (pixelColor.a < 0.1f)
                {
                    sb.Append(" ");
                }
                else
                {
                    // 명도 계산 후 문자에 매핑
                    float brightness = (pixelColor.r * 0.299f) + (pixelColor.g * 0.587f) + (pixelColor.b * 0.114f);
                    int charIndex = Mathf.Clamp(Mathf.FloorToInt(brightness * asciiChars.Length), 0, asciiChars.Length - 1);
                    sb.Append(asciiChars[charIndex]);
                }
            }
            sb.AppendLine(); // 줄바꿈
        }

        // 텍스트 파일로 저장
        string path = AssetDatabase.GetAssetPath(sourceTexture);
        string directory = Path.GetDirectoryName(path);
        string fileName = Path.GetFileNameWithoutExtension(path) + "_ascii.txt";
        File.WriteAllText(Path.Combine(directory, fileName), sb.ToString());
        
        AssetDatabase.Refresh();
        RenderTexture.ReleaseTemporary(rt);
        Debug.Log($"ASCII Art baked successfully at: {Path.Combine(directory, fileName)}");
    }
}