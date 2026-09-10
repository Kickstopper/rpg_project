using System.Collections;
using RPGProject.Feature.Battle;

namespace RPGProject.Tests.Battle
{
    public sealed class BattleTestEntity : BattleEntity
    {
        public int uiUpdates;
        public int agility;
        public int luck;
        public override IEnumerator OnDamageTaken(int damage) { currentHp -= damage; yield break; }
        protected override void UpdateUI() { uiUpdates++; }
        public override int GetTotalStr() => 1;
        public override int GetTotalAgi() => agility;
        public override int GetTotalMag() => 1;
        public override int GetTotalLuc() => luck;
        public override int GetTotalVit() => 1;
        public override int GetTotalInt() => 1;
        public override int GetAttack() => 1;
        public override int GetDefense() => 1;
        public override int GetHitRate() => 100;
        public override int GetEvasion() => 0;
        public override int GetMagicAttack() => 1;
        public override int GetMagicDefense() => 1;
        public override ResistanceData GetResistances() => default;
        public override void SetSelectionState(bool selected) { }
        public override VfxID GetBasicAttackVfx() => VfxID.None;
        public override VfxID GetGunAttackVfx() => VfxID.None;
    }
}
