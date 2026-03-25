using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WorkOrderSystem.Services
{
    public class LineNotifyService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _channelAccessToken;

        // 使用 IHttpClientFactory 和 IConfiguration 注入，這是.NET Core 的最佳實踐
        public LineNotifyService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            // 從 appsettings.json 讀取您的 Channel Access Token
            _channelAccessToken = configuration["LineNotifySettings:ChannelAccessToken"];
        }

        /// <summary>
        /// 主動推播訊息給指定的使用者
        /// </summary>
        public async Task SendPushMessageAsync(string lineUserId, string message)
        {
            if (string.IsNullOrEmpty(lineUserId) || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(_channelAccessToken))
            {
                return;
            }

            var requestUri = "https://api.line.me/v2/bot/message/push";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);

            var payload = new
            {
                to = lineUserId,
                messages = new[]
                {
                    new { type = "text", text = message }
                }
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"發送 LINE Push 訊息時發生例外: {ex.Message}");
            }
        }

        /// <summary>
        /// 回覆訊息給使用者 (通常用於 Webhook)
        /// </summary>
        public async Task SendReplyMessageAsync(string replyToken, string message)
        {
            if (string.IsNullOrEmpty(replyToken) || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(_channelAccessToken))
            {
                return;
            }

            var requestUri = "https://api.line.me/v2/bot/message/reply";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);

            var payload = new
            {
                replyToken,
                messages = new[]
                {
                    new { type = "text", text = message }
                }
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"發送 LINE Reply 訊息失敗: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"發送 LINE Reply 訊息時發生例外: {ex.Message}");
            }
        }
    }
}