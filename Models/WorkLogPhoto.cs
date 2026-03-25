using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorkOrderSystem.Models
{
    public class WorkLogPhoto
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int WorkLogId { get; set; }

        [Required]
        public string FileName { get; set; }

        // ✨ 核心修正：加入 FilePath 屬性來儲存照片的完整路徑
        [Required]
        public string FilePath { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        // --- Navigation Properties ---

        [ForeignKey("WorkLogId")]
        public virtual WorkLog WorkLog { get; set; }
    }
}
