using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

public class MonsterSpriteAssigner : EditorWindow
{
    private MonsterDatabase database;
    private DefaultAsset spriteFolder;

    [MenuItem("Tools/Monster Sprite Assigner")]
    public static void ShowWindow()
    {
        GetWindow<MonsterSpriteAssigner>("Sprite Assigner");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("몬스터 아틀라스 스프라이트 자동 할당기", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // MonsterDatabase 에셋 선택 창
        database = (MonsterDatabase)EditorGUILayout.ObjectField("Monster Database", database, typeof(MonsterDatabase), false);
        
        // 폴더 선택 창
        spriteFolder = (DefaultAsset)EditorGUILayout.ObjectField("Sprite Folder", spriteFolder, typeof(DefaultAsset), false);

        GUILayout.Space(15);

        if (GUILayout.Button("스프라이트 자동 할당 실행", GUILayout.Height(30)))
        {
            AssignSprites();
        }
    }

    private void AssignSprites()
    {
        if (database == null)
        {
            Debug.LogError("MonsterDatabase 에셋을 할당해주세요.");
            return;
        }

        if (spriteFolder == null)
        {
            Debug.LogError("아틀라스 이미지들이 들어있는 폴더를 할당해주세요.");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(spriteFolder);
        // 선택한 폴더 내의 모든 Texture2D 에셋을 찾음
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        int updatedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);

            // 1. [수정됨] 파일명의 시작 부분이 몬스터의 ID(예: enemy_000)와 일치하는지 확인
            var entry = database.entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.id) && fileName.StartsWith(e.id));
            
            if (entry == null)
            {
                Debug.LogWarning($"[스킵됨] '{fileName}' 파일명과 시작 부분 ID가 일치하는 몬스터를 찾을 수 없습니다.");
                continue;
            }

            // 2. 아틀라스 내의 모든 Sprite 로드 및 숫자 순서대로 정렬 (0 ~ 14)
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            Sprite[] sprites = allAssets.OfType<Sprite>()
                .OrderBy(s => {
                    // 유니티 슬라이스 시 자동 부여되는 맨 뒤의 숫자를 추출하여 정렬
                    string[] parts = s.name.Split('_');
                    if (parts.Length > 0 && int.TryParse(parts.Last(), out int parsedNum))
                        return parsedNum;
                    return 0; 
                })
                .ToArray();

            // 3. 15장인지 검증
            if (sprites.Length != 15)
            {
                Debug.LogError($"[오류] '{fileName}' 아틀라스의 스프라이트 개수가 15개가 아닙니다! (현재 {sprites.Length}개)");
                continue;
            }

            // 4. 인덱스에 맞춰서 배열에 할당
            entry.fallDownImgs = sprites.Skip(0).Take(3).ToArray();  // 0~2
            entry.downImgs     = sprites.Skip(3).Take(3).ToArray();  // 3~5
            entry.leftImgs     = sprites.Skip(6).Take(3).ToArray();  // 6~8
            entry.rightImgs    = sprites.Skip(9).Take(3).ToArray();  // 9~11
            entry.upImgs       = sprites.Skip(12).Take(3).ToArray(); // 12~14

            updatedCount++;
            Debug.Log($"[적용 완료] {entry.id} ({entry.name}) 몬스터에 15장의 스프라이트가 할당되었습니다. (파일명: {fileName})");
        }

        // 5. 변경사항 저장
        if (updatedCount > 0)
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green><b>총 {updatedCount}개의 몬스터 데이터가 성공적으로 업데이트되었습니다!</b></color>");
        }
        else
        {
            Debug.Log("업데이트된 몬스터가 없습니다. 파일 이름과 몬스터 ID를 확인해주세요.");
        }
    }
}