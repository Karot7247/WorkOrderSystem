using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkOrderSystem.Data;
using WorkOrderSystem.Models;
using WorkOrderSystem.Models.Line;
using System.Net; // 用於 UrlEncode

namespace WorkOrderSystem.Services
{

    public class LineBotService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string? _channelSecret;
        private readonly string? _channelAccessToken;
        private readonly IWebHostEnvironment _webHostEnvironment;

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public LineBotService(IServiceProvider serviceProvider, IConfiguration configuration, HttpClient httpClient, IWebHostEnvironment webHostEnvironment)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _httpClient = httpClient;
            _webHostEnvironment = webHostEnvironment;

            _channelSecret = _configuration["LineBotSettings:ChannelSecret"];
            _channelAccessToken = _configuration["LineBotSettings:ChannelAccessToken"];
        }

        public bool ValidateSignature(string requestBody, string signature)
        {
            if (string.IsNullOrEmpty(_channelSecret)) return false;
            try
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_channelSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
                var computedSignature = Convert.ToBase64String(hash);
                return string.Equals(computedSignature, signature);
            }
            catch { return false; }
        }

        public async Task ProcessWebhookEventAsync(LineEvent lineEvent)
        {
            var lineUserId = lineEvent.Source?.UserId;
            if (string.IsNullOrEmpty(lineUserId)) return;

            if (lineEvent.Type == "message")
            {
                if (lineEvent.Message?.Type == "text")
                {
                    await ProcessTextMessageAsync(lineEvent);
                }
                else if (lineEvent.Message?.Type == "image")
                {
                    await ProcessImageMessageAsync(lineEvent);
                }
            }
        }

        private async Task ProcessImageMessageAsync(LineEvent lineEvent)
        {
            var lineUserId = lineEvent.Source.UserId;
            var messageId = lineEvent.Message.Id;

            await using var scope = _serviceProvider.CreateAsyncScope();
            var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

            var conversation = await context.LineConversationStates.FindAsync(lineUserId);

            if (conversation?.State == ConversationState.AwaitingPhotos)
            {
                var formData = JsonSerializer.Deserialize<WorkOrderFormData>(conversation.CollectedDataJson ?? "{}", _jsonSerializerOptions) ?? new();

                var filePath = await DownloadLineImageAsync(messageId);
                if (!string.IsNullOrEmpty(filePath))
                {
                    formData.PhotoPaths.Add(filePath);
                    conversation.CollectedDataJson = JsonSerializer.Serialize(formData, _jsonSerializerOptions);
                    conversation.UpdatedAt = DateTime.Now;
                    await context.SaveChangesAsync();
                }
            }
        }

        private async Task ProcessTextMessageAsync(LineEvent lineEvent)
        {
            var lineUserId = lineEvent.Source.UserId;
            var receivedText = lineEvent.Message.Text?.Trim();
            var replyToken = lineEvent.ReplyToken;

            if (string.IsNullOrEmpty(receivedText) || string.IsNullOrEmpty(replyToken)) return;

            await using var scope = _serviceProvider.CreateAsyncScope();
            var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

            var conversation = await context.LineConversationStates.FindAsync(lineUserId);
            if (conversation == null)
            {
                conversation = new LineConversationState { LineUserId = lineUserId };
                context.LineConversationStates.Add(conversation);
            }

            if (receivedText == "取消" || receivedText == "取消報修")
            {
                await ResetConversationAsync(conversation, replyToken, "好的，目前的報修流程已為您取消。");
                await context.SaveChangesAsync();
                return;
            }

            switch (conversation.State)
            {
                case ConversationState.None:
                    await HandleStateNoneAsync(conversation, receivedText, replyToken);
                    break;
                case ConversationState.AwaitingCustomerName:
                    await HandleStateAwaitingCustomerNameAsync(conversation, receivedText, replyToken);
                    break;
                case ConversationState.ConfirmingCustomerName:
                    await HandleStateConfirmingCustomerNameAsync(conversation, receivedText, replyToken);
                    break;
                case ConversationState.AwaitingContactPerson:
                    await HandleStateAwaitingContactPersonAsync(conversation, receivedText, replyToken);
                    break;
                case ConversationState.ConfirmingContactPerson:
                    await HandleStateConfirmingContactPersonAsync(conversation, receivedText, replyToken);
                    break;
                case ConversationState.AwaitingContactPhone:
                    await HandleStateAwaitingContactPhoneAsync(conversation, receivedText, replyToken);
                    break;
                case ConversationState.ConfirmingContactPhone:
                    await HandleStateConfirmingContactPhoneAsync(conversation, receivedText, replyToken);
                    break;
                case ConversationState.AwaitingDescription:
                    await HandleStateAwaitingDescriptionAsync(conversation, receivedText, replyToken);
                    break;
                case ConversationState.ConfirmingDescription:
                    await HandleStateConfirmingDescriptionAsync(conversation, receivedText, replyToken);
                    break;
                case ConversationState.AwaitingPhotoUploadChoice:
                    await HandleStateAwaitingPhotoUploadChoiceAsync(conversation, receivedText, replyToken);
                    break;
                case ConversationState.AwaitingPhotos:
                    if (receivedText.Trim() == "完成")
                    {
                        await AskForFinalConfirmationAsync(conversation, replyToken);
                    }
                    else
                    {
                        await SendReplyMessageAsync(replyToken, "收到文字訊息。如果您已上傳完所有照片，請輸入「完成」來結束報修。");
                    }
                    break;
                case ConversationState.AwaitingFinalConfirmation:
                    await HandleStateAwaitingFinalConfirmationAsync(conversation, receivedText, replyToken);
                    break;
            }

            conversation.UpdatedAt = DateTime.Now;
            await context.SaveChangesAsync();
        }

        private async Task HandleStateNoneAsync(LineConversationState conversation, string text, string replyToken)
        {
            if (text.Contains("報修") || text.Contains("維修"))
            {
                conversation.State = ConversationState.AwaitingCustomerName;
                conversation.CollectedDataJson = JsonSerializer.Serialize(new WorkOrderFormData(), _jsonSerializerOptions);
                await SendReplyMessageAsync(replyToken, "好的，即將為您建立報修單。\n請問您的單位/公司名稱是？");
            }
            // ✨ 新增：查報價功能 ✨
            else if (text.StartsWith("查報價"))
            {
                var keyword = text.Replace("查報價", "").Trim();
                await HandleQuotationSearchAsync(replyToken, keyword);
            }
            else if (text.StartsWith("綁定：") || text.StartsWith("绑订："))
            {
                var fullName = text.Substring(3).Trim();
                string replyMessage;

                await using var scope = _serviceProvider.CreateAsyncScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

                var targetUser = await userManager.Users.FirstOrDefaultAsync(u => u.FullName == fullName);

                if (targetUser != null)
                {
                    targetUser.LineUserId = conversation.LineUserId;
                    var result = await userManager.UpdateAsync(targetUser);
                    replyMessage = result.Succeeded
                        ? $"綁定成功！您的帳號 '{targetUser.FullName}' 未來將會在此收到派工通知。"
                        : "綁定失敗，更新使用者資料時發生錯誤。";
                }
                else
                {
                    replyMessage = $"綁定失敗！在系統中找不到姓名為 '{fullName}' 的使用者。請確認您輸入的姓名與系統中的『完整姓名』完全一致。";
                }
                await SendReplyMessageAsync(replyToken, replyMessage);
            }
            else
            {
                await SendReplyMessageAsync(replyToken, "您好！\n- 如需報修服務，請輸入「我要報修」。\n- 如需查詢報價單，請輸入「查報價 客戶名稱」。\n- 如為內部員工需綁定通知，請輸入「綁定：您的全名」。\n- 如需取消目前操作，請輸入「取消」。");
            }
        }

        // ✨ 新增：處理搜尋與回傳 Flex Message 的方法
        private async Task HandleQuotationSearchAsync(string replyToken, string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                await SendReplyMessageAsync(replyToken, "請輸入關鍵字，例如：「查報價 剛鈺」");
                return;
            }

            // 使用 Scope 解析 Service
            await using var scope = _serviceProvider.CreateAsyncScope();
            var quotationService = scope.ServiceProvider.GetRequiredService<IQuotationFileService>();

            try
            {
                // 呼叫現有的報價單搜尋服務
                // 參數：filterByCustomer=true, customerName=關鍵字, year=null(搜全部)
                var results = await quotationService.ListQuotationsAsync(
                    filterByCustomer: true,
                    customerName: keyword,
                    year: null
                );

                if (results != null && results.Any())
                {
                    // 建立並發送 Flex Message
                    await SendQuotationCarouselAsync(replyToken, results);
                }
                else
                {
                    await SendReplyMessageAsync(replyToken, $"🔍 找不到「{keyword}」的相關報價單。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"搜尋報價單錯誤: {ex.Message}");
                await SendReplyMessageAsync(replyToken, "搜尋時發生錯誤，請稍後再試。");
            }
        }

        // ✨ 新增：產生並發送報價單 Flex Message
        private async Task SendQuotationCarouselAsync(string replyToken, List<QuotationSelectItem> items)
        {
            if (string.IsNullOrEmpty(_channelAccessToken)) return;

            var displayItems = items.Take(10).ToList(); // 取前 10 筆
            var baseUrl = _configuration["ApplicationSettings:BaseUrl"];

            // 建構 Flex Carousel 的匿名物件結構
            var bubbles = displayItems.Select(item =>
            {
                // 組合下載連結 (需建立 LineFileController)
                var downloadUrl = $"{baseUrl}/api/line/files/download/{WebUtility.UrlEncode(item.Value)}";

                return new
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
                            new { type = "text", text = "PDF 報價單", weight = "bold", color = "#dc3545", size = "xs" }
                        }
                    },
                    body = new
                    {
                        type = "box",
                        layout = "vertical",
                        contents = new[]
                        {
                            new { type = "text", text = item.DisplayText, weight = "bold", size = "sm", wrap = true, maxLines = 3 }
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
                                    label = "下載檔案",
                                    uri = downloadUrl
                                }
                            }
                        }
                    }
                };
            }).ToArray();

            var flexMessage = new
            {
                type = "flex",
                altText = "報價單查詢結果",
                contents = new
                {
                    type = "carousel",
                    contents = bubbles
                }
            };

            await SendFlexMessageAsync(replyToken, flexMessage);
        }

        // ✨ 新增：發送 Flex Message 的底層方法
        private async Task SendFlexMessageAsync(string replyToken, object flexMessageContent)
        {
            if (string.IsNullOrEmpty(_channelAccessToken)) return;

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);

            var requestBody = new
            {
                replyToken,
                messages = new[] { flexMessageContent }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody, _jsonSerializerOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/reply", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[錯誤] 發送 LINE Flex 訊息失敗: {errorContent}");
            }
        }

        private async Task HandleStateAwaitingCustomerNameAsync(LineConversationState conversation, string text, string replyToken)
        {
            var formData = JsonSerializer.Deserialize<WorkOrderFormData>(conversation.CollectedDataJson ?? "{}", _jsonSerializerOptions) ?? new();
            formData.CustomerName = text;
            conversation.CollectedDataJson = JsonSerializer.Serialize(formData, _jsonSerializerOptions);
            conversation.State = ConversationState.ConfirmingCustomerName;
            await SendConfirmationQueryAsync(replyToken, $"您輸入的單位名稱是：\n『{text}』\n\n資訊是否正確？");
        }

        private async Task HandleStateConfirmingCustomerNameAsync(LineConversationState conversation, string text, string replyToken)
        {
            if (text.StartsWith("確定"))
            {
                conversation.State = ConversationState.AwaitingContactPerson;
                await SendReplyMessageAsync(replyToken, "感謝您！\n請問主要聯絡人是哪位？");
            }
            else
            {
                conversation.State = ConversationState.AwaitingCustomerName;
                await SendReplyMessageAsync(replyToken, "好的，請重新輸入您的單位/公司名稱。");
            }
        }

        private async Task HandleStateAwaitingContactPersonAsync(LineConversationState conversation, string text, string replyToken)
        {
            var formData = JsonSerializer.Deserialize<WorkOrderFormData>(conversation.CollectedDataJson ?? "{}", _jsonSerializerOptions) ?? new();
            formData.ContactPerson = text;
            conversation.CollectedDataJson = JsonSerializer.Serialize(formData, _jsonSerializerOptions);
            conversation.State = ConversationState.ConfirmingContactPerson;
            await SendConfirmationQueryAsync(replyToken, $"您輸入的聯絡人是：\n『{text}』\n\n資訊是否正確？");
        }

        private async Task HandleStateConfirmingContactPersonAsync(LineConversationState conversation, string text, string replyToken)
        {
            if (text.StartsWith("確定"))
            {
                conversation.State = ConversationState.AwaitingContactPhone;
                await SendReplyMessageAsync(replyToken, "收到！\n請提供您的聯絡電話。");
            }
            else
            {
                conversation.State = ConversationState.AwaitingContactPerson;
                await SendReplyMessageAsync(replyToken, "好的，請重新輸入聯絡人姓名。");
            }
        }

        private async Task HandleStateAwaitingContactPhoneAsync(LineConversationState conversation, string text, string replyToken)
        {
            var formData = JsonSerializer.Deserialize<WorkOrderFormData>(conversation.CollectedDataJson ?? "{}", _jsonSerializerOptions) ?? new();
            formData.ContactPhone = text;
            conversation.CollectedDataJson = JsonSerializer.Serialize(formData, _jsonSerializerOptions);
            conversation.State = ConversationState.ConfirmingContactPhone;
            await SendConfirmationQueryAsync(replyToken, $"您輸入的聯絡電話是：\n『{text}』\n\n資訊是否正確？");
        }

        private async Task HandleStateConfirmingContactPhoneAsync(LineConversationState conversation, string text, string replyToken)
        {
            if (text.StartsWith("確定"))
            {
                conversation.State = ConversationState.AwaitingDescription;
                await SendReplyMessageAsync(replyToken, "好的，最後請您簡短描述一下遇到的問題。");
            }
            else
            {
                conversation.State = ConversationState.AwaitingContactPhone;
                await SendReplyMessageAsync(replyToken, "好的，請重新輸入您的聯絡電話。");
            }
        }

        private async Task HandleStateAwaitingDescriptionAsync(LineConversationState conversation, string text, string replyToken)
        {
            var formData = JsonSerializer.Deserialize<WorkOrderFormData>(conversation.CollectedDataJson ?? "{}", _jsonSerializerOptions) ?? new();
            formData.Description = text;
            conversation.CollectedDataJson = JsonSerializer.Serialize(formData, _jsonSerializerOptions);
            conversation.State = ConversationState.ConfirmingDescription;
            await SendConfirmationQueryAsync(replyToken, $"您輸入的問題描述是：\n『{text}』\n\n資訊是否正確？");
        }

        private async Task HandleStateConfirmingDescriptionAsync(LineConversationState conversation, string text, string replyToken)
        {
            if (text.StartsWith("確定"))
            {
                conversation.State = ConversationState.AwaitingPhotoUploadChoice;
                await SendQuickReplyMessageAsync(replyToken, "問題描述已確認。\n請問您需要上傳照片嗎？",
                    new List<QuickReplyItem> { new("是，我要上傳"), new("否，不需上傳") });
            }
            else
            {
                conversation.State = ConversationState.AwaitingDescription;
                await SendReplyMessageAsync(replyToken, "好的，請重新描述您遇到的問題。");
            }
        }

        private async Task HandleStateAwaitingPhotoUploadChoiceAsync(LineConversationState conversation, string text, string replyToken)
        {
            if (text.StartsWith("是"))
            {
                conversation.State = ConversationState.AwaitingPhotos;
                await SendReplyMessageAsync(replyToken, "好的，請開始傳送您要上傳的照片。\n\n所有照片都傳送完畢後，請輸入「完成」兩個字來結束。");
            }
            else
            {
                await AskForFinalConfirmationAsync(conversation, replyToken);
            }
        }

        private async Task HandleStateAwaitingFinalConfirmationAsync(LineConversationState conversation, string text, string replyToken)
        {
            if (text.StartsWith("確認送出"))
            {
                await CreateWorkOrderFromConversationAsync(conversation, replyToken);
            }
            else if (text.StartsWith("重新輸入"))
            {
                await ResetConversationAsync(conversation, replyToken, "好的，我們重新開始。");
                conversation.State = ConversationState.AwaitingCustomerName;
                await SendReplyMessageAsync(replyToken, "請問您的單位/公司名稱是？");
            }
            else
            {
                await AskForFinalConfirmationAsync(conversation, replyToken);
            }
        }

        private async Task AskForFinalConfirmationAsync(LineConversationState conversation, string replyToken)
        {
            var formData = JsonSerializer.Deserialize<WorkOrderFormData>(conversation.CollectedDataJson ?? "{}", _jsonSerializerOptions) ?? new();

            var summary = new StringBuilder();
            summary.AppendLine("請確認您輸入的報修資訊：");
            summary.AppendLine("--------------------");
            summary.AppendLine($"單位名稱：{formData.CustomerName}");
            summary.AppendLine($"聯絡人：{formData.ContactPerson}");
            summary.AppendLine($"聯絡電話：{formData.ContactPhone}");
            summary.AppendLine($"問題描述：{formData.Description}");
            summary.AppendLine($"上傳照片：{formData.PhotoPaths.Count} 張");
            summary.AppendLine("--------------------");
            summary.AppendLine("如果資訊正確，請點擊「確認送出」。");

            conversation.State = ConversationState.AwaitingFinalConfirmation;

            await SendQuickReplyMessageAsync(replyToken, summary.ToString(),
                new List<QuickReplyItem>
                {
                    new QuickReplyItem("確認送出"),
                    new QuickReplyItem("重新輸入")
                });
        }

        private async Task CreateWorkOrderFromConversationAsync(LineConversationState conversation, string replyToken)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var lineNotifyService = scope.ServiceProvider.GetRequiredService<LineNotifyService>();

            try
            {
                var formData = JsonSerializer.Deserialize<WorkOrderFormData>(conversation.CollectedDataJson ?? "{}", _jsonSerializerOptions) ?? new();
                var lineDisplayName = await GetLineUserProfileNameAsync(conversation.LineUserId);

                var newWorkOrder = new WorkOrder
                {
                    Title = $"來自LINE的報修：{formData.CustomerName}",
                    Description = formData.Description,
                    CustomerName = formData.CustomerName,
                    ContactPerson = formData.ContactPerson,
                    ContactPhone = formData.ContactPhone,
                    LineRequesterDisplayName = lineDisplayName,
                    WorkOrderType = "維修",
                    Status = "待指派",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                };

                context.WorkOrders.Add(newWorkOrder);
                await context.SaveChangesAsync();

                if (formData.PhotoPaths.Any())
                {
                    foreach (var path in formData.PhotoPaths)
                    {
                        context.WorkOrderPhotos.Add(new WorkOrderPhoto
                        {
                            WorkOrderId = newWorkOrder.Id,
                            FilePath = path,
                            Category = "LINE客戶上傳"
                        });
                    }
                    await context.SaveChangesAsync();
                }

                try
                {
                    var adminsAndSupervisors = await userManager.Users
                        .Where(u => u.IsManager || u.IsSupervisor)
                        .ToListAsync();

                    var recipientLineIds = adminsAndSupervisors
                        .Where(u => !string.IsNullOrEmpty(u.LineUserId))
                        .Select(u => u.LineUserId)
                        .ToList();

                    if (recipientLineIds.Any())
                    {
                        var baseUrl = _configuration["ApplicationSettings:BaseUrl"];
                        var workOrderUrl = $"{baseUrl}/workorders/details/{newWorkOrder.Id}?openExternalBrowser=1";
                        var requesterName = lineDisplayName ?? "N/A";
                        var message = $"【新派工申請通知】\n申請人：{requesterName} (LINE)\n標題：{newWorkOrder.Title}\n\n請盡速前往系統進行指派：\n{workOrderUrl}";

                        foreach (var lineId in recipientLineIds)
                        {
                            _ = lineNotifyService.SendPushMessageAsync(lineId, message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LINE 報修] 發送 [新工單] 通知給管理者/主管失敗: {ex.Message}");
                }

                conversation.State = ConversationState.None;
                conversation.CollectedDataJson = null;

                var confirmationMessage = $"報修成功！您的案件已送出。\n案件編號為：#{newWorkOrder.Id}\n我們將盡快為您處理。";
                await SendReplyMessageAsync(replyToken, confirmationMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n\n[偵錯] 在 CreateWorkOrderFromConversationAsync 中發生嚴重錯誤: {ex.ToString()}\n\n");
                await SendReplyMessageAsync(replyToken, "系統發生內部錯誤，建立工單失敗，請稍後再試或聯絡管理員。");
            }
        }

        private async Task<string?> DownloadLineImageAsync(string messageId)
        {
            try
            {
                var requestUri = $"https://api-data.line.me/v2/bot/message/{messageId}/content";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);
                var response = await _httpClient.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    var fileName = $"{Guid.NewGuid()}.jpg";
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                    Directory.CreateDirectory(uploadsFolder);
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    await File.WriteAllBytesAsync(filePath, imageBytes);

                    return $"/uploads/{fileName}";
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"下載 LINE 圖片失敗: {ex.Message}");
                return null;
            }
        }

        private async Task<string> GetLineUserProfileNameAsync(string lineUserId)
        {
            try
            {
                var requestUri = $"https://api.line.me/v2/bot/profile/{lineUserId}";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);
                var response = await _httpClient.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var profile = JsonSerializer.Deserialize<JsonElement>(json, _jsonSerializerOptions);
                    if (profile.TryGetProperty("displayName", out var displayNameElement))
                    {
                        return displayNameElement.GetString() ?? "LINE 使用者";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"取得 LINE 使用者名稱失敗: {ex.Message}");
            }
            return "LINE 使用者";
        }

        private async Task SendReplyMessageAsync(string replyToken, string message)
        {
            if (string.IsNullOrEmpty(_channelAccessToken)) return;

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);

            var requestBody = new
            {
                replyToken,
                messages = new[] { new { type = "text", text = message } }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody, _jsonSerializerOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/reply", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[錯誤] 發送 LINE Reply 訊息失敗: {errorContent}");
            }
        }

        private async Task SendQuickReplyMessageAsync(string replyToken, string text, List<QuickReplyItem> items)
        {
            if (string.IsNullOrEmpty(_channelAccessToken)) return;

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);

            var message = new LineTextMessage
            {
                Text = text,
                QuickReply = new QuickReply { Items = items }
            };

            var requestBody = new { replyToken, messages = new[] { message } };
            var jsonPayload = JsonSerializer.Serialize(requestBody, _jsonSerializerOptions);

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/reply", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[錯誤] 發送 LINE Quick Reply 訊息失敗: {errorContent}");
                Console.WriteLine($"[偵錯] Payload 內容: {jsonPayload}");
            }
        }

        private async Task SendConfirmationQueryAsync(string replyToken, string text)
        {
            await SendQuickReplyMessageAsync(replyToken, text,
                new List<QuickReplyItem> { new("確定"), new("重新輸入"), new("取消報修") });
        }

        private async Task ResetConversationAsync(LineConversationState conversation, string replyToken, string message)
        {
            conversation.State = ConversationState.None;
            conversation.CollectedDataJson = null;
            await SendReplyMessageAsync(replyToken, message);
        }
    }

    public class LineTextMessage
    {
        public string Type => "text";
        public string Text { get; set; }
        public QuickReply? QuickReply { get; set; }
    }

    public class QuickReply
    {
        public List<QuickReplyItem> Items { get; set; }
    }

    public class QuickReplyItem
    {
        public string Type => "action";
        public QuickReplyAction Action { get; set; }

        public QuickReplyItem(string label)
        {
            Action = new QuickReplyAction(label);
        }
    }

    public class QuickReplyAction
    {
        public string Type => "message";
        public string Label { get; set; }
        public string Text { get; set; }

        public QuickReplyAction(string label)
        {
            Label = label;
            Text = label;
        }
    }
}