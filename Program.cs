using ApexCharts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using OfficeOpenXml;
using System.ComponentModel;
using System.IO; // ✨ [新增] 確保可以呼叫 Directory 類別
using System.Text.Json;
using WorkOrderSystem.Components;
using WorkOrderSystem.Components.Account;
using WorkOrderSystem.Data;
using WorkOrderSystem.Models;
using WorkOrderSystem.Services;
using System.IO.Compression;
// ==========================================
// ✨ [關鍵修正] 徹底解決 Windows 服務的 System32 路徑綁架
// 強制將 C# 底層的當前目錄設為程式所在資料夾，避免檔案存取異常崩潰
// ==========================================
if (WindowsServiceHelpers.IsWindowsService())
{
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
}

// ==========================================
// ✨ 初始化 Web 主機設定，確保 Windows 服務路徑正確
// ==========================================
var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService()
        ? AppContext.BaseDirectory
        : default
};

var builder = WebApplication.CreateBuilder(options);

// ✨ [核心修正] 從 appsettings.json 讀取監聽地址，不再寫死 Port
// 預設讀取 ApplicationSettings:Url，若未設定則由 Kestrel 預設配置接管
var configUrl = builder.Configuration["ApplicationSettings:Url"];
if (!string.IsNullOrEmpty(configUrl))
{
    builder.WebHost.UseUrls(configUrl);
}

// ✨ 註冊為 Windows 服務
builder.Host.UseWindowsService(windowsOptions =>
{
    windowsOptions.ServiceName = "WorkOrderSystemService";
});

// ==========================================
// 1. 伺服器效能與傳輸限制設定
// ==========================================
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 512 * 1024 * 1024; // 512 MB
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 512 * 1024 * 1024;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// ==========================================
// 2. 資料庫與 Identity 驗證設定
// ==========================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped(p =>
    p.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

ExcelPackage.License.SetNonCommercialPersonal("沈可強");

// ==========================================
// 3. Blazor 與實時通訊 (SignalR) 設定
// ==========================================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromHours(12);
        options.MaxBufferedUnacknowledgedRenderBatches = 20;
        options.DetailedErrors = true;
    });

builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1 MB
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddSingleton<LedService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.ApplicationCookie.Configure(opt => { opt.Cookie.HttpOnly = true; opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; });
        options.ExternalCookie.Configure(opt => { opt.Cookie.HttpOnly = true; opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; });
        options.TwoFactorRememberMeCookie.Configure(opt => { opt.Cookie.HttpOnly = true; opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; });
    }
});

builder.Services.AddAuthorization();

builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// ==========================================
// 4. 自訂服務與機器人設定
// ==========================================
builder.Services.AddScoped<IQuotationFileService, QuotationFileService>();
builder.Services.AddSingleton<IEmailSender<AppUser>, IdentityNoOpEmailSender>();
builder.Services.AddHttpClient<LineNotifyService>();
builder.Services.AddHttpClient<LineBotService>();
builder.Services.AddScoped<LineBotService>();
builder.Services.AddHttpClient<QuotationBotService>();
builder.Services.AddScoped<QuotationBotService>();

builder.Services.AddApexCharts();
builder.Services.AddControllers();

var app = builder.Build();

// ==========================================
// 5. 資料庫自動遷移
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    using (var dbContext = dbContextFactory.CreateDbContext())
    {
        try
        {
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] 遷移失敗: {ex.Message}");
        }
    }
}

// ==========================================
// 6. 中間件與靜態檔案路由
// ==========================================
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });

app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "text/plain"
});

app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// --- NAS / 外部儲存路徑映射 ---
try
{
    var uploadsRootPath = builder.Configuration["FileStorageSettings:UploadsRootPath"];
    if (!string.IsNullOrEmpty(uploadsRootPath) && Directory.Exists(uploadsRootPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadsRootPath),
            RequestPath = "/uploads"
        });
        Console.WriteLine($"[FileStorage] 映射路徑 /uploads 到 {uploadsRootPath}");
    }
}
catch (Exception ex) { Console.WriteLine($"[FileStorage] 錯誤: {ex.Message}"); }

