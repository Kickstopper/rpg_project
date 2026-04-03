namespace Data
{
    public enum Personality { Polite, Aggressive, Sly, Foolish, Childish }
    public enum Gender { Male, Female, Genderless }
    public enum Race { Human, Beast, Fairy, Angel, Robot, Undead }

    public enum TimeOfDay { Morning, Day, Evening, Night }
    public enum Weather { Clear, Rain, Storm }
    public enum MoonPhase { New, Half, Full }
    public enum ChoiceTone { Friendly, Aggressive, Logical, Bribe, Flirt }

    // 환경 상태를 통째로 담아 계산기에 던져줄 구조체
    [System.Serializable]
    public struct EnvironmentState
    {
        public MoonPhase moonPhase;
        public Weather weather;
        // 필요하다면 던전의 속성(화산, 빙하 등)도 여기에 추가 가능
    }

    // 계산 결과(기분 변화량)를 담을 구조체
    public struct MoodDelta
    {
        public int addedAnger;
        public int addedJoy;
        public int addedInterest;

        public MoodDelta(int anger, int joy, int interest)
        {
            this.addedAnger = anger;
            this.addedJoy = joy;
            this.addedInterest = interest;
        }
    }
}