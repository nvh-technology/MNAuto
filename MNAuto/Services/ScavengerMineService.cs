using MNAuto.Models;
using ScavengerMineSDK.Core;
using ScavengerMineSDK.Models;
using ScavengerMineSDK.Workers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNAuto.Services
{
    /// <summary>
    /// Service để quản lý các hoạt động ScavengerMine
    /// </summary>
    public class ScavengerMineService : IDisposable
    {
        private readonly ScavengerMineClient _client;
        private readonly LoggingService _loggingService;
        private readonly DatabaseService _databaseService;
        private readonly Dictionary<int, MiningWorker> _miningWorkers;
        private readonly Dictionary<int, CancellationTokenSource> _cancellationTokens;
        // Theo dõi thời gian bắt đầu mining cho từng profile để hiển thị "Time spent"
        private readonly Dictionary<int, DateTime> _miningStartTimes;
        private bool _disposed = false;

        public event EventHandler<MiningProgressEventArgs>? MiningProgress;
        public event EventHandler<MiningCompletedEventArgs>? MiningCompleted;

        public ScavengerMineService(LoggingService loggingService, DatabaseService databaseService)
        {
            _client = new ScavengerMineClient();
            _loggingService = loggingService;
            _databaseService = databaseService;
            _miningWorkers = new Dictionary<int, MiningWorker>();
            _cancellationTokens = new Dictionary<int, CancellationTokenSource>();
            _miningStartTimes = new Dictionary<int, DateTime>();
        }

        /// <summary>
        /// Lấy Terms and Conditions từ ScavengerMine API
        /// </summary>
        public async Task<TermsAndConditionsResponse> GetTermsAndConditionsAsync()
        {
            try
            {
                _loggingService.LogInfo("ScavengerMine", "Đang lấy Terms and Conditions...");
                var terms = await _client.GetTermsAndConditionsAsync();
                _loggingService.LogInfo("ScavengerMine", $"Đã lấy T&C version {terms.Version}");
                return terms;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ScavengerMine", $"Lỗi khi lấy T&C: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Đăng ký địa chỉ với ScavengerMine
        /// </summary>
        public async Task<bool> RegisterAddressAsync(Profile profile, string signature, string pubkey)
        {
            try
            {
                if (string.IsNullOrEmpty(profile.WalletAddress))
                {
                    _loggingService.LogError(profile.Name, "Wallet address trống");
                    return false;
                }

                _loggingService.LogInfo(profile.Name, "Bắt đầu đăng ký địa chỉ với ScavengerMine");
                
                var response = await _client.RegisterAsync(profile.WalletAddress, signature, pubkey);
                
                if (response.RegistrationReceipt != null)
                {
                    profile.PublicKey = pubkey;
                    profile.Signature = signature;
                    profile.IsRegistered = true;
                    profile.RegistrationReceipt = response.RegistrationReceipt.Preimage;
                    profile.WorkerId = response.RegistrationReceipt.Preimage;
                    profile.UpdatedAt = DateTime.UtcNow;
                    
                    _loggingService.LogInfo(profile.Name, "Đăng ký địa chỉ thành công");
                    return true;
                }
                
                _loggingService.LogError(profile.Name, "Đăng ký thất bại: không nhận được receipt");
                return false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(profile.Name, $"Lỗi khi đăng ký địa chỉ: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Lấy challenge hiện tại
        /// </summary>
        public async Task<Challenge?> GetCurrentChallengeAsync()
        {
            try
            {
                _loggingService.LogInfo("ScavengerMine", "Đang lấy challenge hiện tại...");
                var response = await _client.GetChallengeAsync();
                
                if (response.Code == "active" && response.Challenge != null)
                {
                    _loggingService.LogInfo("ScavengerMine",
                        $"Đã lấy challenge {response.Challenge.ChallengeId}, difficulty: {response.Challenge.Difficulty}");
                    return response.Challenge;
                }
                else if (response.Code == "before")
                {
                    _loggingService.LogInfo("ScavengerMine", "Mining chưa bắt đầu");
                    return null;
                }
                else if (response.Code == "after")
                {
                    _loggingService.LogInfo("ScavengerMine", "Mining đã kết thúc");
                    return null;
                }
                
                _loggingService.LogWarning("ScavengerMine", $"Trạng thái không xác định: {response.Code}");
                return null;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ScavengerMine", $"Lỗi khi lấy challenge: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Lấy đầy đủ trạng thái challenge (code + challenge) để UI hiển thị
        /// </summary>
        public async Task<CurrentChallengeResponse> GetCurrentChallengeStatusAsync()
        {
            try
            {
                _loggingService.LogInfo("ScavengerMine", "Đang lấy trạng thái challenge (code + challenge)...");
                var response = await _client.GetChallengeAsync();
                return response ?? new CurrentChallengeResponse { Code = "unknown", Challenge = null };
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ScavengerMine", $"Lỗi khi lấy trạng thái challenge: {ex.Message}", ex);
                return new CurrentChallengeResponse { Code = "error", Challenge = null };
            }
        }

        /// <summary>
        /// Bắt đầu mining cho một profile
        /// </summary>
        public async Task<bool> StartMiningAsync(Profile profile, int numThreads = 4)
        {
            try
            {
                if (!profile.IsRegistered)
                {
                    _loggingService.LogError(profile.Name, "Profile chưa đăng ký với ScavengerMine");
                    return false;
                }

                if (_miningWorkers.ContainsKey(profile.Id))
                {
                    _loggingService.LogWarning(profile.Name, "Profile đang mining");
                    return false;
                }

                _loggingService.LogInfo(profile.Name, $"Bắt đầu mining với {numThreads} threads");

                // Lấy challenge hiện tại
                var challenge = await GetCurrentChallengeAsync();
                if (challenge == null)
                {
                    _loggingService.LogError(profile.Name, "Không thể lấy challenge");
                    return false;
                }

                // Tạo mining worker
                var workerId = string.IsNullOrWhiteSpace(profile.WorkerId) ? profile.WalletAddress : profile.WorkerId;
                var worker = new MiningWorker(_client, workerId, profile.WalletAddress);
                worker.ProgressChanged += (sender, e) => 
                {
                    profile.TotalHashes = e.HashCount;
                    MiningProgress?.Invoke(this, e);
                };
                worker.MiningCompleted += (sender, e) =>
                {
                    // Cập nhật trạng thái trong bộ nhớ
                    profile.IsMining = false;
                    profile.Status = ProfileStatus.Running;
                    // Lưu xuống DB (không chặn luồng)
                    _ = _databaseService.UpdateProfileAsync(profile);
                    // Phát sự kiện cho UI
                    MiningCompleted?.Invoke(this, e);
                };

                _miningWorkers[profile.Id] = worker;

                // Tạo cancellation token
                var cts = new CancellationTokenSource();
                _cancellationTokens[profile.Id] = cts;

                // Bắt đầu mining
                profile.IsMining = true;
                profile.Status = ProfileStatus.Mining;
                // Ghi nhận thời điểm bắt đầu để hiển thị "Time spent"
                _miningStartTimes[profile.Id] = DateTime.UtcNow;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var nonce = await worker.FindNonceAsync(challenge, numThreads, cts.Token);
                        
                        if (!string.IsNullOrEmpty(nonce))
                        {
                            _loggingService.LogInfo(profile.Name, $"Tìm thấy nonce: {nonce}");
                            
                            // Gửi solution
                            var solutionResponse = await _client.SubmitSolutionAsync(
                                nonce,
                                profile.WalletAddress,
                                profile.PublicKey,
                                profile.Signature);

                            if (solutionResponse.CryptoReceipt != null)
                            {
                                _loggingService.LogInfo(profile.Name, "Gửi solution thành công");
                                // Cập nhật hồ sơ khi server chấp nhận
                                profile.SolutionsFound++;
                                profile.IsMining = false;
                                profile.Status = ProfileStatus.Running;
                                await _databaseService.UpdateProfileAsync(profile);
                            }
                            else
                            {
                                _loggingService.LogError(profile.Name, "Gửi solution thất bại");
                                // Dù thất bại vẫn đưa trạng thái về Running và lưu DB
                                profile.IsMining = false;
                                profile.Status = ProfileStatus.Running;
                                await _databaseService.UpdateProfileStatusAsync(profile.Id, ProfileStatus.Running);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _loggingService.LogInfo(profile.Name, "Mining đã bị hủy");
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError(profile.Name, $"Lỗi trong quá trình mining: {ex.Message}", ex);
                    }
                    finally
                    {
                        // Đảm bảo trạng thái được đưa về Running và lưu DB
                        profile.IsMining = false;
                        profile.Status = ProfileStatus.Running;
                        try
                        {
                            await _databaseService.UpdateProfileStatusAsync(profile.Id, ProfileStatus.Running);
                        }
                        catch (Exception dbEx)
                        {
                            _loggingService.LogWarning(profile.Name, $"Không thể cập nhật trạng thái vào DB: {dbEx.Message}");
                        }
                        _miningWorkers.Remove(profile.Id);
                        _cancellationTokens.Remove(profile.Id);
                        _miningStartTimes.Remove(profile.Id);
                    }
                });

                _loggingService.LogInfo(profile.Name, "Đã bắt đầu mining thành công");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(profile.Name, $"Lỗi khi bắt đầu mining: {ex.Message}", ex);
                profile.IsMining = false;
                profile.Status = ProfileStatus.Running;
                return false;
            }
        }

        /// <summary>
        /// Dừng mining cho một profile
        /// </summary>
        public bool StopMining(int profileId)
        {
            try
            {
                if (!_cancellationTokens.ContainsKey(profileId))
                {
                    return false;
                }

                _loggingService.LogInfo($"Profile{profileId}", "Dừng mining");
                _cancellationTokens[profileId].Cancel();
                _cancellationTokens.Remove(profileId);
                
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Profile{profileId}", $"Lỗi khi dừng mining: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Bắt đầu mining cho nhiều profiles
        /// </summary>
        public async Task<Dictionary<int, bool>> StartMultiMiningAsync(List<Profile> profiles, int numThreads = 4)
        {
            var results = new Dictionary<int, bool>();
            
            foreach (var profile in profiles)
            {
                try
                {
                    var result = await StartMiningAsync(profile, numThreads);
                    results[profile.Id] = result;
                    
                    // Chờ một chút giữa các profile để tránh quá tải
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    _loggingService.LogError(profile.Name, $"Lỗi khi bắt đầu mining: {ex.Message}", ex);
                    results[profile.Id] = false;
                }
            }
            
            return results;
        }

        /// <summary>
        /// Dừng mining cho tất cả profiles
        /// </summary>
        public void StopAllMining()
        {
            var profileIds = _cancellationTokens.Keys.ToList();
            foreach (var profileId in profileIds)
            {
                StopMining(profileId);
            }
        }

        /// <summary>
        /// Lấy "Time spent on this challenge" dạng HH:mm:ss cho một profile
        /// </summary>
        public string GetMiningElapsedText(int profileId)
        {
            if (!_miningStartTimes.ContainsKey(profileId))
                return "00:00:00";
            var elapsed = DateTime.UtcNow - _miningStartTimes[profileId];
            return elapsed.ToString(@"hh\:mm\:ss");
        }

        /// <summary>
        /// Xây dựng tóm tắt trạng thái Challenge toàn cục để hiển thị trong UI
        /// </summary>
        public async Task<ChallengeSummary> BuildChallengeSummaryAsync()
        {
            var summary = new ChallengeSummary();
            try
            {
                var resp = await _client.GetChallengeAsync();
                summary.Code = resp?.Code ?? "unknown";

                if (resp?.Challenge != null)
                {
                    var ch = resp.Challenge;
                    summary.CurrentChallengeId = ch.ChallengeId ?? string.Empty;
                    summary.Day = ch.Day;
                    summary.ChallengeNumber = ch.ChallengeNumber;
                    summary.DifficultyHex = ch.DifficultyHex ?? string.Empty;

                    // Tính Difficulty bits: ưu tiên giá trị int từ v2 nếu có, fallback từ hex
                    summary.DifficultyBits = ch.Difficulty > 0 ? ch.Difficulty : EstimateDifficultyBits(ch.DifficultyHex);

                    // Phân loại độ khó thân thiện
                    summary.DifficultyCategory = summary.DifficultyBits <= 16 ? "Easy"
                        : summary.DifficultyBits <= 32 ? "Medium" : "Hard";

                    // Tuổi của submission gần nhất
                    summary.LatestSubmissionRaw = ch.LatestSubmission ?? string.Empty;
                    if (DateTime.TryParse(summary.LatestSubmissionRaw, out var latest))
                    {
                        var age = DateTime.UtcNow - latest.ToUniversalTime();
                        if (age.TotalDays >= 1)
                            summary.LatestSubmissionAge = $"{(int)age.TotalDays}d ago";
                        else if (age.TotalHours >= 1)
                            summary.LatestSubmissionAge = $"{(int)age.TotalHours}h ago";
                        else
                            summary.LatestSubmissionAge = $"{(int)Math.Max(0, age.TotalMinutes)}m ago";
                    }
                    else
                    {
                        summary.LatestSubmissionAge = string.IsNullOrWhiteSpace(summary.LatestSubmissionRaw) ? "N/A" : summary.LatestSubmissionRaw;
                    }
                }

                // Nhãn miner status & Next challenge (chưa có lịch -> N/A)
                summary.MinerStatusLabel = summary.Code == "active" ? "ACTIVE" : "INACTIVE";
                summary.NextChallengeIn = null;
                summary.NextChallengeInText = "N/A";

                return summary;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ScavengerMine", $"Lỗi khi xây dựng ChallengeSummary: {ex.Message}", ex);
                summary.Code = "error";
                return summary;
            }
        }

        // Ước lượng difficulty (theo bit) từ chuỗi hex difficulty
        private static int EstimateDifficultyBits(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return 0;

            var s = hex.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);

            int zeroNibbles = 0;
            foreach (var ch in s)
            {
                if (ch == '0') zeroNibbles++;
                else break;
            }
            return zeroNibbles * 4;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopAllMining();
                _client?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}