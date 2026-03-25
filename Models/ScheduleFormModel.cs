using System.ComponentModel.DataAnnotations;

namespace WorkOrderSystem.Models // (請確保 namespace 與你的專案相符)
{
    public class ScheduleFormModel
    {
        [Required(ErrorMessage = "請選擇開始日期")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "請選擇結束日期")]
        public DateTime EndDate { get; set; } = DateTime.Today;
    }
}