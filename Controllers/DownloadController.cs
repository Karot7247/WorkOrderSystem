using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using WorkOrderSystem.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using WorkOrderSystem.Data;
using System.Linq;
using System.Collections.Generic;

namespace WorkOrderSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DownloadController : ControllerBase
    {
        private readonly IQuotationFileService _quotationFileService;
        private readonly ILogger<DownloadController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly string _baseQuotationPath;

        public DownloadController(
            IQuotationFileService quotationFileService,
            ILogger<DownloadController> logger,
            IConfiguration configuration,
            IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _quotationFileService = quotationFileService;
            _logger = logger;
            _configuration = configuration;
            _dbFactory = dbFactory;

            _baseQuotationPath = configuration.GetValue<string>("QuotationSettings:NasBasePath")
                             ?? throw new InvalidOperationException("Quotation NAS base path ('QuotationSettings:NasBasePath') is not configured.");
        }

        // --- 1. 預覽報價單 (PDF) ---
        [HttpGet("preview/quotation.pdf")]
        public async Task<ActionResult<PdfPreviewResponse>> PreviewQuotationPdf([FromQuery] string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning("PreviewQuotationPdf: fileName 參數為空。");
                return BadRequest(new PdfPreviewResponse { ErrorMessage = "File name is required." });
            }

            _logger.LogInformation("PreviewQuotationPdf (Base64): 接收到檔名: {FileName}", fileName);

            string? decodedPath = await _quotationFileService.FindQuotationFilePathAsync(fileName);

            if (string.IsNullOrWhiteSpace(decodedPath))
            {
                return NotFound(new PdfPreviewResponse { ErrorMessage = $"File not found for: {fileName}.xlsx" });
            }

            // 安全性檢查
            if (!decodedPath.StartsWith(_baseQuotationPath, StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new PdfPreviewResponse { ErrorMessage = "Access denied." });
            }
            if (!decodedPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new PdfPreviewResponse { ErrorMessage = "Invalid file type." });
            }

            var (pdfStream, pdfFileName) = await _quotationFileService.GetQuotationAsPdfStreamAsync(decodedPath);

            if (pdfStream == null || pdfFileName == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new PdfPreviewResponse { ErrorMessage = "Unable to convert file to PDF." });
            }

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    await pdfStream.CopyToAsync(memoryStream);
                    pdfStream.Dispose();

                    byte[] pdfBytes = memoryStream.ToArray();
                    string base64Data = Convert.ToBase64String(pdfBytes);

                    var response = new PdfPreviewResponse
                    {
                        Base64Data = base64Data,
                        FileName = pdfFileName
                    };

                    return Ok(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PreviewQuotationPdf: 將 PDF Stream 轉為 Base64 時發生錯誤。");
                return StatusCode(StatusCodes.Status500InternalServerError, new PdfPreviewResponse { ErrorMessage = "Error processing PDF data." });
            }
        }

        // --- 2. 下載日誌照片 (ZIP 打包) ---
        [HttpGet("WorkLogPhotos/{logId}")]
        public async Task<IActionResult> DownloadWorkLogPhotos(int logId)
        {
            try
            {
                using var context = await _dbFactory.CreateDbContextAsync();

                // 載入日誌、照片以及工單資訊 (用於檔名)
                var log = await context.WorkLogs
                    .Include(l => l.Photos)
                    .Include(l => l.WorkOrder)
                    .FirstOrDefaultAsync(l => l.Id == logId);

                if (log == null || log.Photos == null || !log.Photos.Any())
                {
                    return Content("此日誌沒有照片可供下載。", "text/plain; charset=utf-8");
                }

                var uploadsRoot = _configuration["FileStorageSettings:UploadsRootPath"];
                if (string.IsNullOrEmpty(uploadsRoot))
                {
                    return StatusCode(500, "伺服器設定錯誤：找不到檔案儲存路徑 (UploadsRootPath)。");
                }

                var memoryStream = new MemoryStream();
                // 記錄已加入的檔名，避免重複檔名導致 ZIP 錯誤
                var addedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var photo in log.Photos)
                    {
                        try
                        {
                            // --- 路徑處理邏輯 ---
                            // 1. 去除開頭的斜線
                            var relativePath = photo.FilePath.TrimStart('/', '\\');

                            // 2. 智慧修正：如果路徑以 "uploads/" 開頭，嘗試移除它
                            // 因為 uploadsRoot 通常已經指向 ".../wwwroot/uploads"
                            if (relativePath.StartsWith("uploads", StringComparison.OrdinalIgnoreCase))
                            {
                                relativePath = relativePath.Substring(7).TrimStart('/', '\\');
                            }

                            // 3. 組合實體路徑
                            var localPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
                            var fullPath = Path.Combine(uploadsRoot, localPath);

                            // 4. 檢查檔案是否存在
                            if (System.IO.File.Exists(fullPath))
                            {
                                // 決定 ZIP 內的檔名 (優先使用原始檔名，否則用實體檔名)
                                var entryName = !string.IsNullOrEmpty(photo.FileName) ? photo.FileName : Path.GetFileName(fullPath);

                                // 處理重複檔名
                                while (addedEntryNames.Contains(entryName))
                                {
                                    var ext = Path.GetExtension(entryName);
                                    var name = Path.GetFileNameWithoutExtension(entryName);
                                    // 加入隨機碼避免重複
                                    entryName = $"{name}_{Guid.NewGuid().ToString().Substring(0, 4)}{ext}";
                                }
                                addedEntryNames.Add(entryName);

                                archive.CreateEntryFromFile(fullPath, entryName);
                            }
                            else
                            {
                                _logger.LogWarning("打包 ZIP 時找不到實體檔案 (DB ID: {Id})。搜尋路徑: {Path}", photo.Id, fullPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "打包單張照片失敗: {Path}", photo.FilePath);
                        }
                    }
                }

                // 如果 ZIP 是空的 (所有檔案都找不到)，回傳錯誤提示
                if (memoryStream.Length == 0)
                {
                    return Content("錯誤：雖然資料庫有記錄，但伺服器找不到實體照片檔案，可能是路徑設定有誤。", "text/plain; charset=utf-8");
                }

                memoryStream.Position = 0;

                // --- 設定下載檔名 ---
                // 格式：客戶名稱_工程名稱_日誌日期.zip
                var customerName = SanitizeFileName(log.WorkOrder?.CustomerName ?? "UnknownCustomer");
                var projectTitle = SanitizeFileName(log.WorkOrder?.Title ?? "UnknownProject");
                var logDate = log.LogDate.ToString("yyyyMMdd");

                var zipFileName = $"{customerName}_{projectTitle}_{logDate}.zip";

                return File(memoryStream, "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下載日誌照片 ZIP 時發生未預期錯誤。LogId: {LogId}", logId);
                return StatusCode(500, "下載失敗，請稍後再試。");
            }
        }

        // --- 輔助方法：移除檔名中的非法字元 ---
        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Unknown";
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }

    public class PdfPreviewResponse
    {
        public string? Base64Data { get; set; }
        public string? FileName { get; set; }
        public string? ErrorMessage { get; set; }
    }
}