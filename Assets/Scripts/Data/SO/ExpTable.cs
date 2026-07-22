using UnityEngine;
using System.Collections.Generic;

namespace Data
{
    [System.Serializable]
    public class GenderExpFactor
    {
        public Gender gender = Gender.None;
        
        [Tooltip("성별에 따른 최종 경험치 요구량 배율 (1.0이 기본)")]
        [Range(0.5f, 3.0f)] 
        public float multiplier = 1.0f; // 인스펙터 오류 방지용 기본값
    }

    [System.Serializable]
    public class RaceGrowthProfile
    {
        public Race race = Race.Human;

        [Header("Base Formula")]
        public float baseExp = 100f;
        [Range(1f, 5f)] 
        public float exponent = 2.2f;

        [Header("Detailed Pacing Curve")]
        [Tooltip("X축: 레벨 (1~99), Y축: 경험치 배율 (기본 1.0)\n그래프를 아래로 꺾으면(예: 0.5) 그 구간의 성장이 빨라지고, 위로 꺾으면(예: 2.0) 성장이 정체됩니다.")]
        public AnimationCurve pacingCurve = AnimationCurve.Constant(1f, 99f, 1f);
    }

    [CreateAssetMenu(fileName = "New Exp Table", menuName = "RPG/Exp Table")]
    public class ExpTable : ScriptableObject
    {
        [Header("Base Settings")]
        public int maxLevel = 99;

        [Header("Fallback Default Growth")]
        [Tooltip("목록에 없는 종족일 경우 기본으로 적용될 수치")]
        public float defaultBaseExp = 100f;
        public float defaultExponent = 2.2f;

        [Header("Race & Gender Profiles")]
        public List<RaceGrowthProfile> raceProfiles = new List<RaceGrowthProfile>();
        public List<GenderExpFactor> genderFactors = new List<GenderExpFactor>();

        // 레벨, 종족, 성별을 모두 받아 최종 요구 경험치를 계산
        public int GetRequiredExp(int level, Race race, Gender gender)
        {
            if (level >= maxLevel) return 99999999; 

            float currentBaseExp = defaultBaseExp;
            float currentExponent = defaultExponent;
            float pacingMultiplier = 1.0f;

            // 종족별 성장 프로필 찾기
            var profile = raceProfiles.Find(p => p.race == race);
            if (profile != null)
            {
                currentBaseExp = profile.baseExp;
                currentExponent = profile.exponent;
                
                // 그래프에서 현재 레벨(X축)에 해당하는 배율(Y축)을 추출
                pacingMultiplier = profile.pacingCurve.Evaluate(level); 
            }

            // 기본 공식 계산 (Base * Level^Exponent)
            float rawExp = currentBaseExp * Mathf.Pow(level, currentExponent);

            // 종족별 구간 보정치 (Pacing Curve) 적용
            rawExp *= pacingMultiplier;

            // 성별별 최종 배율 적용
            float gMultiplier = 1.0f;
            int gIndex = genderFactors.FindIndex(x => x.gender == gender);
            if (gIndex >= 0) gMultiplier = genderFactors[gIndex].multiplier;

            // Curve를 0으로 꺾거나 배율이 너무 낮아져서 0 이하의 값이 나오는 것을 방지하기 위해 최소치가 1이 되게 함.
            int finalExp = Mathf.FloorToInt(rawExp * gMultiplier);
            return Mathf.Max(1, finalExp); 
        }
    }
}