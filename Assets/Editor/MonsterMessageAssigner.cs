using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;
using System.Collections.Generic;

public class MonsterMessageAssigner : EditorWindow
{
    private MonsterDatabase database;
    private TextAsset csvFile;

    [MenuItem("Tools/Monster Message Assigner")]
    public static void ShowWindow()
    {
        GetWindow<MonsterMessageAssigner>("Message Assigner");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("몬스터 대사(CSV) 자동 할당기", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // MonsterDatabase 에셋 선택 창
        database = (MonsterDatabase)EditorGUILayout.ObjectField("Monster Database", database, typeof(MonsterDatabase), false);
        
        // CSV 파일 선택 창
        csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File (대사 리스트)", csvFile, typeof(TextAsset), false);

        GUILayout.Space(15);

        if (GUILayout.Button("CSV 대사 데이터 할당 실행", GUILayout.Height(30)))
        {
            AssignMessages();
        }
    }

    private void AssignMessages()
    {
        if (database == null)
        {
            Debug.LogError("MonsterDatabase 에셋을 할당해주세요.");
            return;
        }

        if (csvFile == null)
        {
            Debug.LogError("CSV 파일을 할당해주세요.");
            return;
        }

        // BOM(Byte Order Mark) 제거 및 파싱
        string text = csvFile.text.Replace("\uFEFF", "");
        List<string[]> csvData = ParseCSV(text);

        if (csvData.Count <= 1)
        {
            Debug.LogError("CSV 파일이 비어있거나 헤더만 존재합니다.");
            return;
        }

        // 헤더 인덱스 찾기 (열 순서가 바뀌어도 자동으로 매칭)
        string[] headers = csvData[0];
        int idIndex = System.Array.IndexOf(headers, "id");
        int compileIndex = System.Array.IndexOf(headers, "compileResultMsg");
        int condolenceIndex = System.Array.IndexOf(headers, "CondolenceText");

        if (idIndex == -1 || compileIndex == -1 || condolenceIndex == -1)
        {
            Debug.LogError("CSV 파일의 첫 줄(헤더)에 'id', 'compileResultMsg', 'CondolenceText' 열이 모두 존재해야 합니다.");
            return;
        }

        int updatedCount = 0;

        // 1번째 줄(헤더) 이후부터 데이터 읽기
        for (int i = 1; i < csvData.Count; i++)
        {
            string[] row = csvData[i];
            
            // 데이터가 부족한 줄은 스킵
            if (row.Length <= Mathf.Max(idIndex, compileIndex, condolenceIndex))
                continue;

            string id = row[idIndex].Trim();
            string compileMsg = row[compileIndex];
            string condolenceMsg = row[condolenceIndex];

            if (string.IsNullOrEmpty(id)) continue;

            // 데이터베이스에서 ID가 정확히 일치하는 몬스터 찾기
            var entry = database.entries.FirstOrDefault(e => e.id == id);
            
            if (entry != null)
            {
                entry.compileResultMsg = compileMsg;
                entry.CondolenceText = condolenceMsg;
                updatedCount++;
                Debug.Log($"[적용 완료] {entry.id} ({entry.name})의 대사가 업데이트되었습니다.");
            }
            else
            {
                Debug.LogWarning($"[스킵됨] CSV의 ID '{id}'와 일치하는 몬스터를 데이터베이스에서 찾을 수 없습니다.");
            }
        }

        // 변경사항 저장
        if (updatedCount > 0)
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green><b>총 {updatedCount}마리의 몬스터 대사가 성공적으로 업데이트되었습니다!</b></color>");
        }
        else
        {
            Debug.Log("업데이트된 몬스터가 없습니다. CSV 파일의 id 값과 데이터베이스의 id를 확인해주세요.");
        }
    }

    // 큰따옴표 내의 콤마(,)와 줄바꿈을 완벽하게 무시하는 CSV 커스텀 파서
    private List<string[]> ParseCSV(string text)
    {
        List<string[]> rows = new List<string[]>();
        List<string> currentRow = new List<string>();
        bool inQuotes = false;
        StringBuilder currentField = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            
            if (c == '\"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '\"')
                {
                    currentField.Append('\"'); // "" 이스케이프 처리
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                currentRow.Add(currentField.ToString());
                currentField.Clear();
            }
            else if ((c == '\r' || c == '\n') && !inQuotes)
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; // \r\n 처리
                
                currentRow.Add(currentField.ToString());
                currentField.Clear();
                rows.Add(currentRow.ToArray());
                currentRow.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }
        
        // 마지막 남은 항목 처리
        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }
}