using UnityEngine;
using System.Collections.Generic;

namespace Data
{
    public enum SfxID
    {
        None,       // 없음
        UI_Cursor,  // 커서 이동
        UI_Cancel, // 취소 버튼 클릭
        UI_Click,   // 버튼 클릭
        UI_Hover,   // 마우스 올림
        Footstep,   // 발자국
        Bump_Wall,  // 벽 부딪힘
        Attack_Sword, // 칼 공격
        Attack_Critical,    // 크리티컬 공격
        Typing,         // 타이핑 소리
        Explosion,      // 폭발
        Encounter,      // 적과의 인카운터
        Monster_Dead_M, // 몬스터 죽음 (남)
        Monster_Dead_F, // 몬스터 죽음 (여)
        Attack_Gun,     // 총 공격
        Attack_Magic,   // 마법 공격
        PC_Boot,        // 컴퓨터 부팅
        Slide_Door,     // 슬라이드 도어 열림
        Jump,           // 위층으로 올라가는 소리
        Fall,           // 아래층으로 떨어진 소리
        Spash,          // 물에 빠지는 소리
    }

    public enum BgmID
    {
        None,
        Encounter,
        Normal_Battle,
        Fierce_Battle,
        Boss_Battle,
        Victory,
        Fusion,
        Dungeon_0,
        Dungeon_1,
        Dungeon_2,
        Dungeon_3,
        Dungeon_4,
        Dungeon_5,
        Dungeon_6,
        Dungeon_7,
        Dungeon_8,
        Dungeon_9,
        Dungeon_10,
        Dungeon_11,
        Dungeon_12,
        Dungeon_13,
        Dungeon_14,
        Dungeon_15,
        Dungeon_16,
        Dungeon_17,
        Dungeon_18,
        WorldMap,
        Intro,
        WeaponShop,
        ArmorShop,
        ItemShop,
        HealShop,
        Laboatory,
        Terminal,
        LevelUp,
        Title,
        
    }

    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Audio/AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        // 데이터 짝꿍 구조체 (ID + 파일)
        [System.Serializable]
        public struct AudioData
        {
            public SfxID id;
            public AudioClip clip;
            // 팁: 같은 '공격'이라도 소리가 여러 개면 AudioClip[] clips; 로 배열을 써서 랜덤 재생도 가능
        }

        [System.Serializable]
        public struct BgmData
        {
            public BgmID id;
            public AudioClip clip;
        }

        [Header("등록된 사운드 목록")]
        public List<AudioData> AudioList;

        [Header("등록된 배경음악 목록")]
        public List<BgmData> BgmList;

        // 딕셔너리: 검색 속도를 빠르게 하기 위해 리스트를 변환할 통
        private Dictionary<SfxID, AudioClip> AudioDictionary;
        private Dictionary<BgmID, AudioClip> BgmDictionary;

        public void Initialize()
        {
            AudioDictionary = new Dictionary<SfxID, AudioClip>();
            foreach (var data in AudioList)
            {
                if (data.id != SfxID.None && data.clip != null)
                {
                    // 중복 방지 체크 후 추가
                    if (!AudioDictionary.ContainsKey(data.id))
                        AudioDictionary.Add(data.id, data.clip);
                }
            }

            BgmDictionary = new Dictionary<BgmID, AudioClip>();
            foreach (var data in BgmList)
            {
                if (data.id != BgmID.None && data.clip != null)
                {
                    // 중복 방지 체크 후 추가
                    if (!BgmDictionary.ContainsKey(data.id))
                        BgmDictionary.Add(data.id, data.clip);
                }
            }
        }

        // ID로 클립을 찾아주는 함수
        public AudioClip GetSfxClip(SfxID id)
        {
            if (AudioDictionary == null) Initialize();

            if (AudioDictionary.TryGetValue(id, out AudioClip clip))
            {
                return clip;
            }
            
            Debug.LogWarning($"[AudioLibrary] 사운드 ID '{id}'를 찾을 수 없습니다!");
            return null;
        }

        public AudioClip GetBgmClip(BgmID id)
        {
            if (AudioDictionary == null) Initialize();

            if (BgmDictionary.TryGetValue(id, out AudioClip clip))
            {
                return clip;
            }
            
            Debug.LogWarning($"[AudioLibrary] 배경 음악 ID '{id}'를 찾을 수 없습니다!");
            return null;
        }
    }
}
