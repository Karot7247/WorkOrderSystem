using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WorkOrderSystem.Data;

namespace WorkOrderSystem.Models
{
    // 草稿主檔：記錄暫存的施工內容
    public class FieldRecordDraft
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "標題")]
        public string Title { get; set; } // 例如：12/15 台積電維修暫存

        [Display(Name = "施工備註")]
        public string? EngineerNotes { get; set; }

        [Display(Name = "客戶簽名")]
        public string? ClientSignature { get; set; } // Base64 字串

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // --- 關鍵欄位：記錄是誰建立的 ---
        [Required]
        public string CreatorId { get; set; }
        public int? ImportedWorkOrderId { get; set; }

        [ForeignKey("CreatorId")]
        public virtual AppUser Creator { get; set; }

        // 關聯：一個草稿有多張照片
        public virtual ICollection<FieldRecordDraftPhoto> Photos { get; set; } = new List<FieldRecordDraftPhoto>();
    }

    // 草稿照片檔
    public class FieldRecordDraftPhoto
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FilePath { get; set; } // 照片在 NAS/Server 上的路徑

        [Display(Name = "分類")]
        public string Category { get; set; } = "未分類"; // 施工前/中/後

        public string? CustomNote { get; set; } // 照片備註

        // 關聯回主檔
        public int FieldRecordDraftId { get; set; }

        [ForeignKey("FieldRecordDraftId")]
        public virtual FieldRecordDraft FieldRecordDraft { get; set; }
    }
}