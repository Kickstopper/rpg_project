#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Data;

[CustomEditor(typeof(ExpTable))]
public class ExpTableEditor : Editor
{
	private Race previewRace = Race.Human;
	private Gender previewGender = Gender.Male;
	private Vector2 scrollPosition;
	private bool showPreview = true;

	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		ExpTable expTable = (ExpTable)target;

		if (expTable == null) return;

		EditorGUILayout.Space(15);
		
		// 미리보기 섹션 헤더
		GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
		{
			fontSize = 14,
			alignment = TextAnchor.MiddleCenter
		};
		EditorGUILayout.LabelField("📊 경험치 테이블 미리보기", headerStyle);
		EditorGUILayout.Space(5);

		// 테스트할 종족과 성별 선택 드롭다운
		EditorGUILayout.BeginVertical("helpbox");
		previewRace = (Race)EditorGUILayout.EnumPopup("미리보기 종족 (Race)", previewRace);
		previewGender = (Gender)EditorGUILayout.EnumPopup("미리보기 성별 (Gender)", previewGender);
		EditorGUILayout.EndVertical();

		EditorGUILayout.Space(5);

		// 미리보기 표(Table) 토글 및 렌더링
		showPreview = EditorGUILayout.Foldout(showPreview, "경험치 요구량 표 (1 ~ Max Level)", true, EditorStyles.foldoutHeader);
		
		if (showPreview)
		{
			// 스크롤 뷰 시작
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, "box", GUILayout.Height(300));

			// 테이블 헤더 (컬럼 이름)
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("레벨", EditorStyles.boldLabel, GUILayout.Width(50));
			EditorGUILayout.LabelField("다음 레벨 필요 EXP", EditorStyles.boldLabel, GUILayout.Width(150));
			EditorGUILayout.LabelField("누적 총 EXP", EditorStyles.boldLabel, GUILayout.Width(150));
			EditorGUILayout.EndHorizontal();

			// 구분선
			Rect rect = EditorGUILayout.GetControlRect(false, 1);
			EditorGUI.DrawRect(rect, Color.gray);

			long totalExp = 0; // 누적 경험치

			// 1레벨부터 만렙 직전까지 계산하여 출력
			for (int i = 1; i < expTable.maxLevel; i++)
			{
				// ExpTable의 실제 계산 함수 호출
				int reqExp = expTable.GetRequiredExp(i, previewRace, previewGender);
				
				// 만렙 방어코드(99999999)가 반환되면 표 작성을 중단
				if (reqExp == 99999999) break;

				totalExp += reqExp;

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField($"Lv. {i}", GUILayout.Width(50));
				
				EditorGUILayout.LabelField(reqExp.ToString("N0"), GUILayout.Width(150));
				
				// 누적 경험치 출력
				GUI.contentColor = Color.cyan;
				EditorGUILayout.LabelField(totalExp.ToString("N0"), GUILayout.Width(150));
				GUI.contentColor = Color.white;
				
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.EndScrollView();
		}
	}
}
#endif