using System.Collections.Generic;
using UnityEngine;
using Data;
using System.Linq;


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
        public bool isBoss;
        public string id;           // "M001_Slime"
        public string name;         // "슬라임"
        public Race race;         // "요정"
        public Align align;
        public Gender gender;
        
        [Header("Compile")]
        public string compileResultMsg = "HELLO!";
        public TextAsset compileAscii;

        [Header("Visual")]
        public Sprite[] image;     // 이미지
        public float animInterval = 0.5f; // 애니메이션 프레임 간격
        public Sprite portrait;     // 초상화
        
        public Sprite[] fallDownImgs; // 넘어지는 애니메이션 프레임 3장
        public Sprite[] downImgs;     // 아래로 이동 애니메이션 프레임 3장
        public Sprite[] leftImgs;     // 좌로 이동 애니메이션 프레임 3장
        public Sprite[] rightImgs;    // 우로 이동 애니메이션 프레임 3장
        public Sprite[] upImgs;       // 위로 이동 애니메이션 프레임 3장


        [Header("Battle")]
        public VfxID basicAttackVfxId;    // 기본 공격 이펙트 ID
        public Data.AI.MonsterAIProfile aiProfile;
        public List<SkillData> skills;
        public string CondolenceText;

        [Header("Battle Position")]
        public RowType preferredRow; // Front 또는 Back
        public ColumnType preferredCol; // 왼쪽, 오른쪽 또는 가운데

        [Header("Battle Stats")]
        public StatData stats;             // 레벨, 힘, 마력, 체력 등
        public ResistanceData resistances; // 물리, 화염, 빙결 내성 등
        
        [Header("Negotiation")]
        public Personality personality;
        public TimeOfDay timeOfDay; // Morning, Day, Evening, Night
        public Weather weather; // Clear, Rain, Storm
        public MoonPhase moonPhase; // New, Half, Full
        public ChoiceTone choiceTone; // Friendly, Aggressive, Logical, Bribe, Flirt

        [Header("Rewards")]
        public List<string> dropItemIds; // 드랍 아이템 ID 리스트
    }
    
    [Header("몬스터 리스트")]
    public List<MonsterEntry> entries = new List<MonsterEntry>();

    private Dictionary<(Race, Race), Race> raceCombinationTable = new();

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

        InitializeRecipes();
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

    public void InitializeRecipes()
    {
        AddRecipe(Race.Human, Race.Human, Race.Human);
        AddRecipe(Race.Human, Race.Beast, Race.Demihuman);
        AddRecipe(Race.Human, Race.Demihuman, Race.Demihuman);
        AddRecipe(Race.Human, Race.Demon, Race.Demon);
        AddRecipe(Race.Human, Race.Dragon, Race.Dragon);
        AddRecipe(Race.Human, Race.Machine, Race.Machine);
        AddRecipe(Race.Human, Race.Plant, Race.Plant);
        AddRecipe(Race.Human, Race.Spirit, Race.Spirit);
        AddRecipe(Race.Human, Race.Undead, Race.Undead);

        AddRecipe(Race.Beast, Race.Human, Race.Demihuman);
        AddRecipe(Race.Beast, Race.Beast, Race.Beast);
        AddRecipe(Race.Beast, Race.Demihuman, Race.Beast);
        AddRecipe(Race.Beast, Race.Undead, Race.Undead);
        AddRecipe(Race.Beast, Race.Demon, Race.Demon);
        AddRecipe(Race.Beast, Race.Dragon, Race.Dragon);
        AddRecipe(Race.Beast, Race.Machine, Race.Machine);
        AddRecipe(Race.Beast, Race.Plant, Race.Plant);
        AddRecipe(Race.Beast, Race.Spirit, Race.Spirit);
        AddRecipe(Race.Beast, Race.Undead, Race.Undead);

        AddRecipe(Race.Demihuman, Race.Human, Race.Demihuman);
        AddRecipe(Race.Demihuman, Race.Beast, Race.Beast);
        AddRecipe(Race.Demihuman, Race.Demihuman, Race.Demihuman);
        AddRecipe(Race.Demihuman, Race.Demon, Race.Demon);
        AddRecipe(Race.Demihuman, Race.Dragon, Race.Dragon);
        AddRecipe(Race.Demihuman, Race.Machine, Race.Machine);
        AddRecipe(Race.Demihuman, Race.Plant, Race.Plant);
        AddRecipe(Race.Demihuman, Race.Spirit, Race.Spirit);
        AddRecipe(Race.Demihuman, Race.Undead, Race.Undead);

        AddRecipe(Race.Demon, Race.Human, Race.Demon);
        AddRecipe(Race.Demon, Race.Beast, Race.Demon);
        AddRecipe(Race.Demon, Race.Demihuman, Race.Demon);
        AddRecipe(Race.Demon, Race.Demon, Race.Demon);
        AddRecipe(Race.Demon, Race.Dragon, Race.Dragon);
        AddRecipe(Race.Demon, Race.Machine, Race.Machine);
        AddRecipe(Race.Demon, Race.Plant, Race.Plant);
        AddRecipe(Race.Demon, Race.Spirit, Race.Spirit);
        AddRecipe(Race.Demon, Race.Undead, Race.Undead);

        AddRecipe(Race.Dragon, Race.Human, Race.Dragon);
        AddRecipe(Race.Dragon, Race.Beast, Race.Dragon);
        AddRecipe(Race.Dragon, Race.Demihuman, Race.Dragon);
        AddRecipe(Race.Dragon, Race.Demon, Race.Dragon);
        AddRecipe(Race.Dragon, Race.Dragon, Race.Dragon);
        AddRecipe(Race.Dragon, Race.Machine, Race.Machine);
        AddRecipe(Race.Dragon, Race.Plant, Race.Plant);
        AddRecipe(Race.Dragon, Race.Spirit, Race.Spirit);
        AddRecipe(Race.Dragon, Race.Undead, Race.Undead);
        
        AddRecipe(Race.Machine, Race.Human, Race.Machine);
        AddRecipe(Race.Machine, Race.Beast, Race.Machine);
        AddRecipe(Race.Machine, Race.Demihuman, Race.Machine);
        AddRecipe(Race.Machine, Race.Demon, Race.Machine);
        AddRecipe(Race.Machine, Race.Dragon, Race.Machine);
        AddRecipe(Race.Machine, Race.Machine, Race.Machine);
        AddRecipe(Race.Machine, Race.Plant, Race.Machine);
        AddRecipe(Race.Machine, Race.Spirit, Race.Machine);
        AddRecipe(Race.Machine, Race.Undead, Race.Undead);
        
        AddRecipe(Race.Plant, Race.Human, Race.Plant);
        AddRecipe(Race.Plant, Race.Beast, Race.Plant);
        AddRecipe(Race.Plant, Race.Demihuman, Race.Plant);
        AddRecipe(Race.Plant, Race.Demon, Race.Plant);
        AddRecipe(Race.Plant, Race.Dragon, Race.Plant);
        AddRecipe(Race.Plant, Race.Machine, Race.Machine);
        AddRecipe(Race.Plant, Race.Plant, Race.Plant);
        AddRecipe(Race.Plant, Race.Spirit, Race.Spirit);
        AddRecipe(Race.Plant, Race.Undead, Race.Undead);
        
        AddRecipe(Race.Spirit, Race.Human, Race.Spirit);
        AddRecipe(Race.Spirit, Race.Beast, Race.Spirit);
        AddRecipe(Race.Spirit, Race.Demihuman, Race.Spirit);
        AddRecipe(Race.Spirit, Race.Demon, Race.Spirit);
        AddRecipe(Race.Spirit, Race.Dragon, Race.Spirit);
        AddRecipe(Race.Spirit, Race.Machine, Race.Machine);
        AddRecipe(Race.Spirit, Race.Plant, Race.Spirit);
        AddRecipe(Race.Spirit, Race.Spirit, Race.Spirit);
        AddRecipe(Race.Spirit, Race.Undead, Race.Undead);
        
        AddRecipe(Race.Undead, Race.Human, Race.Undead);
        AddRecipe(Race.Undead, Race.Beast, Race.Undead);
        AddRecipe(Race.Undead, Race.Demihuman, Race.Undead);
        AddRecipe(Race.Undead, Race.Demon, Race.Undead);
        AddRecipe(Race.Undead, Race.Dragon, Race.Undead);
        AddRecipe(Race.Undead, Race.Machine, Race.Undead);
        AddRecipe(Race.Undead, Race.Plant, Race.Undead);
        AddRecipe(Race.Undead, Race.Spirit, Race.Undead);
        AddRecipe(Race.Undead, Race.Undead, Race.Undead);

    }

    // A+B와 B+A를 모두 등록하여 순서 상관없이 작동하게 하는 헬퍼 함수
    private void AddRecipe(Race a, Race b, Race result)
    {
        raceCombinationTable[(a, b)] = result;
        raceCombinationTable[(b, a)] = result;
    }

    // 조합 결과 종족을 반환하는 함수
    public Race GetResultRace(Race a, Race b)
    {
        if (raceCombinationTable.TryGetValue((a, b), out Race result))
        {
            return result;
        }
        
        Debug.LogWarning($"조합식이 없습니다: {a} + {b}");
        return Race.Demon; 
    }

    // 두 몬스터의 ID를 받아 합체 결과 몬스터 데이터를 반환
    public MonsterEntry GetCompileResult(string monsterA_ID, string monsterB_ID)
    {
        // 선택된 두 몬스터의 데이터 가져오기
        MonsterEntry a = GetEntry(monsterA_ID);
        MonsterEntry b = GetEntry(monsterB_ID);

        if (a == null || b == null)
        {
            Debug.LogError("합체할 몬스터의 데이터를 찾을 수 없습니다.");
            return null;
        }

        // 조합표를 통해 결과 몬스터의 종족 결정
        Race targetRace = GetResultRace(a.race, b.race);

        // 두 몬스터의 평균 레벨 계산
        int avgLevel = Mathf.CeilToInt((a.stats.level + b.stats.level) / 2f);

        MonsterEntry resultEntry = entries
            .Where(m => m.race == targetRace && m.stats.level >= avgLevel) // 종족이 같고, 평균 레벨 이상인 것들 중
            .OrderBy(m => m.stats.level) // 레벨을 오름차순으로 정렬하여
            .FirstOrDefault(); // 가장 레벨이 낮은 데이터를 가져옴

        // 만약 평균 레벨 이상인 몬스터가 해당 종족에 없다면 해당 종족의 최고 레벨 몬스터를 반환
        if (resultEntry == null)
        {
            resultEntry = entries
                .Where(m => m.race == targetRace)
                .OrderByDescending(m => m.stats.level) // 레벨을 내림차순으로 정렬
                .FirstOrDefault();
        }

        return resultEntry;
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
            entries[i].id = $"enemy_{i.ToString("D3")}";
        }

#if UNITY_EDITOR
        // 스크립터블 오브젝트의 내용이 변경되었음을 유니티에 알려서, 
        // 에디터를 껐다 켜도 데이터가 저장되도록 합니다.
        EditorUtility.SetDirty(this);
        Debug.Log($"[MonsterDatabase] {entries.Count}개의 몬스터 ID가 000부터 순차적으로 재설정되었습니다.");
#endif
    }
}