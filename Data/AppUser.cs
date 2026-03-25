using Microsoft.AspNetCore.Identity;
using WorkOrderSystem.Models;

namespace WorkOrderSystem.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class AppUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? LineUserId { get; set; }

        // 職責布林旗標
        public bool IsRequester { get; set; } = true;
        public bool IsEngineer { get; set; } = false;
        public bool IsSupervisor { get; set; } = false;
        public bool IsManager { get; set; } = false;
        public virtual ICollection<WorkLogParticipant> WorkLogParticipations { get; set; } = new List<WorkLogParticipant>();
        // 我們規劃的電子簽名欄位也可以先加進來
        // 注意：這個欄位邏D輯上應該在 WorkOrder，我把它移到 WorkOrder
        public virtual ICollection<WorkOrderAssignee> WorkOrderAssignments { get; set; } = new List<WorkOrderAssignee>();
    }
}
