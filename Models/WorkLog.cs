using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WorkOrderSystem.Data;

namespace WorkOrderSystem.Models
{
    public class WorkLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DisplayName("所屬工單")]
        public int WorkOrderId { get; set; }

        [Required]
        [DisplayName("工作日期")]
        public DateTime LogDate { get; set; }

        [Required(ErrorMessage = "日誌內容為必填項目")]
        [DisplayName("日誌內容")]
        public string Content { get; set; } = string.Empty;

        [Required]
        [DisplayName("花費時間(小時)")]
        [Column(TypeName = "decimal(5, 2)")]
        public decimal TimeSpent { get; set; }

        [DisplayName("其他參與人員(臨時)")]
        public string? CustomParticipants { get; set; }

        
        public string CreatedById { get; set; }

        [Required]
        [DisplayName("建立時間")]
        public DateTime CreatedAt { get; set; }

        // --- Navigation Properties ---

        [ForeignKey("WorkOrderId")]
        public virtual WorkOrder WorkOrder { get; set; }

        [ForeignKey("CreatedById")]
        public virtual AppUser CreatedBy { get; set; } // <--- 已修正

        public virtual ICollection<WorkLogPhoto> Photos { get; set; } = new List<WorkLogPhoto>();

        public virtual ICollection<WorkLogParticipant> Participants { get; set; } = new List<WorkLogParticipant>();
        public virtual ICollection<WorkLogHistory> Histories { get; set; } = new List<WorkLogHistory>();
    }
}