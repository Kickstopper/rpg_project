namespace Data
{
    public class EventTriggerData
    {
        public string EventID;
        public bool Repeatable;
        public bool IsCompleted; // 이미 실행되었는지 여부 (런타임 상태)

        public EventTriggerData(string id, bool repeat)
        {
            EventID = id;
            Repeatable = repeat;
            IsCompleted = false;
        }
    }
}
