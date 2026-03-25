using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WorkOrderSystem.Data;
using WorkOrderSystem.Models;
using WorkOrderSystem.Models.Line;
using System.Net;

namespace WorkOrderSystem.Services
{
    public class QuotationBotService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string? _channelSecret;
        private readonly string? _channelAccessToken;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public QuotationBotService(IConfiguration configuration, HttpClient httpClient, IServiceScopeFactory scopeFactory)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _scopeFactory = scopeFactory;
            _channelSecret = _configuration["LineNotifySettings:ChannelSecret"];
            _channelAccessToken = _configuration["LineNotifySettings:ChannelAccessToken"];
        }

        public bool ValidateSignature(string requestBody, string signature)
        {
            if (string.IsNullOrEmpty(_channelSecret)) return false;
            try
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_channelSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
                return string.Equals(Convert.ToBase64String(hash), signature);
            }
            catch { return false; }
        }

        public async Task ProcessWebhookEventAsync(LineEvent lineEvent)
        {
            if (lineEvent.Type != "message" || lineEvent.Message?.Type != "text") return;

            var userId = lineEvent.Source?.UserId;
            var userMsg = lineEvent.Message.Text?.Trim();
            var replyToken = lineEvent.ReplyToken;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userMsg)) return;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
            var quotationService = scope.ServiceProvider.GetRequiredService<IQuotationFileService>();

            var conversation = await context.LineConversationStates.FindAsync(userId);
            if (conversation == null)
            {
                conversation = new LineConversationState { LineUserId = userId, State = ConversationState.None };
                context.LineConversationStates.Add(conversation);
            }

            // 1. 入口判斷
            bool isSearchCommand = userMsg.StartsWith("查報價");
            bool isBrowseCommand = userMsg == "啟動查詢";

            if (isSearchCommand || isBrowseCommand)
            {
                string keyword = "";
                if (isSearchCommand)
                {
                    keyword = userMsg.Replace("查報價", "").Trim();
                    if (string.IsNullOrEmpty(keyword))
                    {
                        await SendTextMessageAsync(replyToken, "請輸入關鍵字，例如：「查報價 剛鈺」\n或是點擊選單的「啟動查詢」。");
                        return;
                    }
                }
                // 若是 "啟動查詢"，keyword 留空，稍後再問

                var searchState = new QuotationSearchState { Keyword = keyword };
                conversation.CollectedDataJson = JsonSerializer.Serialize(searchState);
                conversation.State = ConversationState.Quotation_SelectingYear;

                var years = await quotationService.GetAvailableYearsAsync();
                await AskForYearAsync(replyToken, years);
            }
            else if (conversation.State == ConversationState.Quotation_SelectingYear)
            {
                var searchState = JsonSerializer.Deserialize<QuotationSearchState>(conversation.CollectedDataJson ?? "{}");

                if (int.TryParse(userMsg.Replace("年", ""), out int year))
                {
                    searchState.SelectedYear = year;
                }
                else if (userMsg == "全部年份")
                {
                    searchState.SelectedYear = null;
                }
                else
                {
                    await SendTextMessageAsync(replyToken, "請點擊按鈕選擇年份，或重新輸入「查報價...」");
                    return;
                }

                conversation.CollectedDataJson = JsonSerializer.Serialize(searchState);
                conversation.State = ConversationState.Quotation_SelectingScope;
                await AskForScopeAsync(replyToken); // 這裡不需要傳 keyword 了，統一問法
            }
            else if (conversation.State == ConversationState.Quotation_SelectingScope)
            {
                var searchState = JsonSerializer.Deserialize<QuotationSearchState>(conversation.CollectedDataJson ?? "{}");

                if (userMsg == "搜尋客戶") searchState.Scope = SearchScope.Customer;
                else if (userMsg == "搜尋工程") searchState.Scope = SearchScope.Project;
                else searchState.Scope = SearchScope.All;

                conversation.CollectedDataJson = JsonSerializer.Serialize(searchState);

                // ✨✨✨ 關鍵分歧點 ✨✨✨
                if (string.IsNullOrEmpty(searchState.Keyword))
                {
                    // 情況 A：透過「啟動查詢」進來的，還沒輸入關鍵字 -> 進入「等待關鍵字」狀態
                    conversation.State = ConversationState.Quotation_AwaitingKeyword;
                    await SendTextMessageAsync(replyToken, "請輸入您要搜尋的「關鍵字」：\n(例如客戶名稱或工程名稱)");
                }
                else
                {
                    // 情況 B：透過「查報價 剛鈺」進來的，已有關鍵字 -> 直接搜尋
                    searchState.CurrentPage = 1;
                    conversation.CollectedDataJson = JsonSerializer.Serialize(searchState); // 確保狀態更新
                    conversation.State = ConversationState.Quotation_ViewingResults;
                    await ExecuteSearchAndReplyAsync(replyToken, quotationService, searchState);
                }
            }
            else if (conversation.State == ConversationState.Quotation_AwaitingKeyword)
            {
                // ✨✨✨ 新增狀態：接收使用者輸入的關鍵字 ✨✨✨
                var searchState = JsonSerializer.Deserialize<QuotationSearchState>(conversation.CollectedDataJson ?? "{}");

                searchState.Keyword = userMsg; // 存入使用者剛打的字
                searchState.CurrentPage = 1;

                conversation.CollectedDataJson = JsonSerializer.Serialize(searchState);
                conversation.State = ConversationState.Quotation_ViewingResults;

                await ExecuteSearchAndReplyAsync(replyToken, quotationService, searchState);
            }
            else if (conversation.State == ConversationState.Quotation_ViewingResults)
            {
                if (userMsg == "顯示下一頁")
                {
                    var searchState = JsonSerializer.Deserialize<QuotationSearchState>(conversation.CollectedDataJson ?? "{}");
                    searchState.CurrentPage++;
                    conversation.CollectedDataJson = JsonSerializer.Serialize(searchState);
                    await ExecuteSearchAndReplyAsync(replyToken, quotationService, searchState);
                }
                // 若輸入其他文字，可選擇忽略或視為新搜尋，這裡選擇忽略以免干擾
            }

            conversation.UpdatedAt = DateTime.Now;
            await context.SaveChangesAsync();
        }

        // --- 輔助方法 ---

        private async Task AskForYearAsync(string replyToken, List<int> years)
        {
            var actions = new List<QuickReplyItem>();
            foreach (var y in years.Take(3))
            {
                actions.Add(new QuickReplyItem($"{y}年"));
            }
            actions.Add(new QuickReplyItem("全部年份"));

            await SendQuickReplyMessageAsync(replyToken, "請問要查詢哪一年的資料？", actions);
        }

        private async Task AskForScopeAsync(string replyToken)
        {
            var actions = new List<QuickReplyItem>
            {
                new QuickReplyItem("搜尋客戶"),
                new QuickReplyItem("搜尋工程"),
                new QuickReplyItem("兩者皆搜")
            };
            // 統一問句
            await SendQuickReplyMessageAsync(replyToken, "請問您要搜尋的欄位是？", actions);
        }

        private async Task ExecuteSearchAndReplyAsync(string replyToken, IQuotationFileService service, QuotationSearchState state)
        {
            int pageSize = 9;
            var result = await service.SearchQuotationsAsync(state.Keyword, state.SelectedYear, state.Scope, state.CurrentPage, pageSize);

            if (result.TotalCount == 0)
            {
                // 提示使用者可以怎麼做
                await SendTextMessageAsync(replyToken, $"🔍 在 {(state.SelectedYear?.ToString() ?? "全部年份")} 找不到關於「{state.Keyword}」的資料。\n\n請輸入「查報價...」重新查詢。");
                return;
            }

            await SendSearchResultCarouselAsync(replyToken, result);
        }

        private async Task SendSearchResultCarouselAsync(string replyToken, QuotationSearchResult result)
        {
            if (string.IsNullOrEmpty(_channelAccessToken)) return;

            var baseUrl = _configuration["ApplicationSettings:BaseUrl"];
            var bubbles = new List<object>();

            foreach (var item in result.Items)
            {
                var safeFileName = Path.GetFileName(item.Value);
                var ext = Path.GetExtension(safeFileName).ToLower();
                var isPdf = ext == ".pdf";
                var typeLabel = isPdf ? "PDF" : "Excel";
                var typeColor = isPdf ? "#dc3545" : "#198754";

                var downloadUrl = $"{baseUrl}/api/line/files/download/{WebUtility.UrlEncode(safeFileName)}?openExternalBrowser=1";

                bubbles.Add(new
                {
                    type = "bubble",
                    size = "kilo",
                    header = new
                    {
                        type = "box",
                        layout = "vertical",
                        backgroundColor = "#f8f9fa",
                        contents = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"[{typeLabel}] {item.QuotationNo}",
                                weight = "bold",
                                color = typeColor,
                                size = "sm"
                            }
                        }
                    },
                    body = new
                    {
                        type = "box",
                        layout = "vertical",
                        contents = new object[]
                        {
                            new { type = "text", text = item.CustomerName, weight = "bold", size = "md", wrap = true },
                            new { type = "text", text = item.ProjectName, size = "xs", color = "#666666", wrap = true, margin = "sm" }
                        }
                    },
                    footer = new
                    {
                        type = "box",
                        layout = "vertical",
                        contents = new[]
                        {
                            new
                            {
                                type = "button",
                                style = "primary",
                                height = "sm",
                                action = new
                                {
                                    type = "uri",
                                    label = $"下載 {typeLabel}",
                                    uri = downloadUrl
                                }
                            }
                        }
                    }
                });
            }

            if (result.HasNextPage)
            {
                bubbles.Add(new
                {
                    type = "bubble",
                    size = "kilo",
                    header = new { type = "box", layout = "vertical", backgroundColor = "#ffffff", contents = new object[] { } },
                    body = new { type = "box", layout = "vertical", justifyContent = "center", height = "150px", contents = new[] { new { type = "text", text = "還有更多資料...", align = "center", color = "#aaaaaa" } } },
                    footer = new { type = "box", layout = "vertical", contents = new[] { new { type = "button", style = "secondary", height = "sm", action = new { type = "message", label = "顯示下一頁", text = "顯示下一頁" } } } }
                });
            }

            var flexMessagePayload = new { replyToken, messages = new[] { new { type = "flex", altText = "報價單搜尋結果", contents = new { type = "carousel", contents = bubbles } } } };
            await SendApiRequestAsync(flexMessagePayload);
        }

        private async Task SendQuickReplyMessageAsync(string replyToken, string text, List<QuickReplyItem> items)
        {
            var message = new
            {
                type = "text",
                text = text,
                quickReply = new
                {
                    items = items.Select(i => new {
                        type = "action",
                        action = new { type = "message", label = i.Action.Label, text = i.Action.Text }
                    }).ToList()
                }
            };
            var payload = new { replyToken, messages = new[] { message } };
            await SendApiRequestAsync(payload);
        }

        private async Task SendTextMessageAsync(string replyToken, string message)
        {
            var payload = new { replyToken, messages = new[] { new { type = "text", text = message } } };
            await SendApiRequestAsync(payload);
        }

        private async Task SendApiRequestAsync(object payload)
        {
            if (string.IsNullOrEmpty(_channelAccessToken)) return;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);
            var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync("https://api.line.me/v2/bot/message/reply", content);
        }
    }
}