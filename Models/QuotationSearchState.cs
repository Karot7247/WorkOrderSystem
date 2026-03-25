namespace WorkOrderSystem.Models
{
    public class QuotationSearchState
    {
        public string Keyword { get; set; } = string.Empty;
        public int? SelectedYear { get; set; }
        public SearchScope Scope { get; set; } = SearchScope.All; // 使用下方的 Enum
        public int CurrentPage { get; set; } = 1;
    }

    // ✨ 請確認 SearchScope 定義在這裡 (且是 public)
    public enum SearchScope
    {
        All = 0,
        Customer = 1,
        Project = 2
    }
}