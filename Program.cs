
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorkOrderSystem.Components;
using WorkOrderSystem.Components.Account;
using WorkOrderSystem.Data;
using WorkOrderSystem.Services;
using WorkOrderSystem.Models; // ✨ 修正：改回引用 Models (對應您原本的 namespace)
// using WorkOrderSystem.Models.Line; // ❌ 刪除這行，因為您的 namespace 沒有 .Line
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Components.Server;
using ApexCharts;
using OfficeOpenXml;
using System.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
// ★★★ 加入這段 Kestrel 設定 ★★★
builder.Services.Configure<KestrelServerOptions>(options =>
{
    // 設定最大請求大小 (例如 512 MB)
    options.Limits.MaxRequestBodySize = 512 * 1024 * 1024;
});

// 如果你是用 Form 上傳，也要設定這個
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 512 * 1024 * 1024;
});
// ▼▼▼ [新增] 加入 CORS 服務 ▼▼▼
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()  // 允許所有來源 (開發階段方便，上線可改嚴格)
                  .AllowAnyMethod()  // 允許 GET, POST 等
                  .AllowAnyHeader(); // 允許任何標頭
        });
});
// ▲▲▲ [新增結束] ▲▲▲
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped(p =>
    p.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

ExcelPackage.License.SetNonCommercialPersonal("沈可強");

builder.Services.AddScoped<IQuotationFileService, QuotationFileService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromHours(10);
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

builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    options.HttpsPort = 7209;
});

// 找到 builder.Services.AddServerSideBlazor() 並修改為：
builder.Services.AddServerSideBlazor(options =>
{
    // 【極致延長】斷線後伺服器保留記憶體的時間 (例如：12 小時)
    // 只要手機在 12 小時內喚醒重連，畫面都會維持原樣
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromHours(12);
})
.AddHubOptions(options =>
{
    // 放寬 SignalR 用戶端超時時間，避免稍微網路不穩就判定斷線
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);

    // 伺服器發送 Keep-alive ping 的頻率
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
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

builder.Services.AddSingleton<IEmailSender<AppUser>, IdentityNoOpEmailSender>();

builder.Services.AddHttpClient<LineNotifyService>();

// 1. 報修機器人服務
builder.Services.AddHttpClient<LineBotService>();
builder.Services.AddScoped<LineBotService>();

// 2. 派工查詢機器人服務
builder.Services.AddHttpClient<QuotationBotService>();
builder.Services.AddScoped<QuotationBotService>();

builder.Services.AddApexCharts();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    using (var dbContext = dbContextFactory.CreateDbContext())
    {
        try
        {
            Console.WriteLine("正在套用資料庫遷移...");
            await dbContext.Database.MigrateAsync();
            Console.WriteLine("資料庫遷移套用成功。");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"套用資料庫遷移時發生錯誤: {ex.Message}");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });

app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "text/plain"
});
app.UseBlazorFrameworkFiles();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapFallbackToFile("site-survey", "index.html");
app.MapFallbackToFile("construction-report", "index.html");
// ▼▼▼ [新增] 啟用 CORS (請放在 UseStaticFiles 之後，UseRouting 之前) ▼▼▼
app.UseCors("AllowAll");
// ▲▲▲ [新增結束] ▲▲▲
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

try
{
    var uploadsRootPath = builder.Configuration["FileStorageSettings:UploadsRootPath"];

    if (!string.IsNullOrEmpty(uploadsRootPath))
    {
        if (Directory.Exists(uploadsRootPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(uploadsRootPath),
                RequestPath = "/uploads"
            });
            Console.WriteLine($"[FileStorage] 成功映射虛擬路徑 /uploads 到 {uploadsRootPath}");
        }
        else
        {
            Console.WriteLine($"[FileStorage] 警告：設定的 UploadsRootPath '{uploadsRootPath}' 不存在或無法存取。");
        }
    }
    else
    {
        Console.WriteLine("[FileStorage] 警告：FileStorageSettings:UploadsRootPath 未在 appsettings.json 中設定。");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[FileStorage] 嚴重錯誤：設定靜態檔案服務時發生例外狀況: {ex.Message}");
}

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

// -------------------------------------------------------------------------
// Webhook 1: 原本的報修機器人
// -------------------------------------------------------------------------
app.MapPost("/api/line/webhook", async (HttpRequest request, IServiceScopeFactory scopeFactory) =>
{
    string requestBody;
    using (var reader = new StreamReader(request.Body))
    {
        requestBody = await reader.ReadToEndAsync();
    }

    var signature = request.Headers["X-Line-Signature"].FirstOrDefault();
    if (string.IsNullOrEmpty(signature)) { return Results.BadRequest(); }

    using (var scope = scopeFactory.CreateScope())
    {
        var lineBotService = scope.ServiceProvider.GetRequiredService<LineBotService>();
        if (!lineBotService.ValidateSignature(requestBody, signature)) { return Results.Forbid(); }

        // ✨ LineWebhookPayload 現在位於 WorkOrderSystem.Models，上面已經 using 了，所以這裡可以直接用
        var payload = JsonSerializer.Deserialize<LineWebhookPayload>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (payload?.Events == null) return Results.Ok();

        foreach (var lineEvent in payload.Events)
        {
            await lineBotService.ProcessWebhookEventAsync(lineEvent);
        }
    }
    return Results.Ok();
})
.AllowAnonymous();

// -------------------------------------------------------------------------
// Webhook 2: 新增的派工查詢機器人
// -------------------------------------------------------------------------
app.MapPost("/api/dispatch/webhook", async (HttpRequest request, IServiceScopeFactory scopeFactory) =>
{
    string requestBody;
    using (var reader = new StreamReader(request.Body))
    {
        requestBody = await reader.ReadToEndAsync();
    }

    var signature = request.Headers["X-Line-Signature"].FirstOrDefault();
    if (string.IsNullOrEmpty(signature)) { return Results.BadRequest(); }

    using (var scope = scopeFactory.CreateScope())
    {
        var botService = scope.ServiceProvider.GetRequiredService<QuotationBotService>();

        if (!botService.ValidateSignature(requestBody, signature))
        {
            return Results.Forbid();
        }

        // ✨ LineWebhookPayload 直接使用 WorkOrderSystem.Models 下的定義
        var payload = JsonSerializer.Deserialize<LineWebhookPayload>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload?.Events != null)
        {
            foreach (var lineEvent in payload.Events)
            {
                await botService.ProcessWebhookEventAsync(lineEvent);
            }
        }
    }
    return Results.Ok();
})
.AllowAnonymous();

app.Run();