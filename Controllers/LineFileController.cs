using Microsoft.AspNetCore.Mvc;
using System.Net; // 為了 WebUtility

namespace WorkOrderSystem.Controllers
{
    [ApiController]
    [Route("api/line/files")]
    public class LineFileController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<LineFileController> _logger;

        public LineFileController(IConfiguration config, ILogger<LineFileController> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpGet("download/{fileName}")]
        public IActionResult DownloadQuotation(string fileName)
        {
            try
            {
                // 1. 解碼檔名
                var decodedFileName = WebUtility.UrlDecode(fileName);
                var nasRootPath = _config["FileStorageSettings:QuotationsRootPath"]; // 或 QuotationSettings:NasBasePath

                if (string.IsNullOrEmpty(nasRootPath)) return StatusCode(500, "未設定檔案路徑");

                // 2. 模糊搜尋檔案 (因為傳進來的可能是 "檔名.pdf" 或 "檔名.xlsx")
                var safeFileName = Path.GetFileName(decodedFileName);

                // 去掉副檔名再搜尋，以防萬一
                var fileNameNoExt = Path.GetFileNameWithoutExtension(safeFileName);
                var searchPattern = $"{fileNameNoExt}*";

                var files = Directory.GetFiles(nasRootPath, searchPattern, SearchOption.AllDirectories);

                if (files.Length == 0)
                {
                    _logger.LogWarning($"找不到檔案: {safeFileName}");
                    return NotFound("找不到檔案");
                }

                // 取第一個找到的檔案
                var filePath = files[0];
                var fileExtension = Path.GetExtension(filePath).ToLower();
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var downloadName = Path.GetFileName(filePath);

                // 3. 設定 Content-Type
                string contentType = "application/octet-stream";
                if (fileExtension == ".pdf") contentType = "application/pdf";
                else if (fileExtension == ".xlsx") contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                else if (fileExtension == ".xls") contentType = "application/vnd.ms-excel";

                // ✨✨✨ 關鍵修改：PDF 用預覽，Excel 用下載 ✨✨✨
                if (fileExtension == ".pdf")
                {
                    // 設定 Header 為 Inline (在瀏覽器開啟)
                    // 注意：這裡需要處理中文檔名的編碼，否則 Chrome 標題可能會變亂碼
                    var encodedFilename = WebUtility.UrlEncode(downloadName);
                    Response.Headers.Add("Content-Disposition", $"inline; filename*=UTF-8''{encodedFilename}");

                    // 回傳檔案 (不帶第三個參數，讓瀏覽器自己決定，配合上面的 Header 會變成預覽)
                    return File(fileBytes, contentType);
                }
                else
                {
                    // Excel 強制下載 (帶入第三個參數 downloadName，ASP.NET Core 會自動設為 attachment)
                    return File(fileBytes, contentType, downloadName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下載失敗");
                return StatusCode(500, "下載發生錯誤");
            }
        }
    }
}