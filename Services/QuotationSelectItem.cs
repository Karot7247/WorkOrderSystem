namespace WorkOrderSystem.Services // 或者你的 DTO 命名空間
{
    /// <summary>
    /// 用於工單詳情頁關聯報價單下拉選單的選項
    /// </summary>
    public class QuotationSelectItem
    {
        /// <summary>
        /// 儲存到 WorkOrder 的值 (不含副檔名的完整檔名)
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 在下拉選單中顯示的文字 (例如：檔名 - 工程名稱)
        /// </summary>
        public string DisplayText { get; set; } = string.Empty;
        /// <summary>
        /// 客戶名稱 (用於搜尋篩選)
        /// </summary>
        public string? CustomerName { get; set; }

        /// <summary>
        /// 工程名稱 (用於搜尋篩選)
        /// </summary>
        public string? ProjectName { get; set; }

        /// <summary>
        /// 報價單號 (用於排序)
        /// </summary>
        public string? QuotationNo { get; set; }
    }
}