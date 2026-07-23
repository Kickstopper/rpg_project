using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Data;

public class SkillTreeCSVImporter : EditorWindow
{
    private TextAsset csvFile;
    
    private string savePath = "Assets/Database/SkillTrees";

    [MenuItem("Tools/CSV/Skill Tree CSV Importer")]
    public static void ShowWindow()
    {
        GetWindow<SkillTreeCSVImporter>("Skill Tree Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV to SkillTreeData Importer", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // CSV 파일 드롭다운
        csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);
        
        GUILayout.Space(5);
        
        // 저장 경로 텍스트 필드
        savePath = EditorGUILayout.TextField("Save Path", savePath);

        GUILayout.Space(20);

        // Import 버튼
        if (GUILayout.Button("Generate / Update ScriptableObjects", GUILayout.Height(30)))
        {
            if (csvFile == null)
            {
                EditorUtility.DisplayDialog("오류", "CSV 파일을 먼저 할당해주세요.", "확인");
                return;
            }

            ImportCSV();
        }
    }

    private void ImportCSV()
    {
        // 줄바꿈 문자를 기준으로 CSV를 라인별로 쪼갬
        string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        if (lines.Length <= 1)
        {
            EditorUtility.DisplayDialog("오류", "CSV 파일이 비어있거나 헤더만 존재합니다.", "확인");
            return;
        }

        // TreeID를 Key로, 해당 트리에 속한 노드 리스트를 Value로 가지는 딕셔너리
        Dictionary<string, List<SkillUnlockNode>> treeGroups = new Dictionary<string, List<SkillUnlockNode>>();

        // CSV 파싱 (첫 번째 줄은 헤더이므로 인덱스 1부터 시작)
        for (int i = 1; i < lines.Length; i++)
        {
            string[] columns = lines[i].Split(',');

            // 스키마에 정의한 10개의 컬럼이 다 있는지 확인
            if (columns.Length < 10) continue;

            string treeID = columns[0].Trim();
            
            SkillUnlockNode node = new SkillUnlockNode();
            node.nodeId = columns[1].Trim();
            int.TryParse(columns[2], out node.reqLevel);
            int.TryParse(columns[3], out node.reqStr);
            int.TryParse(columns[4], out node.reqMag);
            int.TryParse(columns[5], out node.reqInt);
            int.TryParse(columns[6], out node.reqVit);
            int.TryParse(columns[7], out node.reqAgi);
            int.TryParse(columns[8], out node.reqLuc);
            
            // 파이프(|) 기호로 스킬들을 쪼개서 리스트에 담음
            string rewardsString = columns[9].Trim();
            if (!string.IsNullOrEmpty(rewardsString))
            {
                node.rewardSkillChoices = new List<string>(rewardsString.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                node.rewardSkillChoices = new List<string>();
            }

            // 딕셔너리에 추가
            if (!treeGroups.ContainsKey(treeID))
            {
                treeGroups[treeID] = new List<SkillUnlockNode>();
            }
            treeGroups[treeID].Add(node);
        }

        // 저장할 폴더가 없으면 자동 생성
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
            AssetDatabase.Refresh();
        }

        // 파싱된 데이터를 바탕으로 ScriptableObject 생성 또는 업데이트
        int createdCount = 0;
        int updatedCount = 0;

        foreach (var kvp in treeGroups)
        {
            string treeID = kvp.Key;
            List<SkillUnlockNode> nodes = kvp.Value;

            string assetPath = $"{savePath}/{treeID}.asset";
            
            // 이미 해당 이름의 에셋이 존재하는지 확인
            SkillTreeData existingAsset = AssetDatabase.LoadAssetAtPath<SkillTreeData>(assetPath);

            if (existingAsset == null)
            {
                // 없으면 새로 생성
                SkillTreeData newAsset = ScriptableObject.CreateInstance<SkillTreeData>();
                newAsset.unlockNodes = nodes;
                AssetDatabase.CreateAsset(newAsset, assetPath);
                createdCount++;
            }
            else
            {
                // 있으면 내용물만 덮어쓰기 (레퍼런스 유지)
                existingAsset.unlockNodes = nodes;
                EditorUtility.SetDirty(existingAsset); // 유니티에게 이 에셋이 변경되었음을 알림
                updatedCount++;
            }
        }

        // 변경사항 저장 및 에디터 새로고침
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("가져오기 완료", 
            $"성공적으로 데이터를 처리했습니다.\n\n" +
            $"- 새로 생성된 트리: {createdCount}개\n" +
            $"- 덮어쓴 트리: {updatedCount}개", "확인");
    }
}