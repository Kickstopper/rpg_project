using UnityEngine;
using UnityEditor;
using Data;

[CustomEditor(typeof(GameAppData))]
public class GameAppDataEditor : Editor
{
    private const int GRID_SIZE = 5; // 5x5 그리드
    private const int CENTER = 2;    // 배열의 정중앙 인덱스 (0, 1, [2], 3, 4)

    public override void OnInspectorGUI()
    {
        // 기본 인스펙터 항목들 (이름, 아이콘 등)을 먼저 그림
        base.OnInspectorGUI();

        GameAppData appData = (GameAppData)target;

        GUILayout.Space(20);
        GUILayout.Label("🧩 도형 편집기 (가운데가 기준점 0,0)", EditorStyles.boldLabel);

        // 현재 저장된 List<Vector2Int> 데이터를 5x5 bool 배열(그리드)로 변환
        bool[,] grid = new bool[GRID_SIZE, GRID_SIZE];
        foreach (Vector2Int pos in appData.shapeBlocks)
        {
            int visualX = pos.x + CENTER;
            int visualY = CENTER - pos.y; // 시각적 UI는 위에서 아래로 그려지므로 Y축 반전

            // 그리드 범위를 벗어나는 데이터는 무시
            if (visualX >= 0 && visualX < GRID_SIZE && visualY >= 0 && visualY < GRID_SIZE)
            {
                grid[visualX, visualY] = true;
            }
        }

        // 에디터에서 변경사항이 있는지 추적 시작
        EditorGUI.BeginChangeCheck();

        // 5x5 버튼 그리드 그리기
        for (int y = 0; y < GRID_SIZE; y++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // 버튼들을 가운데 정렬하기 위한 여백

            for (int x = 0; x < GRID_SIZE; x++)
            {
                Color defaultColor = GUI.backgroundColor;

                // 색상 설정: 선택된 블록은 청록색, 중앙(기준점)은 눈에 띄게 표시
                if (grid[x, y])
                {
                    GUI.backgroundColor = (x == CENTER && y == CENTER) ? Color.green : Color.cyan;
                }
                else
                {
                    GUI.backgroundColor = (x == CENTER && y == CENTER) ? new Color(0.6f, 0.6f, 0.6f) : Color.darkGray;
                }

                // 버튼 클릭 처리
                if (GUILayout.Button("", GUILayout.Width(30), GUILayout.Height(30)))
                {
                    grid[x, y] = !grid[x, y]; // 상태 토글 (On/Off)
                }

                GUI.backgroundColor = defaultColor; // 색상 원상복구
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // 사용자가 버튼을 클릭해서 상태가 변했다면
        if (EditorGUI.EndChangeCheck())
        {
            // Ctrl+Z(실행 취소)를 위한 기록 남기기
            Undo.RecordObject(appData, "Change App Shape");

            // bool 배열을 다시 List<Vector2Int>로 변환하여 저장
            appData.shapeBlocks.Clear();
            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int x = 0; x < GRID_SIZE; x++)
                {
                    if (grid[x, y])
                    {
                        // 시각적 좌표를 다시 수학적 좌표(중앙이 0,0)로 변환
                        appData.shapeBlocks.Add(new Vector2Int(x - CENTER, CENTER - y));
                    }
                }
            }

            // 디스크에 변경사항을 강제 저장하도록 플래그 세팅
            EditorUtility.SetDirty(appData);
        }

        GUILayout.Space(10);
        
        // 정보 표시
        EditorGUILayout.HelpBox($"총 차지 메모리: {appData.memoryCost} 블록", MessageType.Info);
        
        // 데이터가 1칸도 없을 경우 경고
        if (appData.memoryCost == 0)
        {
            EditorGUILayout.HelpBox("도형은 최소 1개 이상의 블록(기준점 등)을 포함해야 합니다!", MessageType.Warning);
        }
    }
}