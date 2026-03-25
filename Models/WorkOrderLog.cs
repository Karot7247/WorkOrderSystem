using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WorkOrderSystem.Data;

namespace WorkOrderSystem.Models
{
    public class WorkOrderLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Action { get; set; } = string.Empty; // 例如: "建立工單", "指派給王大明"

        public string? Comment { get; set; } // 備註

        public DateTime Timestamp { get; set; } = DateTime.Now;

        // 關聯
        public int WorkOrderId { get; set; }
        [ForeignKey("WorkOrderId")]
        public virtual WorkOrder WorkOrder { get; set; } = null!;

        public string? OperatorId { get; set; }
        [ForeignKey("OperatorId")]
        public virtual AppUser? Operator { get; set; }
    }
}