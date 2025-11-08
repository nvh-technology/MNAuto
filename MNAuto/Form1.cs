using MNAuto.Models;
using MNAuto.Services;
using ScavengerMineSDK.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MNAuto
{
    public partial class Form1 : Form
    {
        private DatabaseService? _databaseService;
        private LoggingService? _loggingService;
        private ProfileManagerService? _profileManagerService;
        private ScavengerMineService? _scavengerMineService;
        private List<Profile> _profiles = new List<Profile>();
        private bool _isInitialized = false;

        // Trạng thái challenge toàn cục & kết quả giải gần nhất
        private string _currentChallengeCode = "unknown";
        private string _nextChallengeInText = string.Empty;
        private Dictionary<int, bool> _lastSolveSuccess = new Dictionary<int, bool>();
        // Tóm tắt challenge để hiển thị nhiều chỉ số theo ảnh tham chiếu
        private ChallengeSummary _challengeSummary = new ChallengeSummary();

        public Form1()
        {
            InitializeComponent();
            InitializeServices();
        }

        private async void InitializeServices()
        {
            try
            {
                _databaseService = new DatabaseService();
                _loggingService = new LoggingService();
                _profileManagerService = new ProfileManagerService(_databaseService, _loggingService);
                _scavengerMineService = new ScavengerMineService(_loggingService, _databaseService);
                
                await _profileManagerService.InitializeAsync();
                
                // Đăng ký sự kiện logging
                _loggingService.NewLogEntry += OnNewLogEntry;
                
                // Đăng ký sự kiện mining
                _scavengerMineService.MiningProgress += OnMiningProgress;
                _scavengerMineService.MiningCompleted += OnMiningCompleted;
                
                // Tải danh sách profiles
                await LoadProfilesAsync();
                
                _isInitialized = true;
                _loggingService.LogInfo("System", "Khởi tạo ứng dụng thành công");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi khởi tạo ứng dụng: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadProfilesAsync()
        {
            try
            {
                if (_profileManagerService == null) return;

                _profiles = await _profileManagerService.GetAllProfilesAsync();

                // Lấy trạng thái challenge toàn cục để hiển thị thêm (tóm tắt theo ảnh tham chiếu)
                if (_scavengerMineService != null)
                {
                    _challengeSummary = await _scavengerMineService.BuildChallengeSummaryAsync();
                    _currentChallengeCode = _challengeSummary?.Code ?? "unknown";
                    _nextChallengeInText = _challengeSummary?.NextChallengeInText ?? "N/A";
                }

                RefreshProfileList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách profiles: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshProfileList()
        {
            if (dgvProfiles.InvokeRequired)
            {
                dgvProfiles.Invoke(new Action(RefreshProfileList));
                return;
            }

            // Lưu lại trạng thái checkbox trước khi refresh
            var selectedStates = new Dictionary<int, bool>();
            foreach (DataGridViewRow row in dgvProfiles.Rows)
            {
                if (row.Cells["Selected"].Value != null)
                {
                    selectedStates[(int)row.Cells["Id"].Value] = (bool)row.Cells["Selected"].Value;
                }
            }

            dgvProfiles.DataSource = null;
            dgvProfiles.DataSource = _profiles.Select(p => new
            {
                p.Id,
                p.Name,
                p.NightTokens,
                p.WalletAddress,
                // Trạng thái tổng quát của profile
                Status = GetStatusText(p.Status),
                IsRegistered = p.IsRegistered ? "Đã đăng ký" : "Chưa đăng ký",
                SolutionsFound = p.SolutionsFound,
                TotalHashes = p.TotalHashes,

                // Theo ảnh tham chiếu - khối Miner status
                MinerStatus = _challengeSummary.MinerStatusLabel,                                  // ACTIVE/INACTIVE
                CurrentChallenge = _challengeSummary.CurrentChallengeId,                           // 218
                FindingStatus = p.IsMining ? "Finding a solution..." : "Idle",                     // Status
                TimeSpent = _scavengerMineService != null ? _scavengerMineService.GetMiningElapsedText(p.Id) : "00:00:00",
                DifficultyCategory = _challengeSummary.DifficultyCategory,                         // Easy/Medium/Hard

                // Theo ảnh tham chiếu - khối Day / Next challenge / Counters
                Day = _challengeSummary.Day,
                ChallengeNumber = _challengeSummary.ChallengeNumber,
                NextChallengeIn = _challengeSummary.NextChallengeInText,
                LatestSubmissionAge = _challengeSummary.LatestSubmissionAge,

                // Difficulty chi tiết
                DifficultyBits = _challengeSummary.DifficultyBits,
                DifficultyHexShort = string.IsNullOrWhiteSpace(_challengeSummary.DifficultyHex)
                    ? ""
                    : (_challengeSummary.DifficultyHex.Length > 10
                        ? _challengeSummary.DifficultyHex.Substring(0, 10) + "…"
                        : _challengeSummary.DifficultyHex),

                // Challenge Availability theo code
                ChallengeAvailable = _currentChallengeCode == "active" ? "Yes" : "No",

                // Counters tham chiếu (xấp xỉ theo dữ liệu cục bộ)
                AllEvents = p.SolutionsFound + (p.IsMining ? 1 : 0),
                SolvedCount = p.SolutionsFound,
                UnsolvedCount = (_lastSolveSuccess.TryGetValue(p.Id, out var okLast) && !okLast) ? 1 : 0,

                // Giữ cột cũ để tương thích
                IsMining = p.IsMining ? "Đang đào" : "Không",
                p.CreatedAt
            }).ToList();

            // Cấu hình cột
            dgvProfiles.Columns["Id"].HeaderText = "ID";
            dgvProfiles.Columns["Name"].HeaderText = "Tên Profile";
            dgvProfiles.Columns["NightTokens"].HeaderText = "NIGHT Tokens";
            dgvProfiles.Columns["WalletAddress"].HeaderText = "Địa chỉ Wallet";

            // Khối trạng thái profile cơ bản
            dgvProfiles.Columns["Status"].HeaderText = "Trạng thái";
            dgvProfiles.Columns["IsRegistered"].HeaderText = "Đăng ký SM";
            dgvProfiles.Columns["SolutionsFound"].HeaderText = "Solutions";
            dgvProfiles.Columns["TotalHashes"].HeaderText = "Total Hashes";

            // Khối Miner status theo ảnh tham chiếu
            dgvProfiles.Columns["MinerStatus"].HeaderText = "Miner status";
            dgvProfiles.Columns["CurrentChallenge"].HeaderText = "Current challenge";
            dgvProfiles.Columns["FindingStatus"].HeaderText = "Status";
            dgvProfiles.Columns["TimeSpent"].HeaderText = "Time spent on this challenge";
            dgvProfiles.Columns["DifficultyCategory"].HeaderText = "Difficulty";

            // Khối Day / Next challenge / Counters
            dgvProfiles.Columns["Day"].HeaderText = "Day";
            dgvProfiles.Columns["ChallengeNumber"].HeaderText = "Challenge #";
            dgvProfiles.Columns["NextChallengeIn"].HeaderText = "Next challenge in";
            dgvProfiles.Columns["LatestSubmissionAge"].HeaderText = "Latest submission";

            // Difficulty chi tiết
            dgvProfiles.Columns["DifficultyBits"].HeaderText = "Difficulty (bits)";
            dgvProfiles.Columns["DifficultyHexShort"].HeaderText = "Difficulty (hex)";

            // Availability & Counters
            dgvProfiles.Columns["ChallengeAvailable"].HeaderText = "Challenge available";
            dgvProfiles.Columns["AllEvents"].HeaderText = "All";
            dgvProfiles.Columns["SolvedCount"].HeaderText = "Solved";
            dgvProfiles.Columns["UnsolvedCount"].HeaderText = "Unsolved";

            // Cột tương thích cũ
            dgvProfiles.Columns["IsMining"].HeaderText = "Mining";
            dgvProfiles.Columns["CreatedAt"].HeaderText = "Ngày tạo";

            // Thêm cột checkbox
            if (!dgvProfiles.Columns.Contains("Selected"))
            {
                var checkBoxColumn = new DataGridViewCheckBoxColumn
                {
                    Name = "Selected",
                    HeaderText = "Chọn",
                    Width = 50,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                };
                dgvProfiles.Columns.Insert(0, checkBoxColumn);
            }

            // Khôi phục trạng thái checkbox
            foreach (DataGridViewRow row in dgvProfiles.Rows)
            {
                var profileId = (int)row.Cells["Id"].Value;
                if (selectedStates.ContainsKey(profileId))
                {
                    row.Cells["Selected"].Value = selectedStates[profileId];
                }
            }
        }

        private string GetStatusText(ProfileStatus status)
        {
            return status switch
            {
                ProfileStatus.Initializing => "Đang khởi tạo",
                ProfileStatus.NotStarted => "Chưa khởi động",
                ProfileStatus.Running => "Đang chạy",
                ProfileStatus.Stopped => "Đã dừng",
                ProfileStatus.Mining => "Đang đào",
                _ => "Không xác định"
            };
        }

        private string GetNextChallengeText(string code, Challenge? challenge)
        {
            // Map đơn giản theo code từ API
            return code switch
            {
                "active" => "Đang diễn ra",
                "before" => "Chưa bắt đầu",
                "after" => "Đã kết thúc",
                _ => "Không xác định"
            };
        }

        private void OnNewLogEntry(LogEntry logEntry)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action<LogEntry>(OnNewLogEntry), logEntry);
                return;
            }

            var logLine = $"{logEntry.Timestamp:HH:mm:ss} [{logEntry.ProfileName}]: {logEntry.Message}{Environment.NewLine}";
            txtLog.AppendText(logLine);
            
            // Giữ số dòng log trong giới hạn
            var lines = txtLog.Lines;
            if (lines.Length > 1000)
            {
                var newLines = new string[lines.Length - 100];
                Array.Copy(lines, 100, newLines, 0, newLines.Length);
                txtLog.Lines = newLines;
            }
            
            // Tự động scroll xuống cuối
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private void OnMiningProgress(object? sender, MiningProgressEventArgs e)
        {
            var logLine = $"[Mining] Hashes: {e.HashCount:N0}, Rate: {e.HashRate:F2} H/s, Nonce: {e.CurrentNonce}{Environment.NewLine}";
            
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action<string>(AppendLog), logLine);
            }
            else
            {
                AppendLog(logLine);
            }
        }

        private void OnMiningCompleted(object? sender, MiningCompletedEventArgs e)
        {
            var logLine = e.Success
                ? $"[Mining] Hoàn thành! Nonce: {e.Nonce}, Tổng hashes: {e.TotalHashes:N0}, Thời gian: {e.Duration.TotalMinutes:F1} phút{Environment.NewLine}"
                : $"[Mining] Thất bại: {e.ErrorMessage}{Environment.NewLine}";

            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action<string>(AppendLog), logLine);
            }
            else
            {
                AppendLog(logLine);
            }

            // Cập nhật trạng thái Solved/Unsolved gần nhất để hiển thị trong danh sách
            try
            {
                var prof = _profiles.FirstOrDefault(p => p.WorkerId == e.WorkerId || p.WalletAddress == e.WorkerId);
                if (prof != null)
                {
                    _lastSolveSuccess[prof.Id] = e.Success;
                }
            }
            catch { }

            // Sau khi MiningCompleted, làm mới danh sách để đồng bộ trạng thái và counters từ DB
            _ = LoadProfilesAsync();
        }

        private void AppendLog(string logLine)
        {
            txtLog.AppendText(logLine);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private async void btnCreateProfiles_Click(object sender, EventArgs e)
        {
            if (!_isInitialized || _profileManagerService == null) return;
            
            if (!int.TryParse(txtProfileCount.Text, out int count) || count <= 0)
            {
                MessageBox.Show("Vui lòng nhập số lượng profile hợp lệ", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                btnCreateProfiles.Enabled = false;
                _loggingService?.LogInfo("System", $"Bắt đầu tạo {count} profile mới");
                
                var newProfiles = await _profileManagerService.CreateProfilesAsync(count);
                _profiles.AddRange(newProfiles);
                
                RefreshProfileList();
                _loggingService?.LogInfo("System", $"Đã tạo thành công {newProfiles.Count} profile");
                
                MessageBox.Show($"Đã tạo thành công {newProfiles.Count} profile", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("System", $"Lỗi khi tạo profiles: {ex.Message}");
                MessageBox.Show($"Lỗi khi tạo profiles: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCreateProfiles.Enabled = true;
            }
        }

        private async void btnInitializeSelected_Click(object sender, EventArgs e)
        {
            if (!_isInitialized || _profileManagerService == null) return;
            
            var selectedIds = GetSelectedProfileIds();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một profile", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                btnInitializeSelected.Enabled = false;
                _loggingService?.LogInfo("System", $"Bắt đầu khởi tạo {selectedIds.Count} profile");
                
                var results = await _profileManagerService.InitializeMultipleProfilesAsync(selectedIds);
                var successCount = results.Count(r => r);
                
                await LoadProfilesAsync();
                _loggingService?.LogInfo("System", $"Đã khởi tạo thành công {successCount}/{selectedIds.Count} profile");
                
                MessageBox.Show($"Đã khởi tạo thành công {successCount}/{selectedIds.Count} profile", "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("System", $"Lỗi khi khởi tạo profiles: {ex.Message}");
                MessageBox.Show($"Lỗi khi khởi tạo profiles: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnInitializeSelected.Enabled = true;
            }
        }

        private async void btnStartSelected_Click(object sender, EventArgs e)
        {
            if (!_isInitialized || _profileManagerService == null) return;
            
            var selectedIds = GetSelectedProfileIds();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một profile", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                btnStartSelected.Enabled = false;
                _loggingService?.LogInfo("System", $"Bắt đầu khởi động {selectedIds.Count} profile");
                
                var results = await _profileManagerService.StartMultipleProfilesAsync(selectedIds, headless: false);
                var successCount = results.Count(r => r);
                
                await LoadProfilesAsync();
                _loggingService?.LogInfo("System", $"Đã khởi động thành công {successCount}/{selectedIds.Count} profile");
                
                MessageBox.Show($"Đã khởi động thành công {successCount}/{selectedIds.Count} profile", "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("System", $"Lỗi khi khởi động profiles: {ex.Message}");
                MessageBox.Show($"Lỗi khi khởi động profiles: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnStartSelected.Enabled = true;
            }
        }

        private async void btnStartSession_Click(object sender, EventArgs e)
        {
            if (!_isInitialized || _profileManagerService == null || _scavengerMineService == null) return;
            
            var selectedIds = GetSelectedProfileIds();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một profile", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                btnStartSession.Enabled = false;
                _loggingService?.LogInfo("System", $"Bắt đầu Scavenger Session cho {selectedIds.Count} profile");
                
                var selectedProfiles = _profiles.Where(p => selectedIds.Contains(p.Id)).ToList();
                const int maxParallel = 3;
                var semaphore = new SemaphoreSlim(maxParallel);

                var tasks = selectedProfiles.Select(async profile =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        _loggingService?.LogInfo(profile.Name, "Bắt đầu Scavenger Session");
                        
                        // Kiểm tra xem trình duyệt đã chạy chưa
                        if (!_profileManagerService.IsProfileRunning(profile.Id))
                        {
                            _loggingService?.LogInfo(profile.Name, "Khởi động trình duyệt");
                            await _profileManagerService.StartProfileAsync(profile.Id);
                            await Task.Delay(2000); // Chờ trình duyệt khởi động
                        }
                        
                        // Lấy browser service
                        var browserService = _profileManagerService.GetBrowserService();
                        if (browserService == null)
                        {
                            _loggingService?.LogError(profile.Name, "Không lấy được BrowserService");
                            return false;
                        }
                        
                        // Đăng ký địa chỉ nếu chưa đăng ký
                        if (!profile.IsRegistered)
                        {
                            _loggingService?.LogInfo(profile.Name, "Đăng ký địa chỉ với ScavengerMine");
                            var registered = await browserService.RegisterAddressAsync(profile, _scavengerMineService);
                            
                            if (!registered)
                            {
                                _loggingService?.LogError(profile.Name, "Đăng ký địa chỉ thất bại");
                                return false;
                            }
                            
                            // Cập nhật database
                            if (_databaseService != null)
                            {
                                await _databaseService.UpdateProfileAsync(profile);
                            }
                        }
                        
                        // Bắt đầu mining
                        _loggingService?.LogInfo(profile.Name, "Bắt đầu mining");
                        var miningStarted = await _scavengerMineService.StartMiningAsync(profile, 2); // 2 threads per profile
                        
                        if (miningStarted)
                        {
                            _loggingService?.LogInfo(profile.Name, "Đã bắt đầu mining thành công");

                            // Cập nhật trạng thái sang Mining để UI hiển thị đúng
                            if (_databaseService != null)
                            {
                                await _databaseService.UpdateProfileStatusAsync(profile.Id, ProfileStatus.Mining);
                            }

                            // Sau khi ký xong và đã bắt đầu MiningWorker, không cần trình duyệt nữa => đóng để giải phóng tài nguyên
                            try
                            {
                                if (_profileManagerService.IsProfileRunning(profile.Id))
                                {
                                    _loggingService?.LogInfo(profile.Name, "Đóng trình duyệt sau khi bắt đầu mining");
                                    await browserService.CloseBrowserAsync(profile.Id);
                                }
                            }
                            catch (Exception closeEx)
                            {
                                _loggingService?.LogWarning(profile.Name, $"Không thể đóng trình duyệt sau khi bắt đầu mining: {closeEx.Message}");
                            }

                            return true;
                        }
                        else
                        {
                            _loggingService?.LogError(profile.Name, "Bắt đầu mining thất bại");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService?.LogError(profile.Name, $"Lỗi trong Scavenger Session: {ex.Message}", ex);
                        return false;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                var results = await Task.WhenAll(tasks);
                var successCount = results.Count(r => r);

                await LoadProfilesAsync();
                _loggingService?.LogInfo("System", $"Đã khởi tạo Scavenger Session thành công cho {successCount}/{selectedIds.Count} profile");
                
                MessageBox.Show($"Đã khởi tạo Scavenger Session thành công cho {successCount}/{selectedIds.Count} profile",
                    "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("System", $"Lỗi khi khởi tạo Scavenger Session: {ex.Message}", ex);
                MessageBox.Show($"Lỗi khi khởi tạo Scavenger Session: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnStartSession.Enabled = true;
            }
        }

        // Đã loại bỏ nút "Mở trình duyệt"

        private async void btnCloseSelected_Click(object sender, EventArgs e)
        {
            if (!_isInitialized || _profileManagerService == null) return;
            
            var selectedIds = GetSelectedProfileIds();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một profile", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                btnCloseSelected.Enabled = false;
                _loggingService?.LogInfo("System", $"Bắt đầu đóng {selectedIds.Count} profile");
                
                var results = await _profileManagerService.CloseMultipleProfilesAsync(selectedIds);
                var successCount = results.Count(r => r);
                
                await LoadProfilesAsync();
                _loggingService?.LogInfo("System", $"Đã đóng thành công {successCount}/{selectedIds.Count} profile");
                
                MessageBox.Show($"Đã đóng thành công {successCount}/{selectedIds.Count} profile", "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("System", $"Lỗi khi đóng profiles: {ex.Message}");
                MessageBox.Show($"Lỗi khi đóng profiles: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCloseSelected.Enabled = true;
            }
        }

        private async void btnDeleteSelected_Click(object sender, EventArgs e)
        {
            if (!_isInitialized || _profileManagerService == null) return;
            
            var selectedIds = GetSelectedProfileIds();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một profile", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show($"Bạn có chắc chắn muốn xóa {selectedIds.Count} profile đã chọn?",
                "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            
            if (result != DialogResult.Yes) return;

            try
            {
                btnDeleteSelected.Enabled = false;
                _loggingService?.LogInfo("System", $"Bắt đầu xóa {selectedIds.Count} profile");
                
                var successCount = 0;
                foreach (var profileId in selectedIds)
                {
                    try
                    {
                        // Đóng trình duyệt nếu đang chạy
                        if (_profileManagerService.IsProfileRunning(profileId))
                        {
                            await _profileManagerService.CloseProfileAsync(profileId);
                        }
                        
                        // Xóa profile khỏi database
                        if (_databaseService != null)
                        {
                            await _databaseService.DeleteProfileAsync(profileId);
                        }
                        
                        // Xóa khỏi danh sách hiện tại
                        _profiles.RemoveAll(p => p.Id == profileId);
                        
                        successCount++;
                        _loggingService?.LogInfo("System", $"Đã xóa profile ID: {profileId}");
                    }
                    catch (Exception ex)
                    {
                        _loggingService?.LogError("System", $"Lỗi khi xóa profile {profileId}: {ex.Message}");
                    }
                }
                
                RefreshProfileList();
                _loggingService?.LogInfo("System", $"Đã xóa thành công {successCount}/{selectedIds.Count} profile");
                
                MessageBox.Show($"Đã xóa thành công {successCount}/{selectedIds.Count} profile", "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("System", $"Lỗi khi xóa profiles: {ex.Message}");
                MessageBox.Show($"Lỗi khi xóa profiles: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDeleteSelected.Enabled = true;
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            _ = LoadProfilesAsync();
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
            _loggingService?.LogInfo("System", "Đã xóa log hiển thị");
        }

        private void chkSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            if (dgvProfiles.Rows.Count == 0) return;
            
            foreach (DataGridViewRow row in dgvProfiles.Rows)
            {
                row.Cells["Selected"].Value = chkSelectAll.Checked;
            }
        }

        private List<int> GetSelectedProfileIds()
        {
            var selectedIds = new List<int>();
            
            foreach (DataGridViewRow row in dgvProfiles.Rows)
            {
                if (row.Cells["Selected"].Value != null && (bool)row.Cells["Selected"].Value)
                {
                    selectedIds.Add((int)row.Cells["Id"].Value);
                }
            }
            
            return selectedIds;
        }

        private async void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // Dừng tất cả mining
                if (_scavengerMineService != null)
                {
                    _scavengerMineService.StopAllMining();
                    _scavengerMineService.Dispose();
                }
                
                if (_profileManagerService != null)
                {
                    await _profileManagerService.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("System", $"Lỗi khi đóng ứng dụng: {ex.Message}");
            }
        }
    }
}
