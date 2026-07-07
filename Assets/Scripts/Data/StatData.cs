[System.Serializable]
public struct StatData 
{
    public int level; // 레벨
    public int str; // 힘
    public int mag; // 마력
    public int intel; // 지력
    public int vit; // 체력
    public int agi; // 민첩
    public int luc; // 운
}

public enum ResistTier 
{ 
    Normal,   // 보통 (데미지 100%)
    Weak,     // 약점 (데미지 증폭)
    Resist,   // 내성 (데미지 감소)
    Null,     // 무효 (데미지 0)
    Repel,    // 반사 (데미지 반사)
    Drain     // 흡수 (데미지만큼 회복)
}

[System.Serializable]
public struct ResistanceData {
    public ResistTier phys; // 물리 내성
    public ResistTier fire;     // 화염 내성
    public ResistTier ice;      // 빙결 내성
    public ResistTier elec;     // 전격 내성
    public ResistTier force;    // 염동 내성
    public ResistTier psyche;    // 정신 내성

    public ResistTier GetResistanceTier(ElementType element)
{
    switch (element)
    {
        case ElementType.Physical:  return phys;
        case ElementType.Fire:      return fire;
        case ElementType.Ice:       return ice;
        case ElementType.Elec:      return elec;
        case ElementType.Force:     return force;
        case ElementType.Psyche:    return psyche;
        default:                    return ResistTier.Normal;
    }
}
}
