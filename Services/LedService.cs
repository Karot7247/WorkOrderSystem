using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.IO;
using WorkOrderSystem.Data;
using Microsoft.Extensions.Configuration; // 新增這個 using

namespace WorkOrderSystem.Services
{
    public class LedService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly IConfiguration _configuration; // 新增欄位來保存設定
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // 在建構函式中注入 IConfiguration
        public LedService(IDbContextFactory<ApplicationDbContext> dbContextFactory, IConfiguration configuration)
        {
            _dbContextFactory = dbContextFactory;
            _configuration = configuration; // 保存注入的設定物件
        }

        public async Task TriggerLedUpdateAsync()
        {
            Console.WriteLine($"[{DateTime.Now:T}] [LED Service] TriggerLedUpdateAsync 已被呼叫。");

            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var unassignedCount = await context.WorkOrders.CountAsync(wo => wo.Status == "待指派");
            var pendingVerificationCount = await context.WorkOrders.CountAsync(wo => wo.Status == "待管理者核銷");

            string ledText = $"尚有{unassignedCount}件工單未指派，尚有{pendingVerificationCount}件工單待核銷";
            Console.WriteLine($"[{DateTime.Now:T}] [LED Service] 準備透過外部程式發送文字: \"{ledText}\"");

            _ = Task.Run(() => ExecuteLedUpdater(ledText));
        }

        private void ExecuteLedUpdater(string text)
        {
            if (!_semaphore.Wait(0))
            {
                Console.WriteLine($"[{DateTime.Now:T}] [LED Service] 上一次的更新仍在進行中，本次請求已略過。");
                return;
            }

            try
            {
                // 從 appsettings.json 讀取路徑
                var updaterExePath = _configuration["LedServiceSettings:UpdaterExePath"];

                if (string.IsNullOrEmpty(updaterExePath) || !File.Exists(updaterExePath))
                {
                    Console.WriteLine($"[LED Service] 錯誤: 找不到 LedUpdater.exe。請檢查 appsettings.json 中的路徑設定是否正確。檢查的路徑: '{updaterExePath}'");
                    return;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = updaterExePath,
                    Arguments = $"\"{text}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine($"[{DateTime.Now:T}] [LED Service] LedUpdater.exe 執行成功。輸出: {output}");
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:T}] [LED Service] LedUpdater.exe 執行失敗。錯誤: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:T}] [LED Service] 呼叫 LedUpdater.exe 時發生例外狀況: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}