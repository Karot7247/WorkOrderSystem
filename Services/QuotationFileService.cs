using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using WorkOrderSystem.Models; // 確保引用 Models
using OfficeOpenXml; // ✨ [新增] 引用 EPPlus
namespace WorkOrderSystem.Services
{
    public class QuotationFileService : IQuotationFileService
    {
        private readonly ILogger<QuotationFileService> _logger;
        private readonly string _baseQuotationPath;
        private readonly string? _libreOfficePath;
        private readonly string _tempPdfDirectory;

        public QuotationFileService(ILogger<QuotationFileService> logger, IConfiguration configuration)
        {
            _logger = logger;
            // 讀取設定
            _baseQuotationPath = configuration.GetValue<string>("FileStorageSettings:QuotationsRootPath")
                                 ?? configuration.GetValue<string>("QuotationSettings:NasBasePath")
                                 ?? throw new InvalidOperationException("Quotation NAS base path is not configured.");

            _libreOfficePath = configuration.GetValue<string>("AppSettings:LibreOfficePath");
            if (string.IsNullOrWhiteSpace(_libreOfficePath)) { _logger.LogWarning("未設定 LibreOffice 路徑，將嘗試 'soffice'。"); _libreOfficePath = "soffice"; }
            else if (!File.Exists(_libreOfficePath)) { _logger.LogError("設定的 LibreOffice 路徑無效: {Path}", _libreOfficePath); _libreOfficePath = "soffice"; }

            _tempPdfDirectory = configuration.GetValue<string>("AppSettings:PdfTempDirectory") ?? Path.GetTempPath();
            try { if (!Directory.Exists(_tempPdfDirectory)) Directory.CreateDirectory(_tempPdfDirectory); }
            catch (Exception ex) { _logger.LogError(ex, "PDF 暫存目錄錯誤"); }
        }

        // ==========================================
        //  原始功能 (維持不變)
        // ==========================================
        // ==========================================
        //  ✨ [新增] 使用 EPPlus 讀取 Excel 聯絡資訊 ✨
        // ==========================================
        public async Task<(string? CustomerName, string? ContactPerson, string? ContactPhone)> GetContactInfoFromExcelAsync(string fileName)
        {
            var filePath = await FindQuotationFilePathAsync(fileName);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return (null, null, null);
            }

            return await Task.Run(() =>
            {
                try
                {
                    using (var package = new ExcelPackage(new FileInfo(filePath)))
                    {
                        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                        if (worksheet == null) return (null, null, null);

                        // A2 => [2, 1] 客戶名稱
                        // A4 => [4, 1] 聯絡人
                        // A5 => [5, 1] 電話
                        string? custRaw = worksheet.Cells[2, 1].Text;
                        string? nameRaw = worksheet.Cells[4, 1].Text;
                        string? phoneRaw = worksheet.Cells[5, 1].Text;

                        // 1. 清理 A2 (客戶名稱)
                        string? cust = null;
                        if (!string.IsNullOrWhiteSpace(custRaw))
                        {
                            cust = custRaw.Replace("客戶名稱：", "").Replace("客戶名稱:", "").Trim();
                        }

                        // 2. 清理 A4 (聯絡人)
                        string? name = null;
                        if (!string.IsNullOrWhiteSpace(nameRaw))
                        {
                            name = nameRaw.Replace("連絡人員：", "").Replace("連絡人員:", "")
                                          .Replace("聯絡人員：", "").Replace("聯絡人員:", "")
                                          .Trim();
                        }

                        // 3. 清理 A5 (電話)
                        string? phone = null;
                        if (!string.IsNullOrWhiteSpace(phoneRaw))
                        {
                            phone = phoneRaw.Replace("連絡電話：", "").Replace("連絡電話:", "")
                                            .Replace("聯絡電話：", "").Replace("聯絡電話:", "")
                                            .Trim();
                        }

                        return (cust, name, phone);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EPPlus 讀取 Excel 資訊失敗: {FileName}", fileName);
                    return (null, null, null);
                }
            });
        }
        public async Task<List<QuotationSelectItem>> ListQuotationsAsync(bool filterByCustomer = false, string? customerName = null, int? year = null)
        {
            if (filterByCustomer && string.IsNullOrWhiteSpace(customerName)) return new List<QuotationSelectItem>();

            var results = new List<QuotationSelectItem>();
            if (!Directory.Exists(_baseQuotationPath)) return results;

            string searchRootPath = _baseQuotationPath;
            if (year.HasValue)
            {
                searchRootPath = Path.Combine(_baseQuotationPath, $"{year.Value}年度估價單");
                if (!Directory.Exists(searchRootPath)) return results;
            }

            results = await Task.Run(() =>
            {
                var list = new List<QuotationSelectItem>();
                var files = Directory.EnumerateFiles(searchRootPath, "*.xlsx", SearchOption.AllDirectories);

                foreach (var filePath in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    if (Path.GetFileName(filePath).StartsWith("~$")) continue;

                    // 簡易解析邏輯 (舊版相容)
                    if (filterByCustomer)
                    {
                        var parts = fileName.Split('-');
                        if (parts.Length >= 3)
                        {
                            var custName = parts[parts.Length - 2].Trim();
                            if (custName.Equals(customerName, StringComparison.OrdinalIgnoreCase))
                            {
                                list.Add(new QuotationSelectItem { Value = fileName, DisplayText = fileName });
                            }
                        }
                    }
                    else
                    {
                        list.Add(new QuotationSelectItem { Value = fileName, DisplayText = fileName });
                    }
                }
                return list.OrderByDescending(r => r.Value).ToList();
            });

            return results;
        }

