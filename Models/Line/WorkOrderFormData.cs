namespace WorkOrderSystem.Models.Line
{
    public class WorkOrderFormData
    {
        public string? CustomerName { get; set; }
        public string? ContactPerson { get; set; }
        public string? ContactPhone { get; set; }
        public string? Description { get; set; }
        public List<string> PhotoPaths { get; set; } = new List<string>(); // ✨ 新增：用來儲存照片檔案路徑
    }
}