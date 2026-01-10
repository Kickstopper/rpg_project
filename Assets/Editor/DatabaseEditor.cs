using UnityEngine;
using UnityEditor;
using System.Reflection;
using Data;

// -----------------------------------------------------------
// 1. 실제 버튼 기능이 들어있는 부모 클래스 (속성 없음)
// -----------------------------------------------------------
public class BaseDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 원래 인스펙터(리스트 등) 그리기
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        // 초록색 버튼 그리기
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Load All Assets from Resources", GUILayout.Height(40)))
        {
            LoadAssets_viaReflection();
        }
        GUI.backgroundColor = Color.white;
    }

    private void LoadAssets_viaReflection()
    {
        object targetObject = target;
        // "LoadAllFromResources" 함수를 찾아서 실행
        MethodInfo method = targetObject.GetType().GetMethod("LoadAllFromResources");

        if (method != null)
        {
            method.Invoke(targetObject, null);
            Debug.Log($"[{targetObject.GetType().Name}] 데이터 로드 완료!");
        }
        else
        {
            Debug.LogError("LoadAllFromResources 함수를 찾을 수 없습니다.");
        }
    }
}

// -----------------------------------------------------------
// 2. 각 데이터베이스에 연결해주는 자식 클래스들 (여기에 속성 부착)
// -----------------------------------------------------------

[CustomEditor(typeof(WeaponDatabase))]
public class WeaponDatabaseEditor : BaseDatabaseEditor { }

[CustomEditor(typeof(ArmorDatabase))]
public class ArmorDatabaseEditor : BaseDatabaseEditor { }

[CustomEditor(typeof(SkillDatabase))]
public class SkillDatabaseEditor : BaseDatabaseEditor { }