        public async Task<string?> FindQuotationFilePathAsync(string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension)) return null;
            return await Task.Run(() =>
            {
                // 嘗試解析年份以縮小範圍
                string searchPath = _baseQuotationPath;
                if (fileNameWithoutExtension.Length >= 3 && int.TryParse(fileNameWithoutExtension.Substring(0, 3), out int year))
                {
                    var yearPath = Path.Combine(_baseQuotationPath, $"{year}年度估價單");
                    if (Directory.Exists(yearPath)) searchPath = yearPath;
                }

                return Directory.EnumerateFiles(searchPath, $"{fileNameWithoutExtension}.xlsx", SearchOption.AllDirectories).FirstOrDefault();
            });
        }

        public async Task<(Stream? PdfStream, string? FileName)> GetQuotationAsPdfStreamAsync(string excelFilePath)
        {
            if (string.IsNullOrWhiteSpace(excelFilePath) || !File.Exists(excelFilePath))
            {
                _logger.LogWarning("請求轉換 PDF 的 Excel 檔案路徑無效或檔案不存在: {Path}", excelFilePath);
                return (null, null);
            }
            // 防呆：如果 LibreOffice 路徑本身就有問題，直接返回
            if (string.IsNullOrWhiteSpace(_libreOfficePath) || (_libreOfficePath != "soffice" && !File.Exists(_libreOfficePath)))
            {
                _logger.LogError("LibreOffice 路徑未設定或無效，無法執行轉換。請檢查 appsettings.json。");
                return (null, null);
            }


            string pdfFileName = $"{Path.GetFileNameWithoutExtension(excelFilePath)}.pdf";
            string tempPdfPath = Path.Combine(_tempPdfDirectory, pdfFileName);
            MemoryStream? pdfStream = null;

            try
            {
                _logger.LogInformation("開始使用 LibreOffice 將 Excel 轉換為 PDF: {Input} -> {Output}", excelFilePath, tempPdfPath);

                if (File.Exists(tempPdfPath))
                {
                    _logger.LogWarning("暫存 PDF 檔案已存在，將被覆蓋: {Path}", tempPdfPath);
                    File.Delete(tempPdfPath);
                }

                // ✨ --outdir 後面的路徑也要加引號 ✨
                string arguments = $"--headless --convert-to pdf --outdir \"{_tempPdfDirectory}\" \"{excelFilePath}\"";
                _logger.LogInformation("執行命令: \"{Command}\" {Args}", _libreOfficePath, arguments);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = $"\"{_libreOfficePath}\"", // ✨ 為 FileName 加引號 ✨
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    // ✨ 設定工作目錄為 soffice.exe 所在目錄 ✨
                    WorkingDirectory = (_libreOfficePath != "soffice" ? Path.GetDirectoryName(_libreOfficePath) : null) ?? string.Empty
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    var outputBuilder = new System.Text.StringBuilder();
                    var errorBuilder = new System.Text.StringBuilder();
                    process.OutputDataReceived += (sender, args) => outputBuilder.AppendLine(args.Data);
                    process.ErrorDataReceived += (sender, args) => errorBuilder.AppendLine(args.Data);

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    bool exited = await Task.Run(() => process.WaitForExit(60000)); // 等待最多 60 秒
                    if (!exited) { _logger.LogError("LibreOffice 轉換超時: {Input}", excelFilePath); try { process.Kill(); } catch { } return (null, null); }

                    _logger.LogInformation("LibreOffice 程序退出，ExitCode: {Code}", process.ExitCode);
                    _logger.LogDebug("LibreOffice 標準輸出: {Output}", outputBuilder.ToString());
                    string errorOutput = errorBuilder.ToString();
                    if (!string.IsNullOrWhiteSpace(errorOutput)) { _logger.LogError("LibreOffice 錯誤輸出: {Error}", errorOutput); }

                    // ✨ 更嚴格的成功判斷：ExitCode 必須為 0 且 PDF 檔案存在 ✨
                    if (process.ExitCode != 0 || !File.Exists(tempPdfPath))
                    {
                        _logger.LogError("LibreOffice 轉換失敗 (ExitCode: {Code}, FileExists: {Exists}) for {Input}", process.ExitCode, File.Exists(tempPdfPath), excelFilePath);
                        return (null, null);
                    }
                }

                _logger.LogInformation("PDF 檔案已成功生成: {Path}", tempPdfPath);

                pdfStream = new MemoryStream();
                // ✨ 使用 FileShare.ReadWrite 避免可能的檔案鎖定問題 ✨
                using (var fileStream = new FileStream(tempPdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    await fileStream.CopyToAsync(pdfStream);
                }
                pdfStream.Position = 0;
                _logger.LogInformation("已將 PDF 讀入 MemoryStream");

                return (pdfStream, pdfFileName);
            }
            // ✨ 分開捕捉 Win32Exception 以提供更明確的日誌 ✨
            catch (System.ComponentModel.Win32Exception winEx)
            {
                _logger.LogError(winEx, "啟動 LibreOffice 程序時發生 Win32Exception (ErrorCode: {Code}) for {Path}. Message: {Msg}. 請檢查 soffice.exe 路徑、權限及是否有衝突程序。", winEx.NativeErrorCode, excelFilePath, winEx.Message);
                pdfStream?.Dispose();
                return (null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "使用 LibreOffice 轉換 Excel 為 PDF 時發生未預期錯誤 for {Path}", excelFilePath);
                pdfStream?.Dispose();
                return (null, null);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPdfPath))
                    {
                        File.Delete(tempPdfPath);
                        _logger.LogInformation("已刪除暫存 PDF 檔案: {Path}", tempPdfPath);
                    }
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "刪除暫存 PDF 檔案時發生錯誤: {Path}", tempPdfPath);
                }
            }
        }

        // ==========================================
        //  新功能 (LINE Bot)
        // ==========================================

        public Task<List<int>> GetAvailableYearsAsync()
        {
            var years = new List<int>();
            if (!Directory.Exists(_baseQuotationPath)) return Task.FromResult(years);

            try
            {
                var dirs = Directory.GetDirectories(_baseQuotationPath);
                foreach (var dir in dirs)
                {
                    var name = Path.GetFileName(dir);
                    if (name.Length >= 3 && int.TryParse(name.Substring(0, 3), out int year))
                    {
                        years.Add(year);
                    }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "GetAvailableYearsAsync Error"); }
            return Task.FromResult(years.OrderByDescending(y => y).ToList());
        }

        public Task<QuotationSearchResult> SearchQuotationsAsync(string keyword, int? year, SearchScope scope, int page, int pageSize)
        {
            var result = new QuotationSearchResult();
            if (!Directory.Exists(_baseQuotationPath)) return Task.FromResult(result);

            try
            {
                var filesToScan = new List<string>();
                var exts = new[] { "*.pdf", "*.xlsx" };

                if (year.HasValue)
                {
                    var yearPath = Path.Combine(_baseQuotationPath, $"{year.Value}年度估價單");
                    if (Directory.Exists(yearPath))
                    {
                        foreach (var ext in exts) filesToScan.AddRange(Directory.GetFiles(yearPath, ext, SearchOption.AllDirectories));
                    }
                }
                else
                {
                    foreach (var ext in exts) filesToScan.AddRange(Directory.GetFiles(_baseQuotationPath, ext, SearchOption.AllDirectories));
                }

                var query = filesToScan.Select(f => ParseQuotationFile(f)).Where(x => x != null);

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    keyword = keyword.Trim();
                    switch (scope)
                    {
                        case SearchScope.Customer:
                            query = query.Where(q => q!.CustomerName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                            break;
                        case SearchScope.Project:
                            query = query.Where(q => q!.ProjectName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                            break;
                        default:
                            query = query.Where(q => q!.CustomerName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                                     q!.ProjectName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                                     q.QuotationNo.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                            break;
                    }
                }

                // ✨ 修正 CS0428: 明確轉為 List 後再取 Count ✨
                var filteredList = query.OrderByDescending(q => q!.QuotationNo).ToList();

                result.TotalCount = filteredList.Count;
                result.Items = filteredList.Skip((page - 1) * pageSize).Take(pageSize).Cast<QuotationSelectItem>().ToList();
                result.HasNextPage = (page * pageSize) < result.TotalCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchQuotationsAsync Error");
            }

            return Task.FromResult(result);
        }

        private QuotationSelectItem? ParseQuotationFile(string fullPath)
        {
            try
            {
                var fileName = Path.GetFileName(fullPath);
                var fileNameNoExt = Path.GetFileNameWithoutExtension(fullPath);
                var ext = Path.GetExtension(fullPath).ToLower();
                if (fileName.StartsWith("~$")) return null;

                var parts = fileNameNoExt.Split('-');
                if (parts.Length < 3) return null;

                var proj = parts.Last();
                var cust = parts[parts.Length - 2];
                var no = string.Join("-", parts.Take(parts.Length - 2));
                var typeLabel = ext == ".pdf" ? "[PDF]" : "[Excel]";

                return new QuotationSelectItem
                {
                    Value = fileName,
                    DisplayText = $"{typeLabel} {no}\n{cust}\n{proj}",
                    CustomerName = cust,
                    ProjectName = proj,
                    QuotationNo = no
                };
            }
            catch { return null; }
        }
    }
}