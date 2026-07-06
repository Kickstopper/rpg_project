using UnityEngine;
using UnityEditor;

public class MonsterDatabaseEditor : EditorWindow
{
    private MonsterDatabase database;
    private SerializedObject serializedDB;
    private SerializedProperty entriesProp;
    private Vector2 scrollPosition;
    private bool showAnimImages = true; // 이미지 표시 여부를 결정하는 변수
    private enum SortType { None, Race, Level, Gender } // 정렬 상태를 저장하기 위한 변수들
    private SortType currentSortType = SortType.None;
    private bool sortAscending = true;

    [MenuItem("Tools/Monster Database Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<MonsterDatabaseEditor>("Monster DB Editor");
        window.minSize = new Vector2(1200, 600); 
        window.Show();
    }

    private void OnEnable()
    {
        FindDatabase();
        showAnimImages = EditorPrefs.GetBool("MonsterDBEditor_ShowAnimImages", true);
    }

    private void OnDisable()
    {
        EditorPrefs.SetBool("MonsterDBEditor_ShowAnimImages", showAnimImages);
    }

    private void FindDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:MonsterDatabase");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            database = AssetDatabase.LoadAssetAtPath<MonsterDatabase>(path);
            if (database != null)
            {
                serializedDB = new SerializedObject(database);
                entriesProp = serializedDB.FindProperty("entries");
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        
        // --- [상단 컨트롤 패널] ---
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Monster DB", EditorStyles.boldLabel, GUILayout.Width(80));
        
        EditorGUI.BeginChangeCheck();
        database = (MonsterDatabase)EditorGUILayout.ObjectField(database, typeof(MonsterDatabase), false, GUILayout.Width(200));
        if (EditorGUI.EndChangeCheck() && database != null)
        {
            serializedDB = new SerializedObject(database);
            entriesProp = serializedDB.FindProperty("entries");
        }
        
        if (GUILayout.Button("Refresh", GUILayout.Width(60)))
        {
            FindDatabase();
        }
        
        if (GUILayout.Button("Auto Generate IDs", GUILayout.Width(130)))
        {
            if (database != null)
            {
                database.AutoGenerateIds();
                serializedDB.Update();
            }
        }

        GUILayout.Space(15);
        
        // --- [정렬(Sort) 툴바] ---
        GUILayout.Label("Sort By:", EditorStyles.label, GUILayout.Width(50));
        if (GUILayout.Button("Race" + GetSortArrow(SortType.Race), EditorStyles.miniButtonLeft, GUILayout.Width(60))) SortEntries(SortType.Race);
        if (GUILayout.Button("Level" + GetSortArrow(SortType.Level), EditorStyles.miniButtonMid, GUILayout.Width(60))) SortEntries(SortType.Level);
        if (GUILayout.Button("Gender" + GetSortArrow(SortType.Gender), EditorStyles.miniButtonRight, GUILayout.Width(70))) SortEntries(SortType.Gender);

        GUILayout.Space(15);

        // --- [비주얼 토글 버튼] ---
        EditorGUI.BeginChangeCheck();
        showAnimImages = GUILayout.Toggle(showAnimImages, "Show Anim Images", "Button", GUILayout.Width(120));
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool("MonsterDBEditor_ShowAnimImages", showAnimImages);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (database == null || serializedDB == null)
        {
            EditorGUILayout.HelpBox("MonsterDatabase 에셋을 찾을 수 없습니다. 에셋을 할당해주세요.", MessageType.Warning);
            return;
        }

        serializedDB.Update();

        // --- [데이터베이스 헤더] ---
        DrawHeader();

        // --- [데이터 리스트 스크롤 뷰] ---
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            DrawMonsterEntry(entriesProp.GetArrayElementAtIndex(i), i);
        }

        EditorGUILayout.EndScrollView();

        serializedDB.ApplyModifiedProperties();
        
        // --- [하단 데이터 추가 버튼] ---
        EditorGUILayout.Space();
        if (GUILayout.Button("Add New Monster", GUILayout.Height(30)))
        {
            entriesProp.arraySize++;
            serializedDB.ApplyModifiedProperties();
        }
    }

