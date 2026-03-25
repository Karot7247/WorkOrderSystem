using System.ComponentModel.DataAnnotations.Schema;
using WorkOrderSystem.Data;

namespace WorkOrderSystem.Models
{
    public class WorkOrderAssignee
    {
        // 複合主鍵 (由 WorkOrderId 和 UserId 組成)
        public int WorkOrderId { get; set; }
        public string UserId { get; set; }

        // 新增一個旗標，用來標示是否為主要負責人
        public bool IsPrimary { get; set; } = false;

        // --- Navigation Properties ---
        [ForeignKey("WorkOrderId")]
        public virtual WorkOrder WorkOrder { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; }
    }
}