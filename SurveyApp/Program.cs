using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SurveyApp;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ▼▼▼ [請新增此行] ▼▼▼
// 確保所有等級 (包含 Debug 和 Info) 的日誌都能被看見
builder.Logging.SetMinimumLevel(LogLevel.Debug);
// ▲▲▲ [請新增此行] ▲▲▲

// 啟用基本的授權功能
builder.Services.AddAuthorizationCore();

// 啟用階層式驗證狀態 (讓 AuthenticationStateProvider 可以被注入)
//builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, TestAuthStateProvider>();


builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
public class TestAuthStateProvider : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // 回傳一個「未登入 (Anonymous)」的狀態，至少讓程式能跑起來不崩潰
        var identity = new ClaimsIdentity();
        var user = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(user));
    }
}