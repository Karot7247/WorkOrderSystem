namespace WorkOrderSystem.ViewModels.Reports
{
    // 這個類別代表最終要在頁面上顯示的、已分組的資料結構
    public class EngineerReportViewModel
    {
        public string EngineerId { get; set; }
        public string EngineerName { get; set; }
        public int TotalWorkOrders => WorkOrders.Count;
        public int PrimaryWorkOrders => WorkOrders.Count(wo => wo.IsPrimary);
        public int AssistingWorkOrders => WorkOrders.Count(wo => !wo.IsPrimary);
        public List<EngineerWorkOrderViewModel> WorkOrders { get; set; } = new List<EngineerWorkOrderViewModel>();
    }

    // 這個類別代表報表表格中的每一筆工單紀錄
    public class EngineerWorkOrderViewModel
    {
        public int WorkOrderId { get; set; }
        public string CustomerName { get; set; }
        public string Title { get; set; }
        public bool IsPrimary { get; set; }
        public string Status { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}