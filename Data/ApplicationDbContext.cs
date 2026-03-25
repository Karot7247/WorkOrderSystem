using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkOrderSystem.Models; // 引用我們的商業模型
using WorkOrderSystem.Models.Line;
namespace WorkOrderSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // 註冊我們的商業模型，EF Core 才知道要為它們建立資料表
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<WorkOrderPhoto> WorkOrderPhotos { get; set; }
        public DbSet<WorkOrderLog> WorkOrderLogs { get; set; }
        public DbSet<LineConversationState> LineConversationStates { get; set; }
        public DbSet<WorkLog> WorkLogs { get; set; }
        public DbSet<WorkLogPhoto> WorkLogPhotos { get; set; }
        public DbSet<WorkLogParticipant> WorkLogParticipants { get; set; }
        public DbSet<WorkOrderAssignee> WorkOrderAssignees { get; set; }
        public DbSet<WorkLogHistory> WorkLogHistories { get; set; }
        public DbSet<FieldRecordDraft> FieldRecordDrafts { get; set; }
        public DbSet<FieldRecordDraftPhoto> FieldRecordDraftPhotos { get; set; }
        public DbSet<AppMessage> AppMessages { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // <-- 保留這一行

            // --- 修正問題一：多重串聯刪除路徑 ---
            // 我們要手動設定 WorkLog 與 AppUser (建立者) 之間的關聯
            // 並明確告訴 EF Core，當建立者被刪除時，不要觸發串聯刪除 (Cascade)
            // DeleteBehavior.Restrict 的意思是：如果一位使用者還建立過任何日誌，那麼系統將不允許刪除這位使用者，直到他的日誌被處理掉為止。
            // 這通常是更安全的業務邏輯。
            builder.Entity<WorkLog>()
                .HasOne(log => log.CreatedBy) // 一筆日誌只有一個建立者
                .WithMany() // 一個建立者可以有多筆日誌 (我們不在 AppUser 端設定集合)
                .HasForeignKey(log => log.CreatedById) // 外鍵是 CreatedById
                .OnDelete(DeleteBehavior.Restrict); // 設定刪除行為為 Restrict (限制)


            // --- 解決問題一和問題二 ---
            // 設定 WorkLogParticipant 的複合主鍵
            builder.Entity<WorkLogParticipant>(entity =>
            {
                // 設定複合主鍵
                entity.HasKey(p => new { p.WorkLogId, p.UserId });

                // 這段是原本的，但是我們把它放進來統一設定
                entity.HasOne(p => p.WorkLog)
                    .WithMany(w => w.Participants)
                    .HasForeignKey(p => p.WorkLogId);

                // 這段也是原本的，但是我們把它放進來統一設定
                entity.HasOne(p => p.User)
                    .WithMany(u => u.WorkLogParticipations)
                    .HasForeignKey(p => p.UserId);
                builder.Entity<WorkOrderAssignee>()
        .HasKey(a => new { a.WorkOrderId, a.UserId });
            });
        }
    }
}