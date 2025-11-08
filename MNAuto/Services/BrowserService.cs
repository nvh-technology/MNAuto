using Microsoft.Playwright;
using MNAuto.Models;
using ScavengerMineSDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MNAuto.Services
{
    public class BrowserService
    {
        private readonly IPlaywright _playwright;
        private readonly ConcurrentDictionary<int, IBrowserContext> _browserContexts;
        private readonly ConcurrentDictionary<int, IPage> _pages;
        private readonly ConcurrentDictionary<int, string> _profileDataPaths;
        private readonly ConcurrentDictionary<int, string> _extensionIds;
        private readonly string _extensionPath;
        private LoggingService? _loggingService;
        private DatabaseService? _databaseService;

        public BrowserService(IPlaywright playwright, LoggingService? loggingService = null, DatabaseService? databaseService = null)
        {
            _playwright = playwright;
            _browserContexts = new ConcurrentDictionary<int, IBrowserContext>();
            _pages = new ConcurrentDictionary<int, IPage>();
            _profileDataPaths = new ConcurrentDictionary<int, string>();
            _extensionIds = new ConcurrentDictionary<int, string>();
            _loggingService = loggingService;
            _databaseService = databaseService;
            
            // Lấy đường dẫn đến thư mục extension lace
            var currentDirectory = Directory.GetCurrentDirectory();
            _extensionPath = Path.Combine(currentDirectory, "..", "..", "..", "lace");
            
            // Nếu không tìm thấy, thử các đường dẫn khác
            if (!Directory.Exists(_extensionPath))
            {
                _extensionPath = Path.Combine(currentDirectory, "lace");
            }
            
            _loggingService?.LogInfo("BrowserService", $"Đường dẫn extension Lace: {_extensionPath}");
        }

        // Helper: chờ trang extension sẵn sàng (SPA có thể không kích hoạt đầy đủ LoadState)
        private async Task WaitForExtensionReady(IPage page, int timeoutMs = 60000)
        {
            try
            {
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            }
            catch { }

            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                try
                {
                    var state = await page.EvaluateAsync<string>("() => document.readyState");
                    if (state == "complete" || state == "interactive")
                    {
                        return;
                    }
                }
                catch { }
                await page.WaitForTimeoutAsync(500);
            }
        }

        // Helper: chờ selector hiển thị an toàn
        private async Task<bool> WaitForVisibleAsync(IPage page, string selector, int timeoutMs = 60000)
        {
            try
            {
                await page.WaitForSelectorAsync(selector, new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CreateBrowserContextAsync(Profile profile, bool headless = false)
        {
            try
            {
                // Tạo thư mục dữ liệu riêng cho profile trong thư mục cùng cấp file thực thi
                var baseDir = AppContext.BaseDirectory;
                var profileRoot = Path.Combine(baseDir, "ProfileData");
                Directory.CreateDirectory(profileRoot);
                var profileDataPath = Path.Combine(profileRoot, $"Profile_{profile.Id}");
                Directory.CreateDirectory(profileDataPath);
                _profileDataPaths[profile.Id] = profileDataPath;
                
                _loggingService?.LogInfo(profile.Name, $"Tạo thư mục profile tại: {profileDataPath}");

                // Cấu hình options cho trình duyệt với profile riêng biệt
                var contextOptions = new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    IgnoreHTTPSErrors = true // Bỏ qua lỗi HTTPS
                };

                // Tạo browser context với profile riêng biệt sử dụng LaunchPersistentContext
                // LaunchPersistentContextAsync trả về IBrowserContext trực tiếp
                var context = await _playwright.Chromium.LaunchPersistentContextAsync(profileDataPath, new BrowserTypeLaunchPersistentContextOptions
                {
                    // Sử dụng Chrome hệ thống để tránh phải tải Chromium của Playwright
                    Channel = "chrome",
                    Headless = headless, // Theo yêu cầu: headless cho tất cả trừ nút "Mở trình duyệt"
                    SlowMo = 100,
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    IgnoreHTTPSErrors = true,
                    Args = new[]
                    {
                        $"--disable-extensions-except={_extensionPath}",
                        $"--load-extension={_extensionPath}",
                        "--no-first-run",
                        "--no-default-browser-check",
                        "--disable-blink-features=AutomationControlled",
                        "--disable-web-security",
                        "--disable-features=VizDisplayCompositor",
                        "--profile-directory=Profile_" + profile.Id,
                        "--disable-background-timer-throttling",
                        "--disable-backgrounding-occluded-windows",
                        "--disable-renderer-backgrounding"
                    },
                    Permissions = new[] { "clipboard-read", "clipboard-write" }
                });
                
                // Cấp quyền clipboard (best-effort) cho extension ID được phát hiện
                try
                {
                    var resolvedId = await ResolveExtensionIdAsync(context, profile.Id);
                    if (!string.IsNullOrWhiteSpace(resolvedId))
                    {
                        await context.GrantPermissionsAsync(new[] { "clipboard-read", "clipboard-write" }, new() { Origin = $"chrome-extension://{resolvedId}" });
                        _loggingService?.LogInfo(profile.Name, $"Đã cấp quyền clipboard cho extension: {resolvedId}");
                    }
                    else
                    {
                        _loggingService?.LogWarning(profile.Name, "Không xác định được Extension ID để cấp quyền clipboard");
                    }
                }
                catch (Exception ex)
                {
                    _loggingService?.LogWarning(profile.Name, $"Không thể cấp quyền clipboard cho extension: {ex.Message}");
                }
                
                if (context == null)
                {
                    _loggingService?.LogError(profile.Name, "Không thể tạo browser context");
                    return false;
                }
                
                _browserContexts[profile.Id] = context;
                
                _loggingService?.LogInfo(profile.Name, "Đã tạo browser context với profile riêng biệt");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService?.LogError(profile.Name, $"Lỗi khi tạo browser context: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<bool> InitializeWalletAsync(Profile profile)
        {
            try
            {
                _loggingService?.LogInfo(profile.Name, "Bắt đầu khởi tạo wallet");
                
                if (!_browserContexts.ContainsKey(profile.Id))
                {
                    _loggingService?.LogError(profile.Name, "Không tìm thấy browser context");
                    return false;
                }

                var context = _browserContexts[profile.Id];
                var page = await context.NewPageAsync();
                _pages[profile.Id] = page;

                // Truy cập trang tạo wallet
                _loggingService?.LogInfo(profile.Name, "Truy cập trang tạo wallet");
                if (!await TryGotoExtensionAsync(profile, page, "/#/setup/create"))
                {
                    _loggingService?.LogError(profile.Name, "Không thể mở trang tạo ví Lace");
                    try { await page.CloseAsync(); _pages.TryRemove(profile.Id, out _); } catch {}
                    return false;
                }
                _loggingService?.LogInfo(profile.Name, $"Đã truy cập trang: {page.Url}");

                // Kiểm tra xem đã bị chuyển hướng sang trang assets chưa
                var currentUrl = page.Url;
                // Lấy bản ghi mới nhất từ database để đối chiếu dữ liệu
                var latestProfile = profile;
                if (_databaseService != null)
                {
                    var dbProfile = await _databaseService.GetProfileAsync(profile.Id);
                    if (dbProfile != null) latestProfile = dbProfile;
                }
                
                if (currentUrl.Contains("/assets"))
                {
                    var missingPassword = string.IsNullOrWhiteSpace(latestProfile.WalletPassword);
                    var missingRecovery = string.IsNullOrWhiteSpace(latestProfile.RecoveryPhrase);
                    
                    if (missingPassword || missingRecovery)
                    {
                        _loggingService?.LogWarning(profile.Name, "Bị chuyển hướng sang assets nhưng thiếu dữ liệu (password hoặc Recovery Phrase). Xóa dữ liệu extension và khởi tạo lại.");
                        
                        // Xóa dữ liệu extension/profile
                        var cleared = await ClearExtensionDataAsync(profile.Id);
                        if (!cleared)
                        {
                            _loggingService?.LogError(profile.Name, "Không thể xóa dữ liệu extension/profile");
                            return false;
                        }
                        
                        // Đóng page và context cũ
                        await page.CloseAsync();
                        await CloseBrowserAsync(profile.Id);
                        
                        // Tạo lại context và page
                        var recreated = await CreateBrowserContextAsync(profile);
                        if (!recreated)
                        {
                            _loggingService?.LogError(profile.Name, "Không thể tạo lại context sau khi xóa dữ liệu");
                            return false;
                        }
                        
                        var newContext = _browserContexts[profile.Id];
                        var newPage = await newContext.NewPageAsync();
                        _pages[profile.Id] = newPage;
                        
                        _loggingService?.LogInfo(profile.Name, "Truy cập lại trang tạo wallet");
                        if (!await TryGotoExtensionAsync(profile, newPage, "/#/setup/create"))
                        {
                            _loggingService?.LogError(profile.Name, "Không thể mở lại trang tạo ví sau khi xóa dữ liệu");
                            return false;
                        }
                        
                        // Tiếp tục quy trình tạo ví với newPage
                        page = newPage;
                    }
                    else
                    {
                        _loggingService?.LogInfo(profile.Name, "Wallet đã tồn tại và dữ liệu đầy đủ. Tiếp tục lấy địa chỉ ví.");
                        var ok = await ExtractWalletAddressAsync(latestProfile, page);
                        if (ok && _databaseService != null)
                        {
                            await _databaseService.UpdateProfileAsync(latestProfile);
                        }
                        try { await page.CloseAsync(); _pages.TryRemove(profile.Id, out _); } catch {}
                        return ok;
                    }
                }

                // Bước 1: Click Next
                _loggingService?.LogInfo(profile.Name, "Bước 1: Click Next");
                await WaitForVisibleAsync(page, "[data-testid='wallet-setup-step-btn-next']", 60000);
                await page.ClickAsync("[data-testid='wallet-setup-step-btn-next']");
                await page.WaitForTimeoutAsync(800);

                // Bước 2: Copy recovery phrase (không dùng clipboard, vẫn click để theo luồng UI)
                _loggingService?.LogInfo(profile.Name, "Bước 2: Copy recovery phrase (không đọc clipboard)");
                
                // Xử lý dialog cấp quyền clipboard
                page.Dialog += async (sender, e) =>
                {
                    _loggingService?.LogInfo(profile.Name, $"Dialog xuất hiện: {e.Message}");
                    if (e.Message.Contains("clipboard") || e.Message.Contains("permission"))
                    {
                        _loggingService?.LogInfo(profile.Name, "Tự động chấp nhận quyền clipboard");
                        await e.AcceptAsync();
                    }
                    else
                    {
                        await e.AcceptAsync();
                    }
                };
                
                await WaitForVisibleAsync(page, "[data-testid='copy-to-clipboard-button']", 60000);
                await page.ClickAsync("[data-testid='copy-to-clipboard-button']");
                
                // Chờ một chút để UI ổn định
                await page.WaitForTimeoutAsync(1000);
                
                // Thu thập 24 từ từ các phần tử writedown và nối thành recovery phrase
                var words = new List<string>();
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    var wordEls = await page.QuerySelectorAllAsync("p[data-testid='mnemonic-word-writedown']");
                    if (wordEls != null && wordEls.Count >= 24)
                    {
                        words.Clear();
                        foreach (var el in wordEls)
                        {
                            try
                            {
                                var t = (await el.InnerTextAsync())?.Trim();
                                if (!string.IsNullOrWhiteSpace(t)) words.Add(t);
                            }
                            catch { }
                        }
                        if (words.Count >= 24) break;
                    }
                    await page.WaitForTimeoutAsync(300);
                }
                
                // Đảm bảo có đủ 24 từ
                if (words.Count < 24)
                {
                    _loggingService?.LogWarning(profile.Name, $"Chỉ thu thập được {words.Count} từ mnemonic (yêu cầu 24). Vẫn tiếp tục với những gì có.");
                }
                
                // Cắt đúng 24 từ (nếu thừa) rồi nối với dấu cách
                var words24 = words.Take(24).ToList();
                var recoveryPhrase = string.Join(" ", words24);
                profile.RecoveryPhrase = recoveryPhrase;
                _loggingService?.LogInfo(profile.Name, $"Đã ghép recovery phrase từ UI: {recoveryPhrase.Substring(0, Math.Min(20, recoveryPhrase?.Length ?? 0))}...");
                
                // Lưu ngay vào database khi có Recovery Phrase
                if (_databaseService != null)
                {
                    await _databaseService.UpdateProfileAsync(profile);
                }

                // Bước 3: Click Next
                _loggingService?.LogInfo(profile.Name, "Bước 3: Click Next");
                await WaitForVisibleAsync(page, "[data-testid='wallet-setup-step-btn-next']", 60000);
                await page.ClickAsync("[data-testid='wallet-setup-step-btn-next']");
                await page.WaitForTimeoutAsync(800);

                // Bước 4: Điền recovery phrase vào từng ô (không dùng Paste from clipboard)
                _loggingService?.LogInfo(profile.Name, "Bước 4: Điền recovery phrase vào 24 ô nhập");
                
                // Tách 24 từ
                var fillWords = (profile.RecoveryPhrase ?? string.Empty)
                    .Split(new[] { ' ', '\n', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Take(24)
                    .ToArray();
                
                await WaitForVisibleAsync(page, "input[data-testid='mnemonic-word-input']", 60000);
                
                // Lấy tất cả input
                var inputs = await page.QuerySelectorAllAsync("input[data-testid='mnemonic-word-input']");
                
                // Đợi tối đa để đủ 24 input
                for (int i = 0; i < 10 && (inputs == null || inputs.Count < 24); i++)
                {
                    await page.WaitForTimeoutAsync(300);
                    inputs = await page.QuerySelectorAllAsync("input[data-testid='mnemonic-word-input']");
                }
                
                if (inputs == null || inputs.Count == 0)
                {
                    throw new Exception("Không tìm thấy ô nhập mnemonic-word-input");
                }
                
                var maxFill = Math.Min(fillWords.Length, inputs.Count);
                
                // Điền lần lượt
                for (int i = 0; i < maxFill; i++)
                {
                    try
                    {
                        await inputs[i].FillAsync(fillWords[i]);
                        await page.WaitForTimeoutAsync(50);
                    }
                    catch (Exception ex)
                    {
                        _loggingService?.LogWarning(profile.Name, $"Lỗi điền từ mnemonic tại vị trí {i + 1}: {ex.Message}");
                    }
                }
                
                // Nếu còn thiếu, log cảnh báo
                if (maxFill < 24)
                {
                    _loggingService?.LogWarning(profile.Name, $"Chỉ điền được {maxFill}/24 từ mnemonic.");
                }
                
                // Chờ một chút trước khi Next
                await page.WaitForTimeoutAsync(500);

                // Bước 5: Click Next
                _loggingService?.LogInfo(profile.Name, "Bước 5: Click Next");
                await WaitForVisibleAsync(page, "[data-testid='wallet-setup-step-btn-next']", 60000);
                await page.ClickAsync("[data-testid='wallet-setup-step-btn-next']");
                await page.WaitForTimeoutAsync(800);

                // Bước 6: Nhập mật khẩu wallet
                _loggingService?.LogInfo(profile.Name, "Bước 6: Nhập mật khẩu wallet");
                await WaitForVisibleAsync(page, "input[data-testid='wallet-password-verification-input']", 60000);
                await WaitForVisibleAsync(page, "input[data-testid='wallet-password-confirmation-input']", 60000);
                await page.FillAsync("input[data-testid='wallet-password-verification-input']", profile.WalletPassword);
                await page.FillAsync("input[data-testid='wallet-password-confirmation-input']", profile.WalletPassword);

                // Bước 7: Click Open wallet
                _loggingService?.LogInfo(profile.Name, "Bước 7: Click Open wallet");
                await ClickButtonByText(page, "Open wallet");
                
                // Chờ trang load xong và đảm bảo UI sẵn sàng
                _loggingService?.LogInfo(profile.Name, "Chờ trang load xong");
                await WaitForExtensionReady(page);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await page.WaitForTimeoutAsync(2000);
                
                // Điều hướng sang trang assets để đảm bảo hiển thị địa chỉ ví
                if (!await TryGotoExtensionAsync(profile, page, "/#/assets"))
                {
                    _loggingService?.LogError(profile.Name, "Không thể điều hướng tới trang assets");
                    try { await page.CloseAsync(); _pages.TryRemove(profile.Id, out _); } catch {}
                    return false;
                }

                // Lấy địa chỉ wallet
                _loggingService?.LogInfo(profile.Name, "Bắt đầu lấy địa chỉ wallet");
                var __result = await ExtractWalletAddressAsync(profile, page);
                try { await page.CloseAsync(); _pages.TryRemove(profile.Id, out _); } catch {}
                return __result;

            }
            catch (Exception ex)
            {
                try { if (_pages.ContainsKey(profile.Id)) { await _pages[profile.Id].CloseAsync(); _pages.TryRemove(profile.Id, out _); } } catch {}
                _loggingService?.LogError(profile.Name, $"Lỗi khi khởi tạo wallet: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<bool> ExtractWalletAddressAsync(Profile profile, IPage page)
        {
            try
            {
                _loggingService?.LogInfo(profile.Name, "Bắt đầu tìm địa chỉ wallet");
                 
                // Điều hướng sang trang assets nếu chưa ở đúng route
                var currentUrl = page.Url;
                if (!currentUrl.Contains("/assets"))
                {
                    _loggingService?.LogInfo(profile.Name, "Chưa ở trang assets, điều hướng tới /assets");
                    var okNav = await TryGotoExtensionAsync(profile, page, "/#/assets");
                    if (!okNav)
                    {
                        _loggingService?.LogError(profile.Name, "Không thể mở trang assets để lấy địa chỉ ví");
                        return false;
                    }
                }
                
                // Chờ và tìm địa chỉ wallet
                var maxAttempts = 20;
                for (int i = 0; i < maxAttempts; i++)
                {
                    try
                    {
                        _loggingService?.LogInfo(profile.Name, $"Thử tìm địa chỉ wallet - Lần {i + 1}/{maxAttempts}");
                        var addressElement = await page.QuerySelectorAsync("[data-testid='info-wallet-full-address']");
                        if (addressElement != null)
                        {
                            var walletAddress = await addressElement.InnerTextAsync();
                            profile.WalletAddress = walletAddress.Trim();
                            _loggingService?.LogInfo(profile.Name, $"Đã tìm thấy địa chỉ wallet: {walletAddress}");
                            
                            // Lưu ngay vào database khi có địa chỉ ví
                            if (_databaseService != null)
                            {
                                await _databaseService.UpdateProfileAsync(profile);
                            }
                            
                            return true;
                        }
                        else
                        {
                            _loggingService?.LogInfo(profile.Name, "Chưa tìm thấy element địa chỉ wallet, chờ 3 giây");
                            await page.WaitForTimeoutAsync(3000);
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService?.LogWarning(profile.Name, $"Lỗi khi tìm địa chỉ wallet - Lần {i + 1}: {ex.Message}");
                        // Nếu không tìm thấy, chờ 3 giây rồi thử lại
                        await page.WaitForTimeoutAsync(3000);
                    }
                }
                
                _loggingService?.LogError(profile.Name, "Không tìm thấy địa chỉ wallet sau 20 lần thử");
                return false;
            }
            catch (Exception ex)
            {
                _loggingService?.LogError(profile.Name, $"Lỗi khi lấy địa chỉ wallet: {ex.Message}", ex);
                return false;
            }
        }

        private async Task ClickButtonByText(IPage page, string buttonText)
        {
            try
            {
                // Tìm button theo text chứa (contains) thay vì chính xác
                var button = await page.QuerySelectorAsync($"button:has-text(\"{buttonText}\")");
                if (button == null)
                {
                    // Thử tìm theo selector khác với contains
                    button = await page.QuerySelectorAsync($"//*[contains(text(), '{buttonText}')]");
                }
                
                if (button == null)
                {
                    // Thử tìm theo class hoặc role
                    button = await page.QuerySelectorAsync($"button[role='button']:has-text(\"{buttonText}\")");
                }
                
                if (button == null)
                {
                    // Thử tìm theo input button
                    button = await page.QuerySelectorAsync($"input[type='button'][value*='{buttonText}']");
                }
                
                if (button == null)
                {
                    // Log tất cả các button trên trang để debug
                    var allButtons = await page.QuerySelectorAllAsync("button, input[type='button'], [role='button']");
                    _loggingService?.LogError("BrowserService", $"Không tìm thấy button với text: {buttonText}");
                    _loggingService?.LogInfo("BrowserService", $"Tổng số button tìm thấy: {allButtons.Count}");
                    
                    for (int i = 0; i < Math.Min(allButtons.Count, 5); i++)
                    {
                        var btnText = await allButtons[i].InnerTextAsync();
                        _loggingService?.LogInfo("BrowserService", $"Button {i}: '{btnText}'");
                    }
                    
                    throw new Exception($"Không tìm thấy button với text: {buttonText}");
                }
                
                await button.ClickAsync();
                await page.WaitForTimeoutAsync(500); // Chờ một chút để action được thực hiện
                _loggingService?.LogInfo("BrowserService", $"Đã click button: {buttonText}");
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("BrowserService", $"Lỗi khi click button {buttonText}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> StartBrowserAsync(Profile profile, bool headless = false)
        {
            try
            {
                _loggingService?.LogInfo(profile.Name, "Bắt đầu khởi động trình duyệt với profile riêng");
                
                if (!_browserContexts.ContainsKey(profile.Id))
                {
                    _loggingService?.LogInfo(profile.Name, "Chưa có browser context, tạo mới");
                    await CreateBrowserContextAsync(profile, headless);
                }

                var context = _browserContexts[profile.Id];
                var page = await context.NewPageAsync();
                _pages[profile.Id] = page;

                // Truy cập trang extension
                _loggingService?.LogInfo(profile.Name, "Truy cập trang extension");
                if (!await TryGotoExtensionAsync(profile, page, ""))
                {
                    _loggingService?.LogError(profile.Name, "Không thể mở trang extension");
                    return false;
                }
                
                // Nếu đã có địa chỉ ví trong hồ sơ, ưu tiên xác nhận từ trang assets để tránh khởi tạo lại
                if (!string.IsNullOrWhiteSpace(profile.WalletAddress))
                {
                    _loggingService?.LogInfo(profile.Name, "Đã có địa chỉ ví trong hồ sơ, truy cập trang assets để xác nhận");
                    var __navOk = await TryGotoExtensionAsync(profile, page, "/#/assets");
                    if (!__navOk)
                    {
                        _loggingService?.LogWarning(profile.Name, "Không thể mở trang assets; sẽ thử xác nhận địa chỉ trực tiếp.");
                    }
                    
                    var confirmed = await ExtractWalletAddressAsync(profile, page);
                    if (!confirmed)
                    {
                        _loggingService?.LogWarning(profile.Name, "Không xác nhận được địa chỉ ví từ trang assets, tiến hành khởi tạo ví");
                        await InitializeWalletAsync(profile);
                    }
                }
                else
                {
                    // Chưa có địa chỉ ví trong hồ sơ: kiểm tra URL hiện tại để quyết định khởi tạo hay lấy địa chỉ
                    var currentUrl = page.Url;
                    _loggingService?.LogInfo(profile.Name, $"URL hiện tại: {currentUrl}");
                    
                    if (currentUrl.Contains("/assets"))
                    {
                        _loggingService?.LogInfo(profile.Name, "Wallet đã tồn tại, kiểm tra địa chỉ ví");
                        await ExtractWalletAddressAsync(profile, page);
                    }
                    else
                    {
                        _loggingService?.LogInfo(profile.Name, "Chưa có wallet, bắt đầu khởi tạo");
                        await InitializeWalletAsync(profile);
                    }
                }
                
                _loggingService?.LogInfo(profile.Name, "Khởi động trình duyệt thành công");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService?.LogError(profile.Name, $"Lỗi khi khởi động trình duyệt: {ex.Message}", ex);
                return false;
            }
        }

        // Đã loại bỏ phương thức OpenBrowserForUserAsync

        public async Task CloseBrowserAsync(int profileId)
        {
            try
            {
                var profileName = $"Profile{profileId}";
                _loggingService?.LogInfo(profileName, "Bắt đầu đóng trình duyệt");
                
                if (_pages.ContainsKey(profileId))
                {
                    await _pages[profileId].CloseAsync();
                    _pages.TryRemove(profileId, out _);
                    _loggingService?.LogInfo(profileName, "Đã đóng page");
                }

                if (_browserContexts.ContainsKey(profileId))
                {
                    await _browserContexts[profileId].CloseAsync();
                    _browserContexts.TryRemove(profileId, out _);
                    _loggingService?.LogInfo(profileName, "Đã đóng browser context");
                }
                
                _loggingService?.LogInfo(profileName, "Đã đóng trình duyệt thành công");
            }
            catch (Exception ex)
            {
                _loggingService?.LogError($"Profile{profileId}", $"Lỗi khi đóng trình duyệt: {ex.Message}", ex);
            }
        }

        public async Task CloseAllBrowsersAsync()
        {
            var profileIds = _browserContexts.Keys.ToList();
            foreach (var profileId in profileIds)
            {
                await CloseBrowserAsync(profileId);
            }
        }

        public bool IsBrowserRunning(int profileId)
        {
            return _browserContexts.ContainsKey(profileId);
        }
        
        private async Task<bool> ClearExtensionDataAsync(int profileId)
        {
            try
            {
                var profileName = $"Profile{profileId}";
                _loggingService?.LogInfo(profileName, "Xóa dữ liệu extension/profile");
                
                if (_profileDataPaths.TryGetValue(profileId, out var dir))
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                    Directory.CreateDirectory(dir);
                    _loggingService?.LogInfo(profileName, $"Đã tạo lại thư mục profile: {dir}");
                    return true;
                }
                else
                {
                    var baseDir = AppContext.BaseDirectory;
                    var profileRoot = Path.Combine(baseDir, "ProfileData");
                    Directory.CreateDirectory(profileRoot);
                    var newDir = Path.Combine(profileRoot, $"Profile_{profileId}");
                    Directory.CreateDirectory(newDir);
                    _profileDataPaths[profileId] = newDir;
                    _loggingService?.LogInfo(profileName, $"Đã khởi tạo thư mục profile: {newDir}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService?.LogError($"Profile{profileId}", $"Lỗi khi xóa dữ liệu extension/profile: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Tự động hóa việc ký message với ScavengerMine
        /// </summary>
        public async Task<(string signature, string pubkey)?> SignMessageAsync(Profile profile, string messageToSign)
        {
            try
            {
                _loggingService?.LogInfo(profile.Name, "Bắt đầu quá trình ký message với ScavengerMine");
                
                if (!_browserContexts.ContainsKey(profile.Id))
                {
                    _loggingService?.LogError(profile.Name, "Không tìm thấy browser context");
                    return null;
                }

                var context = _browserContexts[profile.Id];
                var page = await context.NewPageAsync();
                
                // Mở trang extension
                _loggingService?.LogInfo(profile.Name, "Mở trang Lace extension");
                if (!await TryGotoExtensionAsync(profile, page, ""))
                {
                    _loggingService?.LogError(profile.Name, "Không thể mở trang Lace extension để ký message");
                    return null;
                }
                
                // Click menu
                _loggingService?.LogInfo(profile.Name, "Click menu");
                await WaitForVisibleAsync(page, "[id=\"menu\"]", 60000);
                await page.ClickAsync("[id=\"menu\"]");
                await page.WaitForTimeoutAsync(1000);
                
                // Click Sign Message
                _loggingService?.LogInfo(profile.Name, "Click Sign Message");
                await WaitForVisibleAsync(page, "[data-testid=\"header-menu-sign-message\"]", 60000);
                await page.ClickAsync("[data-testid=\"header-menu-sign-message\"]");
                await page.WaitForTimeoutAsync(1500);
                
                // Click select address button
                _loggingService?.LogInfo(profile.Name, "Click select address button");
                await WaitForVisibleAsync(page, "button[data-testid=\"select-address-button\"]", 60000);
                await page.ClickAsync("button[data-testid=\"select-address-button\"]");
                await WaitForVisibleAsync(page, "[data-testid=\"address-dropdown-menu\"]", 60000);
                
                // Tìm và click vào address có chứa "Payment"
                _loggingService?.LogInfo(profile.Name, "Tìm address có chứa 'Payment'");
                var addressItems = await page.QuerySelectorAllAsync("[data-testid=\"address-dropdown-menu\"] li");
                bool foundPayment = false;
                
                foreach (var item in addressItems)
                {
                    var text = await item.InnerTextAsync();
                    if (text.Contains("Payment"))
                    {
                        await item.ClickAsync();
                        foundPayment = true;
                        _loggingService?.LogInfo(profile.Name, "Đã chọn address Payment");
                        break;
                    }
                }
                
                if (!foundPayment)
                {
                    _loggingService?.LogError(profile.Name, "Không tìm thấy address có chứa 'Payment'");
                    return null;
                }
                
                await page.WaitForTimeoutAsync(1000);
                
                // Paste message to sign
                _loggingService?.LogInfo(profile.Name, "Paste message to sign");
                await WaitForVisibleAsync(page, "textarea[data-testid=\"sign-message-input\"]", 60000);
                await page.FillAsync("textarea[data-testid=\"sign-message-input\"]", messageToSign);
                await page.WaitForTimeoutAsync(1000);
                
                // Click sign message button
                _loggingService?.LogInfo(profile.Name, "Click sign message button");
                await page.ClickAsync("button[data-testid=\"sign-message-button\"]");
                await page.WaitForTimeoutAsync(2000);
                
                // Nhập password
                _loggingService?.LogInfo(profile.Name, "Nhập password");
                await WaitForVisibleAsync(page, "[data-testid=\"password-input\"]", 60000);
                await page.FillAsync("[data-testid=\"password-input\"]", profile.WalletPassword);
                await page.WaitForTimeoutAsync(1000);
                
                // Click sign message button lần nữa
                _loggingService?.LogInfo(profile.Name, "Xác nhận ký message");
                await page.ClickAsync("button[data-testid=\"sign-message-button\"]");
                await page.WaitForTimeoutAsync(3000);
                
                // Lấy signature
                _loggingService?.LogInfo(profile.Name, "Lấy signature");
                await page.WaitForSelectorAsync("textarea[data-testid=\"sign-message-signature-input\"]", new() { State = WaitForSelectorState.Visible });
                string signature = await page.InputValueAsync("textarea[data-testid=\"sign-message-signature-input\"]");
                for (int i = 0; i < 10 && string.IsNullOrWhiteSpace(signature); i++)
                {
                    await page.WaitForTimeoutAsync(500);
                    signature = await page.InputValueAsync("textarea[data-testid=\"sign-message-signature-input\"]");
                }
                if (string.IsNullOrWhiteSpace(signature))
                {
                    _loggingService?.LogError(profile.Name, "Signature trống sau khi ký");
                    return null;
                }

                // Lấy pubkey
                _loggingService?.LogInfo(profile.Name, "Lấy pubkey");
                await page.WaitForSelectorAsync("textarea[data-testid=\"sign-message-raw-key-input\"]", new() { State = WaitForSelectorState.Visible });
                string pubkey = await page.InputValueAsync("textarea[data-testid=\"sign-message-raw-key-input\"]");
                for (int i = 0; i < 10 && string.IsNullOrWhiteSpace(pubkey); i++)
                {
                    await page.WaitForTimeoutAsync(500);
                    pubkey = await page.InputValueAsync("textarea[data-testid=\"sign-message-raw-key-input\"]");
                }
                if (string.IsNullOrWhiteSpace(pubkey))
                {
                    _loggingService?.LogError(profile.Name, "Pubkey trống sau khi ký");
                    return null;
                }

                // Đóng page
                await page.CloseAsync();

                _loggingService?.LogInfo(profile.Name, $"Đã ký message thành công. Signature: {signature.Substring(0, Math.Min(20, signature.Length))}...");
                _loggingService?.LogInfo(profile.Name, $"Pubkey: {pubkey}");

                return (signature, pubkey);
            }
            catch (Exception ex)
            {
                _loggingService?.LogError(profile.Name, $"Lỗi khi ký message: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Đăng ký địa chỉ với ScavengerMine một cách tự động
        /// </summary>
        public async Task<bool> RegisterAddressAsync(Profile profile, ScavengerMineService scavengerService)
        {
            try
            {
                _loggingService?.LogInfo(profile.Name, "Bắt đầu đăng ký địa chỉ với ScavengerMine");
                
                // Lấy Terms and Conditions
                var terms = await scavengerService.GetTermsAndConditionsAsync();
                var messageToSign = terms.Message;
                
                _loggingService?.LogInfo(profile.Name, $"Message to sign: {messageToSign.Substring(0, Math.Min(50, messageToSign?.Length ?? 0))}...");
                
                // Đảm bảo có địa chỉ ví trước khi đăng ký
                if (string.IsNullOrWhiteSpace(profile.WalletAddress))
                {
                    _loggingService?.LogInfo(profile.Name, "Chưa có địa chỉ ví trong hồ sơ, thử lấy từ trang assets trước khi đăng ký");
                    if (_browserContexts.ContainsKey(profile.Id))
                    {
                        var ctx = _browserContexts[profile.Id];
                        var addrPage = await ctx.NewPageAsync();
                        var navOk = await TryGotoExtensionAsync(profile, addrPage, "/#/assets");
                        if (!navOk)
                        {
                            _loggingService?.LogWarning(profile.Name, "Không thể mở trang assets để lấy địa chỉ trước khi đăng ký");
                        }
                        var got = await ExtractWalletAddressAsync(profile, addrPage);
                        await addrPage.CloseAsync();
                        if (!got)
                        {
                            _loggingService?.LogWarning(profile.Name, "Không thể lấy địa chỉ ví trước khi đăng ký, tiếp tục quy trình đăng ký");
                        }
                        else if (_databaseService != null)
                        {
                            await _databaseService.UpdateProfileAsync(profile);
                        }
                    }
                    else
                    {
                        _loggingService?.LogWarning(profile.Name, "Không có browser context để lấy địa chỉ ví trước khi đăng ký");
                    }
                }
                
                // Ký message
                var signResult = await SignMessageAsync(profile, messageToSign);
                if (signResult == null)
                {
                    _loggingService?.LogError(profile.Name, "Không thể ký message");
                    return false;
                }
                
                // Đăng ký với ScavengerMine
                var registered = await scavengerService.RegisterAddressAsync(profile, signResult.Value.signature, signResult.Value.pubkey);
                
                if (registered)
                {
                    _loggingService?.LogInfo(profile.Name, "Đăng ký địa chỉ thành công với ScavengerMine");
                }
                else
                {
                    _loggingService?.LogError(profile.Name, "Đăng ký địa chỉ thất bại");
                }
                
                return registered;
            }
            catch (Exception ex)
            {
                _loggingService?.LogError(profile.Name, $"Lỗi khi đăng ký địa chỉ: {ex.Message}", ex);
                return false;
            }
        }

        // Điều hướng tới trang extension với retry chống net::ERR_ABORTED
        private async Task<bool> TryGotoExtensionAsync(Profile profile, IPage page, string pathSuffix, int maxAttempts = 5, int delayMs = 800)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var url = await BuildExtensionUrlAsync(profile.Id, pathSuffix);
                    await page.GotoAsync(url);
                    await WaitForExtensionReady(page);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    return true;
                }
                catch (PlaywrightException ex) when (ex.Message.Contains("ERR_ABORTED", StringComparison.OrdinalIgnoreCase))
                {
                    _loggingService?.LogWarning(profile.Name, $"Điều hướng extension bị ERR_ABORTED (lần {attempt}/{maxAttempts}). Thử lại...");
                    await page.WaitForTimeoutAsync(delayMs);
                }
                catch (Exception ex)
                {
                    _loggingService?.LogWarning(profile.Name, $"Lỗi điều hướng extension (lần {attempt}/{maxAttempts}): {ex.Message}");
                    await page.WaitForTimeoutAsync(delayMs);
                }
            }
            _loggingService?.LogError(profile.Name, "Không thể điều hướng tới trang extension sau nhiều lần thử");
            return false;
        }

        // Xây dựng URL trang extension theo Extension ID được phát hiện động
        private async Task<string> BuildExtensionUrlAsync(int profileId, string pathSuffix)
        {
            try
            {
                if (!_browserContexts.TryGetValue(profileId, out var context))
                    throw new Exception("Không có browser context cho profile");

                if (!_extensionIds.TryGetValue(profileId, out var extId) || string.IsNullOrWhiteSpace(extId))
                {
                    var resolved = await ResolveExtensionIdAsync(context, profileId);
                    if (string.IsNullOrWhiteSpace(resolved))
                        throw new Exception("Không thể xác định Extension ID");
                    _extensionIds[profileId] = resolved!;
                    _loggingService?.LogInfo($"Profile{profileId}", $"Đã xác định Extension ID: {resolved}");
                    extId = resolved!;
                }

                // Chuẩn hóa suffix để luôn tương thích hash routing "#/..."
                var suffix = pathSuffix ?? string.Empty;
                if (!string.IsNullOrEmpty(suffix))
                {
                    if (suffix.StartsWith("/#/"))
                    {
                        suffix = suffix.Substring(1); // "/#/..." -> "#/..."
                    }
                    else if (suffix.StartsWith("/"))
                    {
                        suffix = "#" + suffix;        // "/assets" -> "#/assets"
                    }
                    else if (!suffix.StartsWith("#/"))
                    {
                        suffix = "#/" + suffix.TrimStart('#', '/'); // "assets" -> "#/assets"
                    }
                }

                return $"chrome-extension://{extId}/app.html{suffix}";
            }
            catch (Exception ex)
            {
                _loggingService?.LogError($"Profile{profileId}", $"Lỗi BuildExtensionUrl: {ex.Message}", ex);
                // Fallback: dùng ID cố định cũ để không làm vỡ luồng hiện tại và CHUẨN HÓA suffix tương tự nhánh chính
                var fallbackSuffix = pathSuffix ?? string.Empty;
                if (!string.IsNullOrEmpty(fallbackSuffix))
                {
                    if (fallbackSuffix.StartsWith("/#/"))
                    {
                        fallbackSuffix = fallbackSuffix.Substring(1); // "/#/..." -> "#/..."
                    }
                    else if (fallbackSuffix.StartsWith("/"))
                    {
                        fallbackSuffix = "#" + fallbackSuffix;        // "/assets" -> "#/assets"
                    }
                    else if (!fallbackSuffix.StartsWith("#/"))
                    {
                        fallbackSuffix = "#/" + fallbackSuffix.TrimStart('#', '/'); // "assets" -> "#/assets"
                    }
                }
                return $"chrome-extension://gafhhkghbfjjkeiendhlofajokpaflmk/app.html{fallbackSuffix}";
            }
        }

        // Cố gắng phát hiện Extension ID từ Service Worker/Paging của Chromium MV3
        private async Task<string?> ResolveExtensionIdAsync(IBrowserContext context, int profileId, int timeoutMs = 10000)
        {
            // Cách 1 (ưu tiên, ổn định): đọc từ thư mục profile "Extensions/<extId>/<version>"
            try
            {
                if (_profileDataPaths.TryGetValue(profileId, out var profileDir))
                {
                    var extRoot = Path.Combine(profileDir, "Extensions");
                    if (Directory.Exists(extRoot))
                    {
                        var idDirs = Directory.GetDirectories(extRoot);
                        foreach (var idDir in idDirs)
                        {
                            var id = Path.GetFileName(idDir);
                            if (!string.IsNullOrWhiteSpace(id) && id.Length == 32)
                            {
                                return id;
                            }
                        }
                    }
                }
            }
            catch { }

            // Cách 2 (fallback): dò các trang đang mở có scheme chrome-extension://
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                try
                {
                    foreach (var p in context.Pages)
                    {
                        var url = p.Url;
                        if (!string.IsNullOrEmpty(url) && url.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
                        {
                            var uri = new Uri(url);
                            var id = uri.Host;
                            if (!string.IsNullOrWhiteSpace(id))
                                return id;
                        }
                    }
                }
                catch { }

                await Task.Delay(300);
            }

            return null;
        }
    }
}