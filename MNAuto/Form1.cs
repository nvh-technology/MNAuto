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
using System.IO;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MNAuto
{
    public partial class Form1 : Form
    {
        private DatabaseService? _databaseService;
        private LoggingService? _loggingService;
        private ProfileManagerService? _profileManagerService;
        private ScavengerMineService? _scavengerMineService;
        private List<Profile> _profiles = new List<Profile>();
        private HashSet<int> _selectedProfileIds = new HashSet<int>();
        private bool _isInitialized = false;

        // Paging/Virtual mode
        private int _pageSize = 100;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private bool _gridInitialized = false;

        // UI paging controls
        private Button? btnPrevPage;
        private Button? btnNextPage;
        private Label? lblPageInfo;
        private NumericUpDown? nudPageSize;

        // Trạng thái challenge toàn cục & kết quả giải gần nhất
        private string _currentChallengeCode = "unknown";
        private string _nextChallengeInText = string.Empty;
        private Dictionary<int, bool> _lastSolveSuccess = new Dictionary<int, bool>();
        // Tóm tắt challenge để hiển thị nhiều chỉ số theo ảnh tham chiếu
        private ChallengeSummary _challengeSummary = new ChallengeSummary();

        public Form1()
        {
            InitializeComponent();

            // DataGridView performance tuning
            this.dgvProfiles.VirtualMode = true;
            this.dgvProfiles.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; // Tắt Auto-Sizing
            this.dgvProfiles.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;      // Tắt Auto row sizing
            this.dgvProfiles.EditMode = DataGridViewEditMode.EditOnEnter;               // Cho phép edit trực tiếp ô Checkbox
            this.dgvProfiles.ReadOnly = false;                                          // Các cột dữ liệu sẽ đặt ReadOnly riêng
            this.dgvProfiles.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvProfiles.MultiSelect = true;
            this.dgvProfiles.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dgvProfiles.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvProfiles.CellValueNeeded += new DataGridViewCellValueEventHandler(this.dgvProfiles_CellValueNeeded);
            this.dgvProfiles.CellValuePushed += new DataGridViewCellValueEventHandler(this.dgvProfiles_CellValuePushed);
            

            // Bật DoubleBuffered cho DataGridView để giảm flicker
            try
            {
                typeof(DataGridView).InvokeMember("DoubleBuffered",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                    null, this.dgvProfiles, new object[] { true });
            }
            catch { /* fallback if reflection not permitted */ }

            // Tạo controls phân trang (Paging)
            this.lblPageInfo = new Label
            {
                AutoSize = true,
                Text = "Trang 1/1",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(this.ClientSize.Width - 380, 45)
            };

            this.btnPrevPage = new Button
            {
                Text = "<",
                Size = new Size(30, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(this.ClientSize.Width - 220, 43)
            };
            this.btnPrevPage.Click += new EventHandler(this.btnPrevPage_Click);

            this.btnNextPage = new Button
            {
                Text = ">",
                Size = new Size(30, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(this.ClientSize.Width - 185, 43)
            };
            this.btnNextPage.Click += new EventHandler(this.btnNextPage_Click);

            this.nudPageSize = new NumericUpDown
            {
                Minimum = 10,
                Maximum = 100000,
                Value = 100,
                Increment = 10,
                Size = new Size(70, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(this.ClientSize.Width - 100, 44)
            };
            this.nudPageSize.ValueChanged += new EventHandler(this.nudPageSize_ValueChanged);

            var lblPageSize = new Label
            {
                Text = "Page size:",
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(this.ClientSize.Width - 170, 47)
            };

            this.Controls.Add(this.lblPageInfo);
            this.Controls.Add(this.btnPrevPage);
            this.Controls.Add(this.btnNextPage);
            this.Controls.Add(lblPageSize);
            this.Controls.Add(this.nudPageSize);

            // VirtualMode đã xử lý Selected qua CellValueNeeded/CellValuePushed, không cần CellValueChanged/DataBindingComplete

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
                
                // Đồng bộ tên profile theo Id để khớp "Profile {Id}" với thư mục ProfileData
                await _databaseService.NormalizeProfileNamesAsync();
                
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

            // Cập nhật page size từ UI (nếu có)
            if (nudPageSize != null && nudPageSize.Value > 0)
                _pageSize = (int)nudPageSize.Value;

            var total = _profiles?.Count ?? 0;
            _totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)_pageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            int startIndex = (_currentPage - 1) * _pageSize;
            int rows = Math.Max(0, Math.Min(_pageSize, total - startIndex));

            // Tắt redraw khi cập nhật lưới
            using (new RedrawScope(this.dgvProfiles))
            {
                this.dgvProfiles.SuspendLayout();

                // Không sử dụng DataSource khi VirtualMode
                if (this.dgvProfiles.DataSource != null)
                    this.dgvProfiles.DataSource = null;

                // Khởi tạo cột duy nhất 1 lần: chỉ hiển thị các cột yêu cầu
                if (!_gridInitialized)
                {
                    this.dgvProfiles.Columns.Clear();

                    // Cột Id ẩn để mapping
                    var colId = new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", Visible = false };

                    // Cột checkbox chọn hồ sơ
                    var colSelected = new DataGridViewCheckBoxColumn
                    {
                        Name = "Selected",
                        HeaderText = "",
                        Width = 40,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        ThreeState = false
                    };

                    // 5 cột yêu cầu
                    var colName = new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Tên Profile", Width = 180, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
                    var colWallet = new DataGridViewTextBoxColumn { Name = "WalletAddress", HeaderText = "Địa chỉ Wallet", Width = 260, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
                    var colRecovery = new DataGridViewTextBoxColumn { Name = "RecoveryPhrase", HeaderText = "Recovery Phrase", Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
                    var colPwd = new DataGridViewTextBoxColumn { Name = "WalletPassword", HeaderText = "Mật khẩu", Width = 150, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
                    var colStatus = new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Trạng thái ví", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };

                    this.dgvProfiles.Columns.AddRange(new DataGridViewColumn[] { colId, colSelected, colName, colWallet, colRecovery, colPwd, colStatus });

                    foreach (DataGridViewColumn col in this.dgvProfiles.Columns)
                    {
                        col.SortMode = DataGridViewColumnSortMode.NotSortable;
                        col.ReadOnly = true;
                    }
                    // Cho phép chỉnh sửa cột Selected (checkbox)
                    colSelected.ReadOnly = false;

                    _gridInitialized = true;
                }

                // Cập nhật số dòng hiển thị theo trang
                this.dgvProfiles.RowCount = rows;

                // Xóa chọn cũ để đồng bộ UI
                this.dgvProfiles.ClearSelection();

                this.dgvProfiles.ResumeLayout();
            }

            // Cập nhật UI phân trang
            if (lblPageInfo != null)
            {
                lblPageInfo.Text = $"Trang {_currentPage}/{_totalPages} - Tổng {total}";
            }
            if (btnPrevPage != null) btnPrevPage.Enabled = _currentPage > 1;
            if (btnNextPage != null) btnNextPage.Enabled = _currentPage < _totalPages;
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

        // Trạng thái ví đúng theo yêu cầu: "Chưa khởi tạo", "Đang khởi tạo", "Chưa Ký", "Thành công"
        private string GetWalletStatusText(Profile p)
        {
            // Ưu tiên hiển thị "Đang khởi tạo" khi đang trong quá trình khởi tạo ví
            if (p.Status == ProfileStatus.Initializing) return "Đang khởi tạo";

            // Chưa có địa chỉ ví => Chưa khởi tạo
            if (string.IsNullOrWhiteSpace(p.WalletAddress)) return "Chưa khởi tạo";

            // Có ví nhưng chưa ký/đăng ký => Chưa Ký
            if (string.IsNullOrWhiteSpace(p.Signature) || !p.IsRegistered) return "Chưa Ký";

            // Đã ký và đăng ký thành công
            return "Thành công";
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
            // Lấy mật khẩu ví từ ô nhập và kiểm tra
            var pwd = txtWalletPassword?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pwd))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu ví", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // Ràng buộc: tối thiểu 8 ký tự và chứa cả số lẫn chữ
            bool hasLetter = pwd.Any(char.IsLetter);
            bool hasDigit = pwd.Any(char.IsDigit);
            if (pwd.Length < 8 || !hasLetter || !hasDigit)
            {
                MessageBox.Show("Mật khẩu phải tối thiểu 8 ký tự và bao gồm cả số và chữ", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                btnCreateProfiles.Enabled = false;
                _loggingService?.LogInfo("System", $"Bắt đầu tạo {count} profile mới");
                
                var newProfiles = await _profileManagerService.CreateProfilesAsync(count, pwd);
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
                
                // Bước 1: Khởi tạo ví cho các profile đã chọn
                var results = await _profileManagerService.InitializeMultipleProfilesAsync(selectedIds);
                var initSuccessCount = results.Count(r => r);

                // Xác định danh sách ID đã khởi tạo thành công (giữ thứ tự theo selectedIds)
                var initializedIds = new List<int>();
                for (int i = 0; i < selectedIds.Count && i < results.Count; i++)
                {
                    if (results[i]) initializedIds.Add(selectedIds[i]);
                }

                // Bước 2: Ngay sau khi khởi tạo ví thành công, tự động thực hiện ký/đăng ký 
                int signedCount = 0;
                if (initializedIds.Count > 0 && _scavengerMineService != null)
                {
                    _loggingService?.LogInfo("System", $"Bắt đầu ký/đăng ký tự động cho {initializedIds.Count} profile sau khi khởi tạo ví");

                    // Lấy danh sách profile mới nhất từ DB để đảm bảo thông tin ví đã được cập nhật
                    var profilesForSigning = new List<Profile>();
                    if (_databaseService != null)
                    {
                        foreach (var pid in initializedIds)
                        {
                            try
                            {
                                var p = await _databaseService.GetProfileAsync(pid);
                                if (p != null) profilesForSigning.Add(p);
                            }
                            catch (Exception exGet)
                            {
                                _loggingService?.LogWarning($"Profile {pid}", $"Không thể tải profile từ DB để ký: {exGet.Message}");
                            }
                        }
                    }

                    // Giới hạn song song 3 để an toàn
                    const int maxParallel = 3;
                    var semaphore = new SemaphoreSlim(maxParallel);
                    var tasks = profilesForSigning.Select(async profile =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            _loggingService?.LogInfo(profile.Name, "Tự động ký & đăng ký với ScavengerMine sau khi khởi tạo ví");

                            // Đảm bảo trình duyệt đang chạy (cần context cho quy trình ký)
                            if (!_profileManagerService.IsProfileRunning(profile.Id))
                            {
                                _loggingService?.LogInfo(profile.Name, "Khởi động trình duyệt để ký message");
                                var started = await _profileManagerService.StartProfileAsync(profile.Id, headless: false);
                                if (!started)
                                {
                                    _loggingService?.LogError(profile.Name, "Không thể khởi động trình duyệt để ký");
                                    return false;
                                }
                                await Task.Delay(1500);
                            }

                            // Lấy BrowserService
                            var browserService = _profileManagerService.GetBrowserService();
                            if (browserService == null)
                            {
                                _loggingService?.LogError(profile.Name, "Không lấy được BrowserService");
                                return false;
                            }

                            // Nếu chưa đăng ký thì thực hiện đăng ký (quy trình này bao gồm ký message)
                            if (!profile.IsRegistered)
                            {
                                _loggingService?.LogInfo(profile.Name, "Đăng ký địa chỉ với ScavengerMine (ký tự động)");
                                var registered = await browserService.RegisterAddressAsync(profile, _scavengerMineService);
                                if (!registered)
                                {
                                    _loggingService?.LogError(profile.Name, "Đăng ký địa chỉ thất bại");
                                    return false;
                                }

                                // Cập nhật DB với PublicKey/Signature/IsRegistered/WorkerId...
                                if (_databaseService != null)
                                {
                                    await _databaseService.UpdateProfileAsync(profile);
                                }
                            }

                            // Đóng trình duyệt để giải phóng tài nguyên (không tự động bắt đầu mining ở đây)
                            try
                            {
                                if (_profileManagerService.IsProfileRunning(profile.Id))
                                {
                                    _loggingService?.LogInfo(profile.Name, "Đóng trình duyệt sau khi ký/đăng ký");
                                    await browserService.CloseBrowserAsync(profile.Id);
                                }
                            }
                            catch (Exception closeEx)
                            {
                                _loggingService?.LogWarning(profile.Name, $"Không thể đóng trình duyệt sau khi ký: {closeEx.Message}");
                            }

                            return true;
                        }
                        catch (Exception exSign)
                        {
                            _loggingService?.LogError(profile.Name, $"Lỗi trong bước ký/đăng ký sau khởi tạo: {exSign.Message}", exSign);
                            return false;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    var signResults = await Task.WhenAll(tasks);
                    signedCount = signResults.Count(r => r);
                }

                // Làm mới danh sách sau khi hoàn tất cả hai bước
                await LoadProfilesAsync();

                // Ghi log và hiển thị kết quả tổng hợp
                _loggingService?.LogInfo("System", $"Đã khởi tạo thành công {initSuccessCount}/{selectedIds.Count} profile");
                if (initializedIds.Count > 0)
                {
                    _loggingService?.LogInfo("System", $"Đã ký/đăng ký thành công {signedCount}/{initializedIds.Count} profile sau khi khởi tạo");
                }

                var msg = new StringBuilder();
                msg.AppendLine($"Khởi tạo ví: {initSuccessCount}/{selectedIds.Count} profile thành công.");
                if (initializedIds.Count > 0)
                    msg.AppendLine($"Ký/Đăng ký: {signedCount}/{initializedIds.Count} profile thành công.");
                MessageBox.Show(msg.ToString(), "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("System", $"Lỗi khi khởi tạo/ ký profiles: {ex.Message}");
                MessageBox.Show($"Lỗi khi khởi tạo/ ký profiles: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                        // Xóa dữ liệu thư mục ProfileData tương ứng (hỗ trợ cả tên mới và legacy)
                        var baseDir = AppContext.BaseDirectory;
                        var profileRoot = System.IO.Path.Combine(baseDir, "ProfileData");
                        var dirNew = System.IO.Path.Combine(profileRoot, $"Profile {profileId}");
                        var dirLegacy = System.IO.Path.Combine(profileRoot, $"Profile_{profileId}");

                        try
                        {
                            if (System.IO.Directory.Exists(dirNew))
                            {
                                System.IO.Directory.Delete(dirNew, true);
                                _loggingService?.LogInfo("System", $"Đã xóa thư mục dữ liệu: {dirNew}");
                            }
                        }
                        catch (Exception exDirNew)
                        {
                            _loggingService?.LogWarning("System", $"Không thể xóa thư mục {dirNew}: {exDirNew.Message}");
                        }

                        try
                        {
                            if (System.IO.Directory.Exists(dirLegacy))
                            {
                                System.IO.Directory.Delete(dirLegacy, true);
                                _loggingService?.LogInfo("System", $"Đã xóa thư mục dữ liệu (legacy): {dirLegacy}");
                            }
                        }
                        catch (Exception exDirLegacy)
                        {
                            _loggingService?.LogWarning("System", $"Không thể xóa thư mục {dirLegacy}: {exDirLegacy.Message}");
                        }

                        // Xóa khỏi danh sách hiện tại
                        _profiles.RemoveAll(p => p.Id == profileId);

                        successCount++;
                        _loggingService?.LogInfo("System", $"Đã xóa profile ID: {profileId} kèm dữ liệu ProfileData");
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
            bool selectAll = chkSelectAll.Checked;

            // Áp dụng cho toàn bộ danh sách profile
            _selectedProfileIds.Clear();
            if (selectAll && _profiles != null)
            {
                foreach (var p in _profiles)
                    _selectedProfileIds.Add(p.Id);
            }

            // Làm mới hiển thị checkbox
            dgvProfiles.Invalidate();
        }

        private List<int> GetSelectedProfileIds()
        {
            var result = _selectedProfileIds?.ToList() ?? new List<int>();
            result.Sort();
            return result;
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

        // Commit ngay khi user click vào checkbox để nhận giá trị mới
        private void dgvProfiles_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (dgvProfiles.IsCurrentCellDirty)
            {
                dgvProfiles.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvProfiles.EndEdit();
            }
        }

        // Sau khi binding xong, ép trạng thái Selected từ _selectedProfileIds và khóa các cột dữ liệu
        private void dgvProfiles_DataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
        {
            try
            {
                foreach (DataGridViewRow row in dgvProfiles.Rows)
                {
                    if (row.Cells["Id"].Value == null) continue;
                    var id = (int)row.Cells["Id"].Value;
                    row.Cells["Selected"].Value = _selectedProfileIds.Contains(id);
                }
                foreach (DataGridViewColumn col in dgvProfiles.Columns)
                {
                    col.ReadOnly = col.Name != "Selected";
                }
            }
            catch { }
        }

        // Khi một ô Selected đổi giá trị, cập nhật _selectedProfileIds ngay
        private void dgvProfiles_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgvProfiles.Columns[e.ColumnIndex].Name != "Selected") return;

                var row = dgvProfiles.Rows[e.RowIndex];
                if (row.Cells["Id"].Value == null) return;

                var id = (int)row.Cells["Id"].Value;
                var isChecked = row.Cells["Selected"].Value is bool b && b;
                if (isChecked)
                    _selectedProfileIds.Add(id);
                else
                    _selectedProfileIds.Remove(id);
            }
            catch { }
        }

        // Bảo đảm toggle checkbox khi click vào ô "Selected"
        private void dgvProfiles_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgvProfiles.Columns[e.ColumnIndex].Name == "Selected")
                {
                    var cell = dgvProfiles.Rows[e.RowIndex].Cells["Selected"];
                    var current = cell.Value as bool? ?? false;
                    cell.Value = !current;
                }
            }
            catch { /* ignore UI toggle errors */ }
        }

        // Export toàn bộ profiles ra CSV mở bằng Excel: Tên profile, WalletAddress, WalletPassword, RecoveryPhrase
        private void btnExportAll_Click(object? sender, EventArgs e)
        {
            try
            {
                var profiles = _profiles ?? new List<Profile>();
                if (profiles.Count == 0)
                {
                    MessageBox.Show("Không có profile để export", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "CSV (Comma delimited) (*.csv)|*.csv";
                    sfd.FileName = $"profiles_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    if (sfd.ShowDialog() != DialogResult.OK) return;

                    var sb = new StringBuilder();
                    // Header
                    sb.AppendLine(ToCsv(new[] { "Tên profile", "WalletAddress", "WalletPassword", "RecoveryPhrase" }));
                    // Rows
                    foreach (var p in profiles)
                    {
                        sb.AppendLine(ToCsv(new[] { p.Name, p.WalletAddress, p.WalletPassword, p.RecoveryPhrase }));
                    }

                    // Viết file với BOM để Excel hiển thị Unicode tiếng Việt đúng
                    var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
                    File.WriteAllText(sfd.FileName, sb.ToString(), utf8WithBom);

                    MessageBox.Show($"Đã export {profiles.Count} profile ra file:\n{sfd.FileName}", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("System", $"Lỗi khi export profiles: {ex.Message}", ex);
                MessageBox.Show($"Lỗi khi export profiles: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string ToCsv(IEnumerable<string> fields)
        {
            return string.Join(",", fields.Select(f =>
            {
                var val = f ?? string.Empty;
                val = val.Replace("\"", "\"\"");
                return $"\"{val}\"";
            }));
        }

        // VirtualMode: cung cấp dữ liệu cho ô
        private void dgvProfiles_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
        {
            if (_profiles == null)
            {
                e.Value = null;
                return;
            }

            int startIndex = (_currentPage - 1) * _pageSize;
            int index = startIndex + e.RowIndex;

            if (index < 0 || index >= _profiles.Count)
            {
                e.Value = null;
                return;
            }

            var p = _profiles[index];
            var colName = dgvProfiles.Columns[e.ColumnIndex].Name;

            switch (colName)
            {
                case "Selected": e.Value = _selectedProfileIds.Contains(p.Id); break;
                case "Id": e.Value = p.Id; break;
                case "Name": e.Value = p.Name; break;
                case "WalletAddress": e.Value = p.WalletAddress; break;
                case "RecoveryPhrase": e.Value = p.RecoveryPhrase; break;
                case "WalletPassword": e.Value = p.WalletPassword; break;
                case "Status": e.Value = GetWalletStatusText(p); break;
                default: e.Value = null; break;
            }
        }

        // Cập nhật dữ liệu khi người dùng chỉnh checkbox (VirtualMode)
        private void dgvProfiles_CellValuePushed(object? sender, DataGridViewCellValueEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var colName = dgvProfiles.Columns[e.ColumnIndex].Name;
                if (colName != "Selected") return;

                int startIndex = (_currentPage - 1) * _pageSize;
                int index = startIndex + e.RowIndex;
                if (index < 0 || index >= _profiles.Count) return;

                var id = _profiles[index].Id;
                bool isChecked = e.Value is bool b && b;

                if (isChecked)
                    _selectedProfileIds.Add(id);
                else
                    _selectedProfileIds.Remove(id);
            }
            catch { /* ignore */ }
        }

        // Sự kiện phân trang
        private void btnPrevPage_Click(object? sender, EventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                RefreshProfileList();
            }
        }

        private void btnNextPage_Click(object? sender, EventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                RefreshProfileList();
            }
        }

        private void nudPageSize_ValueChanged(object? sender, EventArgs e)
        {
            _pageSize = (int)(nudPageSize?.Value ?? 100);
            if (_pageSize <= 0) _pageSize = 100;
            _currentPage = 1;
            RefreshProfileList();
        }

        // Tắt/ bật redraw để tăng tốc khi cập nhật dữ liệu
        private const int WM_SETREDRAW = 0x000B;
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private sealed class RedrawScope : IDisposable
        {
            private readonly Control _ctrl;
            public RedrawScope(Control ctrl)
            {
                _ctrl = ctrl;
                if (_ctrl.IsHandleCreated)
                {
                    SendMessage(_ctrl.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                }
            }

            public void Dispose()
            {
                if (_ctrl.IsHandleCreated)
                {
                    SendMessage(_ctrl.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                    _ctrl.Invalidate(true);
                    _ctrl.Update();
                }
            }
        }
    }
}
