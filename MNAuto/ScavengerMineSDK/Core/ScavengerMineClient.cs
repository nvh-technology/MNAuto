using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ScavengerMineSDK.Models;
using ScavengerMineSDK.Utilities;

namespace ScavengerMineSDK.Core
{
    public class ScavengerMineClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _fallbackBaseUrl = "https://scavenger.prod.gd.midnighttge.io";
        private bool _disposed = false;
        private string? _lastChallengeId;

        public ScavengerMineClient(string baseUrl = "https://api.scavenger-mine.com")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ScavengerMineSDK/1.0");
            
            Logger.Info($"ScavengerMineClient initialized with base URL: {_baseUrl}");
        }

        public async Task<TermsAndConditionsResponse> GetTermsAndConditionsAsync()
        {
            // Thử gọi theo chuẩn API v2 trước, nếu lỗi DNS/404 sẽ fallback sang tài liệu công bố (/TandC)
            try
            {
                Logger.Info("Fetching terms and conditions via v2 endpoint");
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/v2/terms");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var terms = JsonSerializer.Deserialize<TermsAndConditionsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (terms != null && (!string.IsNullOrWhiteSpace(terms.Version) || !string.IsNullOrWhiteSpace(terms.Terms) || !string.IsNullOrWhiteSpace(terms.Message)))
                {
                    Logger.Info($"Successfully fetched terms version: {terms?.Version}");
                    return terms;
                }

                // Nếu parse không ra dữ liệu hợp lệ, thử fallback
                Logger.Warning("Empty/invalid T&C payload from v2, falling back to public /TandC");
            }
            catch (Exception ex)
            {
                // Lỗi sẽ được log và tiếp tục fallback
                Logger.Warning($"v2 T&C fetch failed, will fallback to /TandC. Reason: {ex.Message}");
            }

            // Fallback: theo tài liệu Scavenger Mine (GET /TandC trả về version, content, message)
            try
            {
                Logger.Info("Fetching terms and conditions via public /TandC endpoint");
                var fallbackResp = await _httpClient.GetAsync($"{_fallbackBaseUrl}/TandC");
                fallbackResp.EnsureSuccessStatusCode();

                var fbContent = await fallbackResp.Content.ReadAsStringAsync();
                var doc = JsonSerializer.Deserialize<DocTermsDto>(fbContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var mapped = new TermsAndConditionsResponse
                {
                    Version = doc?.Version ?? string.Empty,
                    Terms = doc?.Content ?? string.Empty,
                    Message = doc?.Message ?? string.Empty
                };

                Logger.Info($"Successfully fetched terms (fallback) version: {mapped.Version}");
                return mapped;
            }
            catch (Exception ex2)
            {
                Logger.Error("Error fetching terms and conditions (both v2 and fallback failed)", ex2);
                throw;
            }
        }

        public async Task<RegistrationResponse> RegisterWorkerAsync(RegistrationRequest request)
        {
            try
            {
                Logger.Info($"Registering worker for address: {request.Address}");
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/v2/register", content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<RegistrationResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                Logger.Info($"Registration result: {result?.Success}, WorkerId: {result?.WorkerId}");
                return result ?? new RegistrationResponse { Success = false, Message = "Unknown error" };
            }
            catch (Exception ex)
            {
                Logger.Error("Error registering worker", ex);
                throw;
            }
        }

        public async Task<ChallengeResponse> GetChallengeAsync(string workerId)
        {
            try
            {
                Logger.Debug($"Fetching challenge for worker: {workerId}");
                
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/v2/challenge/{workerId}");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ChallengeResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                Logger.Debug($"Challenge received: {result?.Success}, Difficulty: {result?.Challenge?.Difficulty}");
                return result ?? new ChallengeResponse { Success = false };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error fetching challenge for worker: {workerId}", ex);
                throw;
            }
        }

        public async Task<SolutionResponse> SubmitSolutionAsync(SolutionRequest request)
        {
            try
            {
                Logger.Info($"Submitting solution for worker: {request.WorkerId}, Challenge: {request.ChallengeId}");
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/v2/solution", content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SolutionResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                Logger.Info($"Solution submission result: {result?.Success}, Reward: {result?.Receipt?.Reward}");
                return result ?? new SolutionResponse { Success = false, Message = "Unknown error" };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error submitting solution for worker: {request.WorkerId}", ex);
                throw;
            }
        }

        public async Task<DonationResponse> DonateAsync(DonationRequest request)
        {
            try
            {
                Logger.Info($"Donating {request.Amount} stars from worker: {request.WorkerId}");
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/v2/donate", content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<DonationResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                Logger.Info($"Donation result: {result?.Success}");
                return result ?? new DonationResponse { Success = false, Message = "Unknown error" };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error donating from worker: {request.WorkerId}", ex);
                throw;
            }
        }

        public async Task<WorkToStarRateResponse> GetWorkToStarRateAsync()
        {
            try
            {
                Logger.Debug("Fetching work-to-star rate");
                
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/v2/rate");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<WorkToStarRateResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                Logger.Debug($"Current rate: {result?.Rate} stars per work unit");
                return result ?? new WorkToStarRateResponse { Rate = 0 };
            }
            catch (Exception ex)
            {
                Logger.Error("Error fetching work-to-star rate", ex);
                throw;
            }
        }

        // Overload compatible với MNAuto.Services.ScavengerMineService.GetCurrentChallengeAsync
        public async Task<CurrentChallengeResponse> GetChallengeAsync()
        {
            // Thử API v2 trước
            try
            {
                Logger.Debug("Fetching current global challenge via v2");
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/v2/challenge");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CurrentChallengeResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new CurrentChallengeResponse { Code = "unknown" };

                if (result.Challenge != null && !string.IsNullOrWhiteSpace(result.Challenge.ChallengeId))
                {
                    _lastChallengeId = result.Challenge.ChallengeId;
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Warning($"v2 challenge fetch failed, fallback to /challenge. Reason: {ex.Message}");
            }

            // Fallback: theo tài liệu công bố (GET /challenge)
            try
            {
                Logger.Debug("Fetching current global challenge via fallback /challenge");
                var fbResp = await _httpClient.GetAsync($"{_fallbackBaseUrl}/challenge");
                fbResp.EnsureSuccessStatusCode();

                var fbJson = await fbResp.Content.ReadAsStringAsync();
                var doc = JsonSerializer.Deserialize<DocCurrentChallengeResponse>(fbJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var mapped = new CurrentChallengeResponse
                {
                    Code = doc?.Code ?? "unknown",
                    Challenge = doc?.Challenge == null ? null : new Challenge
                    {
                        ChallengeId = doc.Challenge.ChallengeId ?? string.Empty,
                        // Map đầy đủ theo tài liệu công bố (fallback /challenge)
                        DifficultyHex = doc.Challenge.Difficulty ?? string.Empty,
                        NoPreMine = doc.Challenge.NoPreMine ?? string.Empty,
                        NoPreMineHour = doc.Challenge.NoPreMineHour ?? string.Empty,
                        LatestSubmission = doc.Challenge.LatestSubmission ?? string.Empty,
                        Day = doc.Challenge.Day ?? 0,
                        ChallengeNumber = doc.Challenge.ChallengeNumber ?? 0,
                        // Ước lượng độ khó (bit) từ số lượng nibble '0' ở đầu chuỗi hex để compat các luồng cũ
                        Difficulty = EstimateDifficultyBits(doc.Challenge.Difficulty),
                        // Tạm thời không có challenge string/timestamp ở fallback
                        ChallengeString = string.Empty,
                        Timestamp = 0
                    }
                };

                if (mapped.Challenge != null)
                {
                    _lastChallengeId = mapped.Challenge.ChallengeId;
                }

                return mapped;
            }
            catch (Exception ex2)
            {
                Logger.Error("Error fetching current global challenge (both v2 and fallback failed)", ex2);
                return new CurrentChallengeResponse { Code = "error" };
            }
        }

        // Wrapper compatible với MNAuto.Services.ScavengerMineService.RegisterAddressAsync
        public async Task<RegisterResult> RegisterAsync(string walletAddress, string signature, string pubkey)
        {
            Logger.Info($"RegisterAsync called for address: {walletAddress}");

            // Thử đăng ký theo API v2 trước
            try
            {
                // Lấy Terms để điền termsVersion nếu API v2 yêu cầu
                var terms = await GetTermsAndConditionsAsync();
                var termsVersion = terms?.Version ?? string.Empty;

                var request = new RegistrationRequest
                {
                    Address = walletAddress,
                    Signature = signature,
                    Pubkey = pubkey,
                    TermsVersion = termsVersion
                };

                var regResp = await RegisterWorkerAsync(request);
                if (regResp.Success)
                {
                    return new RegisterResult
                    {
                        RegistrationReceipt = new RegistrationReceipt
                        {
                            // Nếu workerId có, ưu tiên dùng làm preimage tương thích hệ thống cũ
                            Preimage = string.IsNullOrWhiteSpace(regResp.WorkerId) ? regResp.Message : regResp.WorkerId!
                        }
                    };
                }

                Logger.Warning($"v2 register returned unsuccessful result: {regResp.Message}. Will try fallback /register path.");
            }
            catch (Exception ex)
            {
                // Lỗi trong luồng v2 (bao gồm DNS không phân giải)
                Logger.Warning($"v2 register failed: {ex.Message}. Will try fallback /register path.");
            }

            // Fallback: theo tài liệu Scavenger Mine (POST /register/{address}/{signature}/{pubkey})
            try
            {
                var encodedAddress = Uri.EscapeDataString(walletAddress);
                var encodedSig = Uri.EscapeDataString(signature);
                var encodedPk = Uri.EscapeDataString(pubkey);

                var url = $"{_fallbackBaseUrl}/register/{encodedAddress}/{encodedSig}/{encodedPk}";
                var content = new StringContent("{}", Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var fb = JsonSerializer.Deserialize<DocRegisterResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (fb?.RegistrationReceipt != null && !string.IsNullOrWhiteSpace(fb.RegistrationReceipt.Preimage))
                {
                    Logger.Info("Fallback registration succeeded via /register");
                    return new RegisterResult
                    {
                        RegistrationReceipt = new RegistrationReceipt
                        {
                            Preimage = fb.RegistrationReceipt.Preimage
                        }
                    };
                }

                Logger.Warning("Fallback /register response missing registrationReceipt");
                return new RegisterResult { RegistrationReceipt = null };
            }
            catch (Exception ex2)
            {
                Logger.Error("Error in RegisterAsync fallback (/register)", ex2);
                return new RegisterResult { RegistrationReceipt = null };
            }
        }

        // Overload compatible với MNAuto.Services.ScavengerMineService.SubmitSolutionAsync(nonce, walletAddress, pubkey, signature)
        public async Task<SolutionSubmitResponse> SubmitSolutionAsync(string nonce, string walletAddress, string pubkey, string signature)
        {
            // Thử API v2 trước
            try
            {
                Logger.Info($"SubmitSolutionAsync called (v2). Address: {walletAddress}, Nonce: {nonce}");

                var body = new
                {
                    nonce,
                    address = walletAddress,
                    pubkey,
                    signature
                };

                var json = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/v2/solution/submit", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SolutionSubmitResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new SolutionSubmitResponse();
            }
            catch (Exception ex)
            {
                Logger.Warning($"SubmitSolution v2 failed: {ex.Message}. Will try fallback if challenge_id is available.");
            }

            // Fallback: POST /solution/{address}/{challenge_id}/{nonce} theo tài liệu PDF
            try
            {
                if (string.IsNullOrWhiteSpace(_lastChallengeId))
                {
                    Logger.Warning("Fallback submit skipped because challenge_id is unknown. Call GetChallengeAsync() first.");
                    return new SolutionSubmitResponse();
                }

                Logger.Info($"SubmitSolutionAsync fallback. Address: {walletAddress}, ChallengeId: {_lastChallengeId}, Nonce: {nonce}");

                var encodedAddress = Uri.EscapeDataString(walletAddress);
                var encodedChallengeId = Uri.EscapeDataString(_lastChallengeId);
                var encodedNonce = Uri.EscapeDataString(nonce);

                var url = $"{_fallbackBaseUrl}/solution/{encodedAddress}/{encodedChallengeId}/{encodedNonce}";
                var content = new StringContent("{}", Encoding.UTF8, "application/json");

                var fbResp = await _httpClient.PostAsync(url, content);
                fbResp.EnsureSuccessStatusCode();

                var fbJson = await fbResp.Content.ReadAsStringAsync();
                var doc = JsonSerializer.Deserialize<DocSolutionResponse>(fbJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Map tối thiểu: chỉ cần CryptoReceipt != null để Service coi là thành công
                var mapped = new SolutionSubmitResponse
                {
                    CryptoReceipt = doc?.CryptoReceipt != null
                        ? new CryptoReceipt { TxHash = doc.CryptoReceipt.Signature ?? "ok" }
                        : null
                };

                return mapped;
            }
            catch (Exception ex2)
            {
                Logger.Error("Error in SubmitSolutionAsync fallback (/solution/{addr}/{challenge_id}/{nonce})", ex2);
                return new SolutionSubmitResponse();
            }
        }
        // Helpers & DTOs cho fallback endpoints theo tài liệu công bố
        private static bool IsDnsResolutionError(Exception ex)
        {
            if (ex is HttpRequestException hre && hre.InnerException is SocketException se1)
                return se1.SocketErrorCode == SocketError.HostNotFound;

            if (ex.InnerException is SocketException se2)
                return se2.SocketErrorCode == SocketError.HostNotFound;

            return ex.Message.Contains("No such host is known", StringComparison.OrdinalIgnoreCase);
        }

        private class DocTermsDto
        {
            public string Version { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        private class DocRegistrationReceipt
        {
            public string Preimage { get; set; } = string.Empty;
            public string Signature { get; set; } = string.Empty;
            public string Timestamp { get; set; } = string.Empty;
        }

        private class DocRegisterResponse
        {
            public DocRegistrationReceipt? RegistrationReceipt { get; set; }
        }

        // DTO cho fallback GET /challenge
        private class DocCurrentChallengeResponse
        {
            [JsonPropertyName("code")]
            public string Code { get; set; } = string.Empty;

            [JsonPropertyName("challenge")]
            public DocChallenge? Challenge { get; set; }
        }

        private class DocChallenge
        {
            [JsonPropertyName("challenge_id")]
            public string? ChallengeId { get; set; }

            [JsonPropertyName("difficulty")]
            public string? Difficulty { get; set; }

            [JsonPropertyName("no_pre_mine")]
            public string? NoPreMine { get; set; }

            [JsonPropertyName("no_pre_mine_hour")]
            public string? NoPreMineHour { get; set; }

            [JsonPropertyName("latest_submission")]
            public string? LatestSubmission { get; set; }

            [JsonPropertyName("day")]
            public int? Day { get; set; }

            [JsonPropertyName("challenge_number")]
            public int? ChallengeNumber { get; set; }
        }

        // DTO cho fallback POST /solution
        private class DocSolutionResponse
        {
            [JsonPropertyName("crypto_receipt")]
            public DocCryptoReceipt? CryptoReceipt { get; set; }
        }

        private class DocCryptoReceipt
        {
            [JsonPropertyName("preimage")]
            public string? Preimage { get; set; }

            [JsonPropertyName("timestamp")]
            public string? Timestamp { get; set; }

            [JsonPropertyName("signature")]
            public string? Signature { get; set; }
        }

        // Ước lượng difficulty (theo bit) từ chuỗi hex difficulty của tài liệu công bố.
        // Chiến lược: đếm số nibble '0' liên tiếp từ đầu chuỗi (bỏ qua tiền tố 0x nếu có) rồi nhân 4.
        // Mục đích là để lấp vào thuộc tính Difficulty (int) phục vụ log/compat hiện tại, không dùng cho xác thực thực sự.
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _httpClient?.Dispose();
                _disposed = true;
                Logger.Info("ScavengerMineClient disposed");
            }
        }
    }
}