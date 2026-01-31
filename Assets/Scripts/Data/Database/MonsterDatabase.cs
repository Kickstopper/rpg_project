using System.Collections.Generic;
using UnityEngine;
using Data;

#if UNITY_EDITOR
using UnityEditor; // 에디터 기능 추가
#endif


[CreateAssetMenu(fileName = "MonsterDatabase", menuName = "Game Data/Monster Database")]
public class MonsterDatabase : ScriptableObject
{
    [System.Serializable]
    public class MonsterEntry
    {
        [Header("Basic Info")]
        public string id;           // "M001_Slime"
        public string name;         // "슬라임"
        public string race;         // "요정"

        [Header("Visual")]
        public Sprite[] image;     // 이미지
        public Sprite portrait;     // 초상화
        public GameObject prefab;   // 3D 모델 또는 전투용 프리팹

        [Header("Combat Position")]
        public RowType preferredRow; // Front 또는 Back
        public ColumnType preferredCol; // 왼쪽, 오른쪽 또는 가운데

        [Header("Combat Stats")]
        public Align align;
        public StatData stats;             // 레벨, 힘, 마력, 체력 등
        public ResistanceData resistances; // 물리, 화염, 빙결 내성 등

        [Header("Rewards")]
        public List<string> dropItemIds; // 드랍 아이템 ID 리스트
    }
    
    [Header("몬스터 리스트")]
    public List<MonsterEntry> entries = new List<MonsterEntry>();

    // 빠른 검색을 위한 딕셔너리 (게임 시작 시 생성)
    private Dictionary<string, MonsterEntry> lookupTable;

    public void Initialize()
    {
        lookupTable = new Dictionary<string, MonsterEntry>();
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.id)) continue;

            if (!lookupTable.ContainsKey(entry.id))
            {
                lookupTable.Add(entry.id, entry);
            }
        }
        Debug.Log($"[MonsterDB] 몬스터 {entries.Count}마리 로드 완료.");
    }

    public MonsterEntry GetEntry(string id)
    {
        if (lookupTable == null) Initialize();

        if (lookupTable.TryGetValue(id, out MonsterEntry entry))
        {
            return entry;
        }
        return null;
    }

    // 인스펙터의 톱니바퀴 아이콘이나 컴포넌트 우클릭 메뉴에 "Auto Generate IDs" 항목을 추가합니다.
    [ContextMenu("Auto Generate IDs")] 
    public void AutoGenerateIds()
    {
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            // "D3" 포맷 문자열은 숫자를 3자리로 맞추고 빈 자리는 0으로 채웁니다.
            // 예: 1 -> "001", 23 -> "023", 105 -> "105"
            entries[i].id = i.ToString("D3");
        }

#if UNITY_EDITOR
        // 스크립터블 오브젝트의 내용이 변경되었음을 유니티에 알려서, 
        // 에디터를 껐다 켜도 데이터가 저장되도록 합니다.
        EditorUtility.SetDirty(this);
        Debug.Log($"[MonsterDatabase] {entries.Count}개의 몬스터 ID가 000부터 순차적으로 재설정되었습니다.");
#endif
    }
}