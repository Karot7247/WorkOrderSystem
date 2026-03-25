using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WorkOrderSystem.Data;

namespace WorkOrderSystem.Models
{
    public class WorkOrder
    {
        [Key]
        public int Id { get; set; }
        public string? GeneratedPdfPath { get; set; }
        [Required(ErrorMessage = "請輸入標題")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入詳細說明")]
        public string Description { get; set; } = string.Empty;

        public Guid? PublicAccessToken { get; set; }

        public string Status { get; set; } = "待指派"; // 初始狀態
        [Required(ErrorMessage = "請輸入客戶名稱")]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(50)]
        public string? ContactPhone { get; set; }

        [Required]
        public string WorkOrderType { get; set; } = "維修"; // 案件類型，預設為 "維修"
        [MaxLength(50)]
        public string? Source { get; set; } // 客源分類

        [MaxLength(255)]
        public string? Address { get; set; } // 地址
                                             // ▼▼▼ [新增] 場堪定位地址 (隱藏欄位) ▼▼▼
        [Display(Name = "場堪定位地址")]
        public string? SurveyAddress { get; set; }

        // 關聯到申請人
        public string? RequesterId { get; set; }
        [ForeignKey("RequesterId")]
        public virtual AppUser? Requester { get; set; }
        public string? LineRequesterDisplayName { get; set; }

        // 關聯到指派的工程師
       

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        // 業主電子簽名 (Base64字串)
        public string? ClientSignature { get; set; }

        // 工程師施工備註 (可選)
        public string? EngineerNotes { get; set; }
        public DateTime? AssignmentDate { get; set; } // 任務指派的當下時間
        public DateTime? ScheduledStartDate { get; set; } // 預計施工開始日期
        public DateTime? ScheduledEndDate { get; set; } // 預計施工結束日期

        // 關聯到此工單的照片和日誌
        public virtual ICollection<WorkOrderPhoto> Photos { get; set; } = new List<WorkOrderPhoto>();
        public virtual ICollection<WorkOrderLog> Logs { get; set; } = new List<WorkOrderLog>();
        public virtual ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();
        public virtual ICollection<WorkOrderAssignee> Assignments { get; set; } = new List<WorkOrderAssignee>();

        [MaxLength(100)] // 檔名可能稍長，增加長度限制
        public string? RelatedQuotationFileName { get; set; } // 儲存不含副檔名的完整檔名

       

    }

}