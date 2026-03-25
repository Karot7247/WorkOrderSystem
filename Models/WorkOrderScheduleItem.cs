namespace WorkOrderSystem.Models // (請確保 namespace 與你的專案相符)
{
    public class WorkOrderScheduleItem
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Title { get; set; }
        public string Style { get; set; }
    }
}