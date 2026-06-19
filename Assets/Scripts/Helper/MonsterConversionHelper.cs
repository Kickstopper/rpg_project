using System.Collections.Generic;
using Data.Database;
using Data;

namespace Helper
{
    public static class MonsterConversionHelper
    {
        /// <summary>
        /// MonsterEntry를 런타임용 CharacterEntry로 변환하여 반환합니다.
        /// </summary>
        public static CharacterDatabase.CharacterEntry ToCharacterEntry(this MonsterDatabase.MonsterEntry monster)
        {
            var charEntry = new CharacterDatabase.CharacterEntry();

            // 기본 식별자 및 정보 매핑
            charEntry.id = monster.id;
            charEntry.name = monster.name;
            charEntry.gender = monster.gender;
            charEntry.align = monster.align;
            
            charEntry.isCommander = false; 
            charEntry.isRegular = false;

            charEntry.stats = monster.stats; 
            charEntry.resistances = monster.resistances;

            // 스킬 세팅
            charEntry.initialSkills = new List<SkillData>();
            if (monster.skills != null)
            {
                charEntry.initialSkills.AddRange(monster.skills);
            }

            // 비주얼 매핑
            charEntry.portraitImage = monster.portrait;
            charEntry.battlePortraitImg = monster.portrait;
            
            if (monster.image != null && monster.image.Length > 0)
            {
                charEntry.standingImage = monster.image[0]; // 몬스터의 첫 번째 스프라이트를 스탠딩으로 사용
            }

            // 장비 슬롯 비우기
            charEntry.initialWeaponId = "";
            charEntry.initialGunId = "";
            charEntry.initialAmmoId = "";
            charEntry.initialArmorIds = new List<string>();

            // 경험치 테이블
            // 몬스터 전용 경험치 테이블이 있다면 여기서 할당
            // charEntry.expTable = ...; 

            return charEntry;
        }
    }
}