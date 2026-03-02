using UnityEngine;
using System.Collections.Generic;

namespace Data
{
    [CreateAssetMenu(fileName = "NewRegionTheme", menuName = "WorldMap/RegionTheme")]
    public class RegionTheme : ScriptableObject
    {
        [Header("기본 정보")]
        public string regionID;
        public string regionName;       // 화면 표시용 UI 텍스트

        [Header("환경 설정")]
        public BgmID fieldBgmID;        // BGM
        
        [Header("전투 설정")]
        public Sprite battleBackground; // 전투 Canvas에서 쓸 배경 이미지
        public BgmID battleBgmID;       // 지역 전용 전투 BGM
        public List<string> monsterList; // 출현하는 몬스터 ID
        public Vector3 startPosition;    // 월드맵이 열렸을 때 플레이어가 표시되는 위치
    }
}