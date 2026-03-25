using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WorkOrderSystem.Data;

namespace WorkOrderSystem.Models
{
    public class AppMessage
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "請輸入留言內容")]
        public string Content { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 用於支援「回覆」功能 (若為 Null 則是主留言，若有值則是回覆)
        public int? ParentMessageId { get; set; }
        public AppMessage? ParentMessage { get; set; }
        public List<AppMessage> Replies { get; set; } = new();
    }
}