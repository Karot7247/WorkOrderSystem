using System.Collections.Generic;
using System.IO; // ✨ 必須加入這行以支援 Stream
using System.Threading.Tasks;
using WorkOrderSystem.Models; // 引用以使用 QuotationSelectItem

namespace WorkOrderSystem.Services
{
    public interface IQuotationFileService
    {
        // ==========================================
        //  原始功能 (保持完全不變，供網頁前端使用)
        // ==========================================

        /// <summary>
        /// 獲取符合條件的報價單列表，用於下拉選單 (Excel)。
        /// </summary>
        Task<List<QuotationSelectItem>> ListQuotationsAsync(bool filterByCustomer = false, string? customerName = null, int? year = null);

        /// <summary>
        /// 根據檔名 (不含副檔名) 查找 NAS 上的完整檔案路徑。
        /// </summary>
        Task<string?> FindQuotationFilePathAsync(string fileNameWithoutExtension);

        /// <summary>
        /// 將 Excel 轉換為 PDF Stream (供預覽或下載)。
        /// </summary>
        Task<(Stream? PdfStream, string? FileName)> GetQuotationAsPdfStreamAsync(string excelFilePath);


        // ==========================================
        //  ✨ 新功能 (供 LINE Bot 查詢使用，支援 PDF/Excel)
        // ==========================================

        /// <summary>
        /// 取得 NAS 中所有可用的年份 (例如 [112, 113, 114])。
        /// </summary>
        Task<List<int>> GetAvailableYearsAsync();

        /// <summary>
        /// 進階搜尋 (支援關鍵字、年份篩選、搜尋範圍、分頁)。
        /// </summary>
        /// <param name="keyword">關鍵字 (客戶名或工程名)</param>
        /// <param name="year">年份 (null 為全部)</param>
        /// <param name="scope">搜尋範圍</param>
        /// <param name="page">頁碼 (從1開始)</param>
        /// <param name="pageSize">每頁筆數</param>
        /// <returns>包含分頁資訊的搜尋結果</returns>
        Task<QuotationSearchResult> SearchQuotationsAsync(string keyword, int? year, SearchScope scope, int page, int pageSize);
        Task<(string? CustomerName, string? ContactPerson, string? ContactPhone)> GetContactInfoFromExcelAsync(string fileName);
    }

    // ✨ 定義搜尋結果 DTO (含分頁資訊)
    public class QuotationSearchResult
    {
        public List<QuotationSelectItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public bool HasNextPage { get; set; }
    }

    // ✨ 定義搜尋範圍列舉
   
}