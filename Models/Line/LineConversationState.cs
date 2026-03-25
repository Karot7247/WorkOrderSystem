using System.ComponentModel.DataAnnotations;

namespace WorkOrderSystem.Models.Line
{
    // 這是原本的資料庫實體 (不需要改，但我列出來確認位置)
    public class LineConversationState
    {
        [Key]
        public string LineUserId { get; set; }
        public ConversationState State { get; set; } = ConversationState.None;
        public string? CollectedDataJson { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // ✨ 請修改這個 Enum，加入最後面三個狀態 ✨
   
}