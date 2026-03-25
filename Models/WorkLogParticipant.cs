using System.ComponentModel.DataAnnotations.Schema;
using WorkOrderSystem.Data; // <--- 確保引用了正確的命名空間

namespace WorkOrderSystem.Models
{
    public class WorkLogParticipant
    {
        public int WorkLogId { get; set; }
        public string UserId { get; set; }

        // --- Navigation Properties ---

        [ForeignKey("WorkLogId")]
        public virtual WorkLog WorkLog { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } // <--- 已修正
    }
}