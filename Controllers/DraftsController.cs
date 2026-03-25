using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkOrderSystem.Data;
using WorkOrderSystem.Models;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace WorkOrderSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 確保只有登入者能呼叫
    public class DraftsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;

        public DraftsController(ApplicationDbContext context, UserManager<AppUser> userManager, IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
        }

        // 定義接收資料的 DTO (Data Transfer Object)
        public class DraftUploadDto
        {
            public string Title { get; set; }
            public string Address { get; set; }
            public List<DraftPhotoDto> Photos { get; set; }
        }

        public class DraftPhotoDto
        {
            public string ImageDataUrl { get; set; } // Base64
            public string Notes { get; set; }
            public string Category { get; set; }
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateDraft([FromBody] DraftUploadDto dto)
        {
            try
            {
                // 1. 取得當前使用者
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized("無法識別使用者");

                // 2. 建立 Draft 物件
                var draft = new FieldRecordDraft
                {
                    Title = dto.Title,
                    EngineerNotes = $"位置: {dto.Address}",
                    CreatorId = user.Id,
                    CreatedAt = DateTime.Now
                };

                // 3. 處理照片 (寫入 NAS/Server 資料夾)
                var uploadsRoot = _configuration["FileStorageSettings:UploadsRootPath"];
                if (string.IsNullOrEmpty(uploadsRoot)) return StatusCode(500, "伺服器未設定上傳路徑");

                // 建立專屬資料夾: uploads/drafts/{UserId}/{DateString}/
                var draftFolder = Path.Combine(uploadsRoot, "drafts", user.Id, DateTime.Now.ToString("yyyyMMdd"));
                Directory.CreateDirectory(draftFolder);

                foreach (var photoDto in dto.Photos)
                {
                    var uniqueFileName = $"{Guid.NewGuid()}.jpg";
                    var savePath = Path.Combine(draftFolder, uniqueFileName);

                    // Base64 轉 Byte[]
                    // 前端傳來的可能是 "data:image/jpeg;base64,....."，需切除前綴
                    var base64Data = photoDto.ImageDataUrl.Contains(",")
                        ? photoDto.ImageDataUrl.Split(',')[1]
                        : photoDto.ImageDataUrl;

                    var bytes = Convert.FromBase64String(base64Data);
                    await System.IO.File.WriteAllBytesAsync(savePath, bytes);

                    // 設定資料庫路徑 (相對路徑)
                    var dbPath = $"/uploads/drafts/{user.Id}/{DateTime.Now:yyyyMMdd}/{uniqueFileName}";

                    draft.Photos.Add(new FieldRecordDraftPhoto
                    {
                        FilePath = dbPath,
                        Category = photoDto.Category ?? "施工前",
                        CustomNote = photoDto.Notes
                    });
                }

                // 4. 存入資料庫
                _context.FieldRecordDrafts.Add(draft);
                await _context.SaveChangesAsync();

                return Ok(new { message = "暫存建立成功", draftId = draft.Id });
            }
            catch (Exception ex)
            {
                // 記錄錯誤 (Log)
                return StatusCode(500, $"建立暫存失敗: {ex.Message}");
            }
        }
    }
}