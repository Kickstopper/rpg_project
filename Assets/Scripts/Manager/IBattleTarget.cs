namespace Manager
{
    // EffectManager가 데이터를 조작하기 위한 인터페이스
    public interface IBattleTarget
    {
        // 상태 확인
        bool IsAlive { get; }
        bool IsMaxHp { get; }
        bool IsMaxMp { get; }

        // 순수 데이터 값 (Getter)
        int CurrentHp { get; }
        int MaxHp { get; }
        int CurrentMp { get; }
        int MaxMp { get; }

        // 데이터 변경 메서드 (Model 수정)
        void ApplyHpChange(int amount);
        void ApplyMpChange(int amount);
        void ApplyRevive(int percent); // 퍼센트 부활
        
        // 변경 사항을 UI에 반영하라고 지시 (View 갱신)
        void RefreshView();
    }
}