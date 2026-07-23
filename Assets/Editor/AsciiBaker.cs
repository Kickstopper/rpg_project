using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class AsciiBaker : EditorWindow
{
    private Texture2D singleSourceTexture;
    private Vector2Int targetResolution = new Vector2Int(64, 64);
    private string asciiChars = " .:-=+*#%@";

    [MenuItem("Tools/Image/ASCII Art Baker")]
    public static void ShowWindow() => GetWindow<AsciiBaker>("ASCII Baker");

    private void OnGUI()
    {
        // 공통 설정
        GUILayout.Label("Bake Settings", EditorStyles.boldLabel);
        targetResolution = EditorGUILayout.Vector2IntField("Target Resolution", targetResolution);
        asciiChars = EditorGUILayout.TextField("ASCII Chars", asciiChars);

        EditorGUILayout.Space(10);
        DrawLine();
        EditorGUILayout.Space(10);

        // 단일 변환 모드
        GUILayout.Label("Single Bake Mode", EditorStyles.boldLabel);
        singleSourceTexture = (Texture2D)EditorGUILayout.ObjectField("Monster Texture", singleSourceTexture, typeof(Texture2D), false);
        
        if (GUILayout.Button("Bake Single Texture") && singleSourceTexture != null)
        {
            BakeTexture(singleSourceTexture);
            AssetDatabase.Refresh();
            Debug.Log($"[ASCII Baker] {singleSourceTexture.name} 단일 변환 완료!");
        }

        EditorGUILayout.Space(10);
        DrawLine();
        EditorGUILayout.Space(10);

        // 일괄 변환 모드
        GUILayout.Label("Batch Bake Mode", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Project 창에서 변환할 몬스터 이미지들을 여러 개 선택하거나 폴더를 선택한 후 아래 버튼을 누르세요.", MessageType.Info);

        if (GUILayout.Button("Batch Bake Selected Textures", GUILayout.Height(30)))
        {
            BatchBakeSelected();
        }
    }

    private void BatchBakeSelected()
    {
        // Project 창에서 현재 선택된 모든 텍스처를 가져옴 (폴더를 선택해도 OK)
        Texture2D[] selectedTextures = Selection.GetFiltered<Texture2D>(SelectionMode.DeepAssets);

        if (selectedTextures.Length == 0)
        {
            EditorUtility.DisplayDialog("알림", "Project 창에서 변환할 텍스처(또는 폴더)를 먼저 선택해 주세요.", "확인");
            return;
        }

        // 대량 작업 전 확인 팝업
        if (!EditorUtility.DisplayDialog("Batch Bake", $"총 {selectedTextures.Length}개의 텍스처를 아스키 아트로 변환하시겠습니까?\n(기존 파일이 있다면 덮어씁니다)", "변환 시작", "취소"))
        {
            return;
        }

        int successCount = 0;

        for (int i = 0; i < selectedTextures.Length; i++)
        {
            Texture2D tex = selectedTextures[i];
            
            // 화면 중앙에 프로그레스 바 표시
            EditorUtility.DisplayProgressBar("Baking ASCII Art...", $"Processing {tex.name} ({i + 1}/{selectedTextures.Length})", (float)i / selectedTextures.Length);

            BakeTexture(tex);
            successCount++;
        }

        // 작업 완료 후 프로그레스 바 제거 및 에셋 갱신
        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료", $"총 {successCount}개의 아스키 아트 일괄 변환이 완료되었습니다!", "확인");
    }

    private void BakeTexture(Texture2D sourceTex)
    {
        RenderTexture rt = RenderTexture.GetTemporary(targetResolution.x, targetResolution.y);
        Graphics.Blit(sourceTex, rt);
        
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D resultTex = new Texture2D(targetResolution.x, targetResolution.y);
        resultTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        resultTex.Apply();

        RenderTexture.active = previous;

        StringBuilder sb = new StringBuilder();

        for (int y = targetResolution.y - 1; y >= 0; y--)
        {
            for (int x = 0; x < targetResolution.x; x++)
            {
                Color pixelColor = resultTex.GetPixel(x, y);
                
                if (pixelColor.a < 0.1f)
                {
                    sb.Append(" ");
                }
                else
                {
                    float brightness = (pixelColor.r * 0.299f) + (pixelColor.g * 0.587f) + (pixelColor.b * 0.114f);
                    int charIndex = Mathf.Clamp(Mathf.FloorToInt(brightness * asciiChars.Length), 0, asciiChars.Length - 1);
                    sb.Append(asciiChars[charIndex]);
                }
            }
            sb.AppendLine();
        }

        string path = AssetDatabase.GetAssetPath(sourceTex);
        string directory = Path.GetDirectoryName(path);
        string fileName = Path.GetFileNameWithoutExtension(path) + "_ascii.txt";
        File.WriteAllText(Path.Combine(directory, fileName), sb.ToString());
        
        RenderTexture.ReleaseTemporary(rt);
        DestroyImmediate(resultTex); // 대량 작업 시 메모리 누수 방지
    }

    // UI 디자인을 위한 가로선 긋기 헬퍼 함수
    private void DrawLine()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 1f));
    }
}