using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkOrderSystem.Data;
using WorkOrderSystem.Models;
using System.Security.Claims; // 🔥 1. 務必加入這個引用

namespace WorkOrderSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkOrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WorkOrdersController> _logger;

        public WorkOrdersController(ApplicationDbContext context, IConfiguration configuration, ILogger<WorkOrdersController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // GET: api/WorkOrders/list?mode=survey&engineerId=xxxx
        [HttpGet("list")]
        public async Task<ActionResult<IEnumerable<SurveyWorkOrderDto>>> GetWorkOrders(
            [FromQuery] string mode = "survey",
            [FromQuery] string? engineerId = null)
        {
            // 建立查詢
            var query = _context.WorkOrders.AsNoTracking();

            // 🔥 2. 決定要用哪個 ID 來過濾
            // 優先順序：前端傳來的 ID -> 當前登入者的 ID
            var targetUserId = engineerId;

            if (string.IsNullOrEmpty(targetUserId))
            {
                // 如果前端沒傳，嘗試從後端 Session/Cookie 抓取當前登入者
                targetUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                               ?? User.FindFirstValue("sub");
            }

            // 🔥 3. 安全性檢查：如果最後還是不知道是誰，就回傳空列表 (不要回傳全部！)
            if (string.IsNullOrEmpty(targetUserId))
            {
                // 除非是管理員模式想要看全部，否則預設不給資料
                // 這裡簡單處理：沒 ID 就回傳空陣列
                return Ok(new List<SurveyWorkOrderDto>());
            }

            // 🔥 4. 執行過濾 (檢查 Assignments 列表)
            query = query.Where(w => w.Assignments.Any(a => a.UserId == targetUserId));

            // --- 模式過濾 ---
            if (mode == "construction")
            {
                // 施工回報模式：抓 "施工" 或 "維修" 且未結案的
                query = query.Where(w => (w.WorkOrderType == "施工" || w.WorkOrderType == "維修") && w.Status != "已完成");
            }
            else
            {
                // 場堪模式 (預設)：只抓 "報價"
                query = query.Where(w => w.WorkOrderType == "報價");
            }

            var orders = await query
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new SurveyWorkOrderDto
                {
                    Id = w.Id,
                    Title = w.Title,
                    CustomerName = w.CustomerName,
                    Address = w.Address
                })
                .ToListAsync();

            return orders;
        }

        // POST: api/WorkOrders/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadSurveyData([FromBody] SurveyUploadDto uploadData)
        {
            if (uploadData == null || uploadData.WorkOrderId == 0) return BadRequest("無效的資料");

            var workOrder = await _context.WorkOrders
                .Include(w => w.Logs)
                .FirstOrDefaultAsync(w => w.Id == uploadData.WorkOrderId);

            if (workOrder == null) return NotFound("找不到工單");

            var uploadsRoot = _configuration["FileStorageSettings:UploadsRootPath"];
            if (string.IsNullOrEmpty(uploadsRoot)) return StatusCode(500, "存檔路徑未設定");

            try
            {
                if (uploadData.UploadMode == "survey" && !string.IsNullOrWhiteSpace(uploadData.Address))
                {
                    if (string.IsNullOrWhiteSpace(workOrder.Address))
                    {
                        workOrder.Address = uploadData.Address;
                    }
                    else if (workOrder.Address != uploadData.Address)
                    {
                        workOrder.SurveyAddress = uploadData.Address;
                    }
                }

                var folderName = uploadData.UploadMode == "construction" ? "施工照片" : "場堪照片";
                var categoryName = uploadData.UploadMode == "construction" ? "施工照片" : "場堪照片";

                var targetDirectory = Path.Combine(uploadsRoot, SanitizeFolderName(workOrder.CustomerName), SanitizeFolderName(workOrder.Title), folderName);
                Directory.CreateDirectory(targetDirectory);

                int savedCount = 0;
                foreach (var photo in uploadData.Photos)
                {
                    if (string.IsNullOrEmpty(photo.ImageDataUrl)) continue;

                    string extension = ".jpg";
                    string base64Data = photo.ImageDataUrl;

                    if (base64Data.Contains("data:image/png")) { extension = ".png"; base64Data = base64Data.Replace("data:image/png;base64,", ""); }
                    else if (base64Data.Contains("data:image/jpeg")) { extension = ".jpg"; base64Data = base64Data.Replace("data:image/jpeg;base64,", ""); }
                    else { var parts = base64Data.Split(','); if (parts.Length > 1) base64Data = parts[1]; }

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var savePath = Path.Combine(targetDirectory, uniqueFileName);

                    await System.IO.File.WriteAllBytesAsync(savePath, Convert.FromBase64String(base64Data));

                    var dbPath = $"/uploads/{SanitizeFolderName(workOrder.CustomerName)}/{SanitizeFolderName(workOrder.Title)}/{folderName}/{uniqueFileName}".Replace('\\', '/');

                    _context.WorkOrderPhotos.Add(new WorkOrderPhoto
                    {
                        WorkOrderId = workOrder.Id,
                        FilePath = dbPath,
                        Category = categoryName,
                        CustomNote = photo.Notes ?? ""
                    });
                    savedCount++;
                }

                workOrder.Logs.Add(new WorkOrderLog
                {
                    WorkOrderId = workOrder.Id,
                    OperatorId = workOrder.RequesterId,
                    Action = uploadData.UploadMode == "construction" ? "上傳施工回報" : "上傳場堪資料",
                    Comment = $"App 上傳 {savedCount} 張照片"
                });

                await _context.SaveChangesAsync();
                return Ok(new { count = savedCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上傳失敗");
                return StatusCode(500, ex.Message);
            }
        }

        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Uncategorized";
            foreach (char c in Path.GetInvalidFileNameChars()) { name = name.Replace(c, '_'); }
            return name.Trim();
        }
    }

    public class SurveyWorkOrderDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? CustomerName { get; set; }
        public string Address { get; set; }
    }

    public class SurveyUploadDto
    {
        public int WorkOrderId { get; set; }
        public string Address { get; set; }
        public string UploadMode { get; set; } = "survey";
        public List<SurveyPhotoDto> Photos { get; set; }
    }
    public class SurveyPhotoDto { public string ImageDataUrl { get; set; } public string Notes { get; set; } }
}