app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();

// -------------------------------------------------------------------------
// Webhook 1: 報修機器人
// -------------------------------------------------------------------------
app.MapPost("/api/line/webhook", async (HttpRequest request, IServiceScopeFactory scopeFactory) =>
{
    using (var reader = new StreamReader(request.Body))
    {
        var requestBody = await reader.ReadToEndAsync();
        var signature = request.Headers["X-Line-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature)) return Results.BadRequest();

        using (var scope = scopeFactory.CreateScope())
        {
            var lineBotService = scope.ServiceProvider.GetRequiredService<LineBotService>();
            if (!lineBotService.ValidateSignature(requestBody, signature)) return Results.Forbid();

            var payload = JsonSerializer.Deserialize<LineWebhookPayload>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload?.Events != null)
            {
                foreach (var lineEvent in payload.Events) await lineBotService.ProcessWebhookEventAsync(lineEvent);
            }
        }
    }
    return Results.Ok();
}).AllowAnonymous();

// -------------------------------------------------------------------------
// Webhook 2: 派工查詢機器人
// -------------------------------------------------------------------------
app.MapPost("/api/dispatch/webhook", async (HttpRequest request, IServiceScopeFactory scopeFactory) =>
{
    using (var reader = new StreamReader(request.Body))
    {
        var requestBody = await reader.ReadToEndAsync();
        var signature = request.Headers["X-Line-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature)) return Results.BadRequest();

        using (var scope = scopeFactory.CreateScope())
        {
            var botService = scope.ServiceProvider.GetRequiredService<QuotationBotService>();
            if (!botService.ValidateSignature(requestBody, signature)) return Results.Forbid();

            var payload = JsonSerializer.Deserialize<LineWebhookPayload>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload?.Events != null)
            {
                foreach (var lineEvent in payload.Events) await botService.ProcessWebhookEventAsync(lineEvent);
            }
        }
    }
    return Results.Ok();
}).AllowAnonymous();
// -------------------------------------------------------------------------
// Webhook / API: 打包下載工單照片 (公開連結)
// -------------------------------------------------------------------------
app.MapGet("/api/download/photos/{token:guid}", async (Guid token, ApplicationDbContext context, IConfiguration config) =>
{
    var workOrder = await context.WorkOrders.Include(w => w.Photos).FirstOrDefaultAsync(w => w.PublicAccessToken == token);
    if (workOrder == null || workOrder.Photos == null || !workOrder.Photos.Any())
        return Results.NotFound("找不到相應的照片或授權已失效。");

    var uploadsRoot = config["FileStorageSettings:UploadsRootPath"];
    if (string.IsNullOrEmpty(uploadsRoot)) return Results.Problem("系統儲存路徑未設定。");

    var memoryStream = new System.IO.MemoryStream();
    using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
    {
        foreach (var photo in workOrder.Photos)
        {
            // 將資料庫路徑轉換為 NAS 實體路徑
            var relativePath = photo.FilePath.Replace("/uploads", "", StringComparison.OrdinalIgnoreCase).TrimStart('/', '\\');
            var fullPath = System.IO.Path.Combine(uploadsRoot, relativePath);

            if (System.IO.File.Exists(fullPath))
            {
                // 為了避免照片同名互相覆蓋，自動在檔名前面加上 ID 作為前綴
                var entryName = $"{photo.Id}_{System.IO.Path.GetFileName(fullPath)}";
                archive.CreateEntryFromFile(fullPath, entryName);
            }
        }
    }

    memoryStream.Position = 0;
    return Results.File(memoryStream, "application/zip", $"工單附件照片_{workOrder.Id}.zip");
}).AllowAnonymous();

app.Run();