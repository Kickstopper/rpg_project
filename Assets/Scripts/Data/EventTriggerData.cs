namespace Data
{
    public class EventTriggerData
    {
        public string EventID;
        public bool Repeatable;
        public bool IsCompleted; // 이미 실행되었는지 여부 (런타임 상태)
        public int ForceDir; // 방향 강제 (0:North, 1:East, 2:South, 3:West, -1:유지)

        public EventTriggerData(string id, bool repeat, int forceDir = -1)
        {
            EventID = id;
            Repeatable = repeat;
            IsCompleted = false;
            ForceDir = forceDir;
        }
    }
}
