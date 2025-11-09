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

        public async Task<List<Profile>> CreateProfilesAsync(int count, string walletPassword)
        {
            if (string.IsNullOrWhiteSpace(walletPassword))
                throw new ArgumentException("Vui lòng nhập mật khẩu ví hợp lệ", nameof(walletPassword));
            // Validate: tối thiểu 8 ký tự và có cả số lẫn chữ
            bool hasLetter = walletPassword.Any(char.IsLetter);
            bool hasDigit = walletPassword.Any(char.IsDigit);
            if (walletPassword.Length < 8 || !hasLetter || !hasDigit)
                throw new ArgumentException("Mật khẩu phải tối thiểu 8 ký tự và bao gồm cả số và chữ", nameof(walletPassword));
            var profiles = new List<Profile>();
             
            try
            {
                _loggingService.LogInfo("System", $"Bắt đầu tạo {count} profile mới");

                // Tạo profile sao cho Name và thư mục ProfileData đồng bộ theo Id: "Profile {id}"
                for (int i = 0; i < count; i++)
                {
                    // Đặt tên tạm thời để chèn (UNIQUE Name yêu cầu khác rỗng và duy nhất)
                    var profile = new Profile
                    {
                        Name = $"tmp-{Guid.NewGuid():N}",
                        WalletPassword = walletPassword,
                        Status = ProfileStatus.NotInitialized
                    };

                    // Chèn để lấy Id
                    var profileId = await _databaseService.CreateProfileAsync(profile);
                    profile.Id = profileId;

                    // Cập nhật tên chính thức theo quy tắc: "Profile {Id}"
                    profile.Name = $"Profile {profile.Id}";
                    await _databaseService.UpdateProfileAsync(profile);

                    profiles.Add(profile);
                    _loggingService.LogInfo(profile.Name, "Đã tạo profile thành công");
                }

                _loggingService.LogInfo("System", $"Đã tạo xong {profiles.Count} profile");
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
                    await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.InitError);
                    return false;
                }

                // Khởi tạo wallet
                var walletInitialized = await _browserService.InitializeWalletAsync(profile);
                if (!walletInitialized)
                {
                    _loggingService.LogError(profile.Name, "Không thể khởi tạo wallet");
                    await _browserService.CloseBrowserAsync(profileId); // Không thể khởi tạo wallet cũng đóng trình duyệt
                    await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.InitError);
                    return false;
                }

                // Cập nhật thông tin profile
                await _databaseService.UpdateProfileAsync(profile);
                
                // Đóng trình duyệt sau khi khởi tạo xong
                await _browserService.CloseBrowserAsync(profileId);
                
                // Cập nhật trạng thái
                await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.Initialized);
                
                _loggingService.LogInfo(profile.Name, "Khởi tạo trình duyệt thành công");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Profile{profileId}", "Lỗi khi khởi tạo trình duyệt", ex);
                await _databaseService.UpdateProfileStatusAsync(profileId, ProfileStatus.InitError);
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

        // Sinh tên profile duy nhất theo mẫu baseName + số tăng dần, tránh trùng với existingNames
        private string GenerateUniqueProfileName(HashSet<string> existingNames, string baseName = "Profile")
        {
            var suffix = 1;
            string candidate;
            do
            {
                candidate = $"{baseName}{suffix}";
                suffix++;
            } while (existingNames.Contains(candidate));

            // Đánh dấu đã dùng để tránh đụng nhau giữa các vòng lặp
            existingNames.Add(candidate);
            return candidate;
        }

        public async Task<bool> DeleteProfileAsync(int profileId)
        {
            try
            {
                // Lấy thông tin để log
                var profile = await _databaseService.GetProfileAsync(profileId);
                var profileName = profile?.Name ?? $"Profile {profileId}";

                // Đóng trình duyệt nếu đang chạy
                if (_browserService != null && _browserService.IsBrowserRunning(profileId))
                {
                    await _browserService.CloseBrowserAsync(profileId);
                }

                // Xóa dữ liệu thư mục ProfileData tương ứng
                var baseDir = AppContext.BaseDirectory;
                var profileRoot = System.IO.Path.Combine(baseDir, "ProfileData");
                var dirNew = System.IO.Path.Combine(profileRoot, $"Profile {profileId}");
                var dirLegacy = System.IO.Path.Combine(profileRoot, $"Profile_{profileId}");

                try
                {
                    if (System.IO.Directory.Exists(dirNew))
                    {
                        System.IO.Directory.Delete(dirNew, true);
                        _loggingService.LogInfo(profileName, $"Đã xóa thư mục dữ liệu: {dirNew}");
                    }
                }
                catch (Exception exDirNew)
                {
                    _loggingService.LogWarning(profileName, $"Không thể xóa thư mục {dirNew}: {exDirNew.Message}");
                }

                try
                {
                    if (System.IO.Directory.Exists(dirLegacy))
                    {
                        System.IO.Directory.Delete(dirLegacy, true);
                        _loggingService.LogInfo(profileName, $"Đã xóa thư mục dữ liệu (legacy): {dirLegacy}");
                    }
                }
                catch (Exception exDirLegacy)
                {
                    _loggingService.LogWarning(profileName, $"Không thể xóa thư mục {dirLegacy}: {exDirLegacy.Message}");
                }

                // Xóa hồ sơ khỏi database
                await _databaseService.DeleteProfileAsync(profileId);

                _loggingService.LogInfo("System", $"Đã xóa profile ID: {profileId} kèm dữ liệu ProfileData");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("System", $"Lỗi khi xóa profile {profileId}", ex);
                return false;
            }
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