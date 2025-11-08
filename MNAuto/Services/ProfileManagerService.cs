using Microsoft.Playwright;
using MNAuto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNAuto.Services
{
    public class ProfileManagerService
    {
        private readonly DatabaseService _databaseService;
        private readonly LoggingService _loggingService;
        private BrowserService? _browserService;
        private IPlaywright? _playwright;

        public ProfileManagerService(DatabaseService databaseService, LoggingService loggingService)
        {
            _databaseService = databaseService;
            _loggingService = loggingService;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _playwright = await Playwright.CreateAsync();
                _browserService = new BrowserService(_playwright, _loggingService, _databaseService);
                _loggingService.LogInfo("System", "Khởi tạo Profile Manager thành công");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("System", "Lỗi khi khởi tạo Profile Manager", ex);
                throw;
            }
        }

        public async Task<List<Profile>> CreateProfilesAsync(int count)
        {
            var profiles = new List<Profile>();
            
            try
            {
                _loggingService.LogInfo("System", $"Bắt đầu tạo {count} profile mới");

                for (int i = 1; i <= count; i++)
                {
                    var profile = new Profile
                    {
                        Name = $"Profile{i}",
                        WalletPassword = GenerateRandomPassword(15),
                        Status = ProfileStatus.Initializing
                    };

                    var profileId = await _databaseService.CreateProfileAsync(profile);
                    profile.Id = profileId;
                    
                    profiles.Add(profile);
                    _loggingService.LogInfo(profile.Name, "Đã tạo profile thành công");
                }

                _loggingService.LogInfo("System", $"Đã tạo xong {count} profile");
                return profiles;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("System", $"Lỗi khi tạo profiles", ex);
                throw;
            }
        }

        public async Task<List<Profile>> GetAllProfilesAsync()
        {
            try
            {
                return await _databaseService.GetAllProfilesAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("System", "Lỗi khi lấy danh sách profiles", ex);
                return new List<Profile>();
            }
        }

        public async Task<bool> InitializeProfileAsync(int profileId)
        {
            try
            {
                var profile = await _databaseService.GetProfileAsync(profileId);
                if (profile == null)
                {
                    _loggingService.LogError("System", $"Không tìm thấy profile với ID: {profileId}");
                    return false;
                }

                _loggingService.LogInfo(profile.Name, "Bắt đầu khởi tạo trình duyệt");
                
                // Cập nhật trạng thái
                await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.Initializing);

                if (_browserService == null)
                {
                    _loggingService.LogError("System", "Browser service chưa được khởi tạo");
                    return false;
                }

                // Tạo browser context (bắt buộc headful để sử dụng Chrome Extension)
                var contextCreated = await _browserService.CreateBrowserContextAsync(profile, headless: false);
                if (!contextCreated)
                {
                    _loggingService.LogError(profile.Name, "Không thể tạo browser context");
                    return false;
                }

                // Khởi tạo wallet
                var walletInitialized = await _browserService.InitializeWalletAsync(profile);
                if (!walletInitialized)
                {
                    _loggingService.LogError(profile.Name, "Không thể khởi tạo wallet");
                    return false;
                }

                // Cập nhật thông tin profile
                await _databaseService.UpdateProfileAsync(profile);
                
                // Đóng trình duyệt sau khi khởi tạo xong
                await _browserService.CloseBrowserAsync(profileId);
                
                // Cập nhật trạng thái
                await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.NotStarted);
                
                _loggingService.LogInfo(profile.Name, "Khởi tạo trình duyệt thành công");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Profile{profileId}", "Lỗi khi khởi tạo trình duyệt", ex);
                await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.Stopped);
                return false;
            }
        }

        public async Task<bool> StartProfileAsync(int profileId, bool headless = false)
        {
            try
            {
                var profile = await _databaseService.GetProfileAsync(profileId);
                if (profile == null)
                {
                    _loggingService.LogError("System", $"Không tìm thấy profile với ID: {profileId}");
                    return false;
                }

                _loggingService.LogInfo(profile.Name, "Bắt đầu khởi động trình duyệt");
                
                // Cập nhật trạng thái
                await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.Running);

                if (_browserService == null)
                {
                    _loggingService.LogError("System", "Browser service chưa được khởi tạo");
                    return false;
                }

                // Khởi động trình duyệt
                var started = await _browserService.StartBrowserAsync(profile, headless);
                if (!started)
                {
                    _loggingService.LogError(profile.Name, "Không thể khởi động trình duyệt");
                    await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.Stopped);
                    return false;
                }

                // Cập nhật thông tin profile (nếu có thay đổi)
                await _databaseService.UpdateProfileAsync(profile);
                
                _loggingService.LogInfo(profile.Name, "Khởi động trình duyệt thành công");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Profile{profileId}", "Lỗi khi khởi động trình duyệt", ex);
                await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.Stopped);
                return false;
            }
        }

        // Đã loại bỏ phương thức OpenProfileForUserAsync

        public async Task<bool> CloseProfileAsync(int profileId)
        {
            try
            {
                var profile = await _databaseService.GetProfileAsync(profileId);
                if (profile == null)
                {
                    _loggingService.LogError("System", $"Không tìm thấy profile với ID: {profileId}");
                    return false;
                }

                _loggingService.LogInfo(profile.Name, "Đóng trình duyệt");

                if (_browserService == null)
                {
                    _loggingService.LogError("System", "Browser service chưa được khởi tạo");
                    return false;
                }

                // Đóng trình duyệt
                await _browserService.CloseBrowserAsync(profileId);
                
                // Cập nhật trạng thái
                await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.Stopped);
                
                _loggingService.LogInfo(profile.Name, "Đã đóng trình duyệt thành công");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Profile{profileId}", "Lỗi khi đóng trình duyệt", ex);
                return false;
            }
        }

        public async Task<List<bool>> InitializeMultipleProfilesAsync(List<int> profileIds, int maxDegreeOfParallelism = 3)
        {
            var semaphore = new SemaphoreSlim(Math.Max(1, maxDegreeOfParallelism));
            var tasks = profileIds.Select(async id =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await InitializeProfileAsync(id);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        public async Task<List<bool>> StartMultipleProfilesAsync(List<int> profileIds, int maxDegreeOfParallelism = 3, bool headless = false)
        {
            var semaphore = new SemaphoreSlim(Math.Max(1, maxDegreeOfParallelism));
            var tasks = profileIds.Select(async id =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await StartProfileAsync(id, headless);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        // Đã loại bỏ phương thức OpenMultipleProfilesForUserAsync

        public async Task<List<bool>> CloseMultipleProfilesAsync(List<int> profileIds, int maxDegreeOfParallelism = 3)
        {
            var semaphore = new SemaphoreSlim(Math.Max(1, maxDegreeOfParallelism));
            var tasks = profileIds.Select(async id =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await CloseProfileAsync(id);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        public bool IsProfileRunning(int profileId)
        {
            if (_browserService == null)
                return false;
                
            return _browserService.IsBrowserRunning(profileId);
        }

        /// <summary>
        /// Lấy BrowserService để sử dụng trực tiếp
        /// </summary>
        public BrowserService? GetBrowserService()
        {
            return _browserService;
        }

        private string GenerateRandomPassword(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public async Task DisposeAsync()
        {
            try
            {
                if (_browserService != null)
                {
                    await _browserService.CloseAllBrowsersAsync();
                }
                
                _playwright?.Dispose();
                _loggingService.LogInfo("System", "Đã dọn dẹp tài nguyên Profile Manager");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("System", "Lỗi khi dọn dẹp tài nguyên", ex);
            }
        }
    }
}