    // 정렬 방향 화살표를 표시하는 헬퍼 함수
    private string GetSortArrow(SortType type)
    {
        if (currentSortType == type)
        {
            return sortAscending ? " ▲" : " ▼";
        }
        return "";
    }

    // 실제 데이터를 정렬하는 함수
    private void SortEntries(SortType type)
    {
        if (database == null || database.entries == null) return;

        // 에디터 창에서 수정한 내용을 실제 데이터베이스에 먼저 저장
        serializedDB.ApplyModifiedProperties();

        // 같은 정렬 버튼을 다시 누르면 정렬 방향(오름차순/내림차순)을 반전시킴
        if (currentSortType == type)
        {
            sortAscending = !sortAscending;
        }
        else
        {
            currentSortType = type;
            sortAscending = true; // 새로운 정렬 기준이면 오름차순으로 초기화
        }

        // 정렬
        database.entries.Sort((a, b) =>
        {
            int result = 0;
            switch (type)
            {
                case SortType.Race:
                    result = a.race.CompareTo(b.race);
                    break;
                case SortType.Level:
                    float levelA = a.stats.level;
                    float levelB = b.stats.level;
                    result = levelA.CompareTo(levelB);
                    break;
                case SortType.Gender:
                    result = a.gender.CompareTo(b.gender);
                    break;
            }

            // 내림차순일 경우 결과를 반전
            return sortAscending ? result : -result;
        });

        // 정렬된 리스트를 유니티가 저장하도록 Dirty 마킹 후 직렬화 데이터 업데이트
        EditorUtility.SetDirty(database);
        serializedDB.Update();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Space(25); 
        GUILayout.Label("No.", GUILayout.Width(30));
        GUILayout.Label("Portrait", GUILayout.Width(50));
        
        if (showAnimImages)
        {
            GUILayout.Label("Anim Images", GUILayout.Width(135)); 
        }
        
        GUILayout.Label("ID", GUILayout.Width(80));
        GUILayout.Label("Name", GUILayout.Width(120));
        GUILayout.Label("Boss", GUILayout.Width(40));
        GUILayout.Label("Lv", GUILayout.Width(30));
        GUILayout.Label("Race", GUILayout.Width(80));
        GUILayout.Label("Align", GUILayout.Width(80));
        GUILayout.Label("Gender", GUILayout.Width(80));
        GUILayout.FlexibleSpace();
        GUILayout.Label("Actions", GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawMonsterEntry(SerializedProperty entryProp, int index)
    {
        SerializedProperty idProp = entryProp.FindPropertyRelative("id");
        SerializedProperty nameProp = entryProp.FindPropertyRelative("name");
        SerializedProperty isBossProp = entryProp.FindPropertyRelative("isBoss");
        SerializedProperty raceProp = entryProp.FindPropertyRelative("race");
        SerializedProperty alignProp = entryProp.FindPropertyRelative("align");
        SerializedProperty genderProp = entryProp.FindPropertyRelative("gender");
        
        SerializedProperty portraitProp = entryProp.FindPropertyRelative("portrait");
        SerializedProperty imageArrayProp = entryProp.FindPropertyRelative("image");

        // 전투 스탯 내의 레벨 프로퍼티 찾기
        SerializedProperty statsProp = entryProp.FindPropertyRelative("stats");
        SerializedProperty levelProp = statsProp != null ? statsProp.FindPropertyRelative("level") : null;

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();

        // 접기/펼치기 토글
        entryProp.isExpanded = EditorGUILayout.Toggle(entryProp.isExpanded, EditorStyles.foldout, GUILayout.Width(15));
        GUILayout.Label(index.ToString(), GUILayout.Width(30));

        // 썸네일 렌더링
        EditorGUILayout.BeginHorizontal(GUILayout.Width(50));
        DrawThumbnail(portraitProp);
        EditorGUILayout.EndHorizontal();

        if (showAnimImages)
        {
            DrawSpriteArrayPreview(imageArrayProp, 135);
        }

        // 주요 기본 정보
        EditorGUILayout.PropertyField(idProp, GUIContent.none, GUILayout.Width(80));
        EditorGUILayout.PropertyField(nameProp, GUIContent.none, GUILayout.Width(120));
        EditorGUILayout.PropertyField(isBossProp, GUIContent.none, GUILayout.Width(40));
        
        // 레벨 필드 노출 (정렬 결과를 바로 볼 수 있도록)
        if (levelProp != null)
        {
            EditorGUILayout.PropertyField(levelProp, GUIContent.none, GUILayout.Width(30));
        }
        else
        {
            GUILayout.Label("-", GUILayout.Width(30));
        }

        EditorGUILayout.PropertyField(raceProp, GUIContent.none, GUILayout.Width(80));
        EditorGUILayout.PropertyField(alignProp, GUIContent.none, GUILayout.Width(80));
        EditorGUILayout.PropertyField(genderProp, GUIContent.none, GUILayout.Width(80));
        
        GUILayout.FlexibleSpace();

        // 삭제 버튼
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            entriesProp.DeleteArrayElementAtIndex(index);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // 항목을 펼쳤을 때 세부 정보 표시
        if (entryProp.isExpanded)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space();
            
            SerializedProperty iterator = entryProp.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            iterator.NextVisible(true);
            
            do 
            {
                if (SerializedProperty.EqualContents(iterator, endProperty)) break;

                string propName = iterator.name;
                
                // 가로에 표시한 속성 제외
                if (propName == "id" || propName == "name" || propName == "isBoss" || 
                    propName == "race" || propName == "align" || propName == "gender" || 
                    propName == "portrait")
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            } while (iterator.NextVisible(false));

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSpriteArrayPreview(SerializedProperty arrayProp, float areaWidth)
    {
        int maxPreviewCount = 3; 
        int arraySize = arrayProp.arraySize;
        int displayCount = Mathf.Min(arraySize, maxPreviewCount);

        EditorGUILayout.BeginHorizontal(GUILayout.Width(areaWidth));

        if (arraySize == 0)
        {
            GUI.color = Color.gray;
            GUILayout.Label("No Img", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(40), GUILayout.Height(40));
            GUI.color = Color.white;
        }
        else
        {
            for (int i = 0; i < displayCount; i++)
            {
                SerializedProperty spriteProp = arrayProp.GetArrayElementAtIndex(i);
                DrawThumbnail(spriteProp);
            }

            if (arraySize > maxPreviewCount)
            {
                GUILayout.Label($"+{arraySize - maxPreviewCount}", EditorStyles.boldLabel, GUILayout.Width(25), GUILayout.Height(40));
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawThumbnail(SerializedProperty spriteProp)
    {
        Rect previewRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
        
        Sprite sprite = spriteProp.objectReferenceValue as Sprite;
        if (sprite != null)
        {
            Texture2D tex = AssetPreview.GetAssetPreview(sprite);
            
            if (tex == null && sprite.texture != null) 
            {
                tex = sprite.texture;
            }

            if (tex != null)
            {
                GUI.DrawTexture(previewRect, tex, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Box(previewRect, "Load");
            }
        }
        else
        {
            GUI.Box(previewRect, "None");
        }

        // ObjectField의 회색 배경을 투명하게 만들어 이미지를 가리지 않도록 처리
        Color defaultColor = GUI.color;
        GUI.color = new Color(0, 0, 0, 0); 
        
        spriteProp.objectReferenceValue = EditorGUI.ObjectField(previewRect, spriteProp.objectReferenceValue, typeof(Sprite), false);
        
        GUI.color = defaultColor;
    }
}