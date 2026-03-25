// Models/LineWebhookModels.cs
using System.Text.Json.Serialization;

namespace WorkOrderSystem.Models
{
    // 這些是為了對應 LINE Webhook 送來的 JSON 格式
    public class LineWebhookPayload
    {
        [JsonPropertyName("events")]
        public List<LineEvent> Events { get; set; } = new();
    }

    public class LineEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("source")]
        public LineSource Source { get; set; } = new();

        [JsonPropertyName("message")]
        public LineMessage? Message { get; set; }

        [JsonPropertyName("replyToken")]
        public string ReplyToken { get; set; } = "";
    }

    public class LineSource
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = "";
    }

    public class LineMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
        public string Id { get; set; }
    }
}