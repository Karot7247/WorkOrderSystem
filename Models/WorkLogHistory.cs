using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WorkOrderSystem.Data;

namespace WorkOrderSystem.Models
{
    public class WorkLogHistory
    {
        public int Id { get; set; }

        [Required]
        public int WorkLogId { get; set; }
        [ForeignKey("WorkLogId")]
        public virtual WorkLog WorkLog { get; set; }

        [Required]
        public string ArchivedContent { get; set; }

        public string? EditedById { get; set; }
        [ForeignKey("EditedById")]
        public virtual AppUser EditedBy { get; set; }

        public DateTime EditedAt { get; set; } = DateTime.Now;
    }
}