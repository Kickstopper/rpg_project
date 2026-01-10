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

[System.Serializable]
public struct ResistanceData {
    public float physical; // 물리 내성 (1.0 = 100%)
    public float fire;     // 화염 내성
    public float ice;      // 빙결 내성
    public float elec;     // 전격 내성
    public float force;    // 염동 내성
    public float havoc;    // 정신 내성
}
