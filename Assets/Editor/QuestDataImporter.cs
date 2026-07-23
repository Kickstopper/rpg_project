using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Data;

public class QuestDataImporter : EditorWindow
{
    private string csvFilePath = "";
    // SO 파일들이 저장될 기본 경로 (프로젝트 내 경로로 자유롭게 수정 가능합니다)
    private string savePath = "Assets/Database/Quest"; 

    // 유니티 상단 메뉴에 툴 추가
    [MenuItem("Tools/CSV/Quest Data Importer")]
    public static void ShowWindow()
    {
        GetWindow<QuestDataImporter>("Quest Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV to QuestData SO Importer", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // CSV 파일 경로 선택 UI
        GUILayout.BeginHorizontal();
        csvFilePath = EditorGUILayout.TextField("CSV File Path", csvFilePath);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            csvFilePath = EditorUtility.OpenFilePanel("Select Quest CSV", Application.dataPath, "csv");
        }
        GUILayout.EndHorizontal();

        // 저장 폴더 경로 설정 UI
        savePath = EditorGUILayout.TextField("Save Path", savePath);

        GUILayout.Space(20);

        // Export 버튼
        if (GUILayout.Button("Generate Quest SO Files", GUILayout.Height(40)))
        {
            if (string.IsNullOrEmpty(csvFilePath) || !File.Exists(csvFilePath))
            {
                EditorUtility.DisplayDialog("Error", "유효한 CSV 파일을 선택해주세요.", "OK");
                return;
            }
            
            if (!AssetDatabase.IsValidFolder(savePath))
            {
                EditorUtility.DisplayDialog("Error", $"저장 경로가 존재하지 않습니다: {savePath}\n폴더를 먼저 생성해주세요.", "OK");
                return;
            }

            ImportCSV(csvFilePath, savePath);
        }
    }

    private void ImportCSV(string path, string saveFolderPath)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length <= 1)
        {
            Debug.LogWarning("CSV 파일이 비어있거나 헤더만 존재합니다.");
            return;
        }

        // CSV 파싱 정규식 (Description 등 데이터 내부의 쉼표(,)를 안전하게 처리하기 위함)
        Regex csvParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");

        // 첫 줄(헤더)을 읽어 컬럼명과 인덱스를 매핑 (열 순서가 바뀌어도 대응 가능)
        string[] headers = csvParser.Split(lines[0]);
        Dictionary<string, int> headerMap = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
        {
            headerMap[headers[i].Trim('\"', ' ').Trim()] = i;
        }

        int successCount = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] fields = csvParser.Split(line);
            
            // 데이터 내부의 쌍따옴표 정리
            for (int j = 0; j < fields.Length; j++)
            {
                fields[j] = fields[j].TrimStart('\"').TrimEnd('\"').Replace("\"\"", "\"");
            }

            // 새로운 QuestData ScriptableObject 인스턴스 생성
            QuestData questSO = ScriptableObject.CreateInstance<QuestData>();

            // 일반 필드 할당
            questSO.QuestID = GetValue(fields, headerMap, "QuestID");
            questSO.QuestName = GetValue(fields, headerMap, "QuestName");
            questSO.QuestType = GetValue(fields, headerMap, "QuestType");
            questSO.Location = GetValue(fields, headerMap, "Location");
            questSO.locationID = GetValue(fields, headerMap, "locationID");
            
            int.TryParse(GetValue(fields, headerMap, "Risk"), out questSO.Risk);
            int.TryParse(GetValue(fields, headerMap, "Reward"), out questSO.Reward);
            
            questSO.Description = GetValue(fields, headerMap, "Description");

            // Targets 파싱 (형식: 몬스터ID:요구수량|몬스터ID:요구수량)
            string rawTargets = GetValue(fields, headerMap, "Targets");
            questSO.Targets = new List<QuestTarget>();
            
            if (!string.IsNullOrEmpty(rawTargets))
            {
                string[] targetArray = rawTargets.Split('|');
                foreach (string t in targetArray)
                {
                    string[] detail = t.Split(':');
                    if (detail.Length == 2)
                    {
                        QuestTarget newTarget = new QuestTarget
                        {
                            monsterID = detail[0].Trim(),
                            requiredCount = int.TryParse(detail[1].Trim(), out int countVal) ? countVal : 0
                        };
                        questSO.Targets.Add(newTarget);
                    }
                }
            }

            // 에셋 파일로 저장 (QuestID를 파일명으로 사용)
            string assetPath = $"{saveFolderPath}/{questSO.QuestID}.asset";
            AssetDatabase.CreateAsset(questSO, assetPath);
            successCount++;
        }

        // 유니티 에셋 데이터베이스 갱신 및 저장
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Success", $"총 {successCount}개의 QuestData 에셋이 성공적으로 생성되었습니다!", "OK");
    }

    // 헤더 이름을 기반으로 안전하게 값을 가져오는 헬퍼 메서드
    private string GetValue(string[] fields, Dictionary<string, int> map, string key)
    {
        if (map.TryGetValue(key, out int index))
        {
            if (index < fields.Length)
            {
                return fields[index];
            }
        }
        return "";
    }
}