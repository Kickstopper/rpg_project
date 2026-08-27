using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UI;

public class DialogueEditorWindow : EditorWindow
{
    [System.Serializable]
    public class DialogueRow
    {
        public string EventID = "";
        public string Seq = "";
        public string Type = "TALK";
        public string BackgroundID = "";
        public string CharacterID = "";
        public string Name = "";
        public string Text = "";
        public string Condition = "";
        public string Action = "";
        public string NextID = "";
    }

    private List<DialogueRow> allRows = new List<DialogueRow>();
    private string csvFilePath = "Assets/CSV/Dialogues/EventScripts.csv";
    
    private Vector2 scrollPos;
    private string searchEventID = ""; // 특정 이벤트만 필터링해서 볼 때 사용

    [MenuItem("Tools/Dialogue Editor")]
    public static void ShowWindow()
    {
        GetWindow<DialogueEditorWindow>("대화 이벤트 에디터");
    }

    void OnGUI()
    {
        GUILayout.Label("대화 및 이벤트 스크립트 에디터", EditorStyles.boldLabel);

        // 저장 및 불러오기
        EditorGUILayout.BeginHorizontal();
        csvFilePath = EditorGUILayout.TextField("CSV 경로", csvFilePath);
        if (GUILayout.Button("불러오기(Load)", GUILayout.Width(100))) LoadCSV();
        if (GUILayout.Button("저장하기(Save)", GUILayout.Width(100))) SaveCSV();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 미리보기
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("▶ 현재 이벤트 미리보기 (Play Mode에서만 작동)", GUILayout.Height(30)))
        {
            PreviewInGame();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        // 필터링 검색창
        searchEventID = EditorGUILayout.TextField("🔍 Event ID 검색 (편집할 이벤트)", searchEventID);

        // 중복 오류 검사
        CheckForDuplicates();

        EditorGUILayout.Space();

        // 데이터 편집 리스트
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < allRows.Count; i++)
        {
            var row = allRows[i];

            // 검색어가 있으면 해당 EventID만 표시 (비어있으면 전체 표시)
            if (!string.IsNullOrEmpty(searchEventID) && !row.EventID.Contains(searchEventID)) 
                continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            
            row.EventID = EditorGUILayout.TextField("EventID", row.EventID, GUILayout.Width(150));
            row.Seq = EditorGUILayout.TextField("Seq", row.Seq, GUILayout.Width(80));
            row.Type = EditorGUILayout.TextField("Type", row.Type, GUILayout.Width(100));
            row.NextID = EditorGUILayout.TextField("NextID", row.NextID, GUILayout.Width(100));
            
            if (GUILayout.Button("X", GUILayout.Width(30))) 
            { 
                allRows.RemoveAt(i); 
                break; 
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            row.BackgroundID = EditorGUILayout.TextField("BG", row.BackgroundID, GUILayout.Width(150));
            row.CharacterID = EditorGUILayout.TextField("CharID", row.CharacterID, GUILayout.Width(120));
            row.Name = EditorGUILayout.TextField("Name", row.Name, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            row.Text = EditorGUILayout.TextField("Text", row.Text);
            row.Condition = EditorGUILayout.TextField("Condition", row.Condition);
            row.Action = EditorGUILayout.TextField("Action", row.Action);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("+ 새 대사 추가"))
        {
            allRows.Add(new DialogueRow() { EventID = searchEventID, Seq = (allRows.Count + 1).ToString() });
        }

        EditorGUILayout.EndScrollView();
    }

    // 미리보기 기능
    private void PreviewInGame()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("알림", "유니티 재생(Play) 버튼을 누른 상태에서만 미리보기가 가능합니다.", "확인");
            return;
        }

        if (string.IsNullOrEmpty(searchEventID))
        {
            EditorUtility.DisplayDialog("알림", "검색창에 미리보기 할 EventID를 정확히 입력해주세요.", "확인");
            return;
        }

        DialogueUI dialogueUI = FindFirstObjectByType<DialogueUI>();
        if (dialogueUI == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 DialogueUI 컴포넌트가 없습니다.", "확인");
            return;
        }

        // 현재 검색된 EventID에 해당하는 데이터만 Dictionary 형태로 변환
        List<Dictionary<string, string>> dynamicLines = new List<Dictionary<string, string>>();
        foreach (var row in allRows.Where(r => r.EventID == searchEventID))
        {
            var dict = new Dictionary<string, string>
            {
                { "Seq", row.Seq }, { "Type", row.Type }, { "BackgroundID", row.BackgroundID },
                { "CharacterID", row.CharacterID }, { "Name", row.Name }, { "Text", row.Text },
                { "Condition", row.Condition }, { "Action", row.Action }, { "NextID", row.NextID }
            };
            dynamicLines.Add(dict);
        }

