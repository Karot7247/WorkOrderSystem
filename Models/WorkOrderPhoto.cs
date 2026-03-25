using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorkOrderSystem.Models
{
    public class WorkOrderPhoto
    {
        [Key]
        public int Id { get; set; }

        // 照片儲存的路徑，例如 "/uploads/xxxxxxxx.jpg"
        [Required]
        public string FilePath { get; set; }

        // 分類：施工前, 施工中, 施工後, 自行備註
        public string Category { get; set; } = "未分類";

        // 當分類為"自行備註"時，用來儲存文字
        public string? CustomNote { get; set; }

        // --- 建立關聯 ---
        // 指向它所屬的工單 ID
        public int WorkOrderId { get; set; }

        // 導覽屬性，讓 EF Core 知道它們的關聯
        [ForeignKey("WorkOrderId")]
        public virtual WorkOrder WorkOrder { get; set; }
    }
}