        if (dynamicLines.Count == 0)
        {
            EditorUtility.DisplayDialog("알림", "해당 EventID의 대사가 없습니다.", "확인");
            return;
        }

        // 초기화 함수 호출
        dialogueUI.InitializeDynamic(dynamicLines);
    }

    // 중복 및 오류 검사 로직
    private void CheckForDuplicates()
    {
        // Seq 중복 검사 (빈 줄은 무시)
        var seqDuplicates = allRows
            .Where(x => !string.IsNullOrWhiteSpace(x.EventID) && !string.IsNullOrWhiteSpace(x.Seq))
            .GroupBy(x => x.EventID.Trim() + "_" + x.Seq.Trim())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        // EventID 재사용 검사
        List<string> scatteredEvents = new List<string>();
        HashSet<string> seenEventIDs = new HashSet<string>();
        string lastEventID = null;

        foreach (var row in allRows)
        {
            string currentID = row.EventID?.Trim();
            
            if (string.IsNullOrEmpty(currentID)) continue;

            // 이전 줄의 ID와 다를 때 새로운 이벤트 블록이 시작됨
            if (currentID != lastEventID)
            {
                // 이미 이전에 처리했던 EventID가 또 나왔다면 재사용된 것으로 판단
                if (seenEventIDs.Contains(currentID))
                {
                    if (!scatteredEvents.Contains(currentID))
                        scatteredEvents.Add(currentID);
                }
                else
                {
                    seenEventIDs.Add(currentID);
                }
            }
            
            lastEventID = currentID;
        }

        // 결과 출력
        if (seqDuplicates.Count > 0 || scatteredEvents.Count > 0)
        {
            string errorMsg = "[데이터 경고] 스크립트에 오류가 있습니다!\n";
            
            if (seqDuplicates.Count > 0)
                errorMsg += $"\n▶ 중복된 대사 번호 (Seq 중복):\n{string.Join(", ", seqDuplicates)}\n";
                
            if (scatteredEvents.Count > 0)
                errorMsg += $"\n▶ 떨어진 위치에 재사용된 EventID (블록 분산):\n{string.Join(", ", scatteredEvents)}\n";

            EditorGUILayout.HelpBox(errorMsg, MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox("데이터가 정상입니다. (오류 없음)", MessageType.Info);
        }
    }

    // CSV 저장
    private void SaveCSV()
    {
        using (StreamWriter writer = new StreamWriter(csvFilePath, false, System.Text.Encoding.UTF8))
        {
            // 헤더 작성
            writer.WriteLine("EventID,Seq,Type,BackgroundID,CharacterID,Name,Text,Condition,Action,NextID");

            foreach (var row in allRows)
            {
                string line = $"{Escape(row.EventID)},{Escape(row.Seq)},{Escape(row.Type)},{Escape(row.BackgroundID)},{Escape(row.CharacterID)},{Escape(row.Name)},{Escape(row.Text)},{Escape(row.Condition)},{Escape(row.Action)},{Escape(row.NextID)}";
                writer.WriteLine(line);
            }
        }
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("저장 완료", "CSV 파일이 성공적으로 저장되었습니다.", "확인");
    }

    // 쉼표나 줄바꿈이 들어간 텍스트를 CSV 규격에 맞게 이스케이프 처리
    private string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return s;
    }

    // CSV 불러오기
    private void LoadCSV()
    {
        if (!File.Exists(csvFilePath))
        {
            EditorUtility.DisplayDialog("오류", "파일을 찾을 수 없습니다.", "확인");
            return;
        }

        allRows.Clear();
        string[] lines = File.ReadAllLines(csvFilePath);
        
        for (int i = 1; i < lines.Length; i++)
        {
            // 정규식을 사용하지 않은 매우 단순화된 분리이므로, 
            // 엑셀에서 작업 후 불러올 때 쉼표 내부에 쉼표가 있으면 깨질 수 있음
            string[] cols = lines[i].Split(','); 
            if (cols.Length < 10) continue;

            allRows.Add(new DialogueRow()
            {
                EventID = cols[0].Trim('"'), Seq = cols[1].Trim('"'), Type = cols[2].Trim('"'),
                BackgroundID = cols[3].Trim('"'), CharacterID = cols[4].Trim('"'), Name = cols[5].Trim('"'),
                Text = cols[6].Trim('"'), Condition = cols[7].Trim('"'), Action = cols[8].Trim('"'),
                NextID = cols[9].Trim('"')
            });
        }
        EditorUtility.DisplayDialog("불러오기 완료", "데이터를 성공적으로 불러왔습니다.", "확인");
    }
}