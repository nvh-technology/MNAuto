using System;
using System.Text.Json.Serialization;

namespace ScavengerMineSDK.Models
{
    // Terms & Conditions
    public class TermsAndConditionsResponse
    {
        [JsonPropertyName("terms")]
        public string Terms { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        // Một số luồng trong app đang dùng terms.Message để ký
        // Bổ sung để tương thích, mặc định mapping lại từ Terms nếu API không cung cấp
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    // Registration
    public class RegistrationRequest
    {
        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;

        [JsonPropertyName("pubkey")]
        public string Pubkey { get; set; } = string.Empty;

        [JsonPropertyName("termsVersion")]
        public string TermsVersion { get; set; } = string.Empty;
    }

    // SDK nội bộ dùng kiểu RegistrationResponse (API v2)
    public class RegistrationResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("workerId")]
        public string? WorkerId { get; set; }
    }

    // Kiểu trả về mong đợi bởi MNAuto.Services.ScavengerMineService.RegisterAddressAsync
    // response.RegistrationReceipt != null => dùng response.RegistrationReceipt.Preimage
    public class RegistrationReceipt
    {
        [JsonPropertyName("preimage")]
        public string Preimage { get; set; } = string.Empty;
    }

    public class RegisterResult
    {
        [JsonPropertyName("registrationReceipt")]
        public RegistrationReceipt? RegistrationReceipt { get; set; }
    }

    // Challenge
    public class Challenge
    {
        // Một số luồng đang dùng ChallengeId
        [JsonPropertyName("challengeId")]
        public string ChallengeId { get; set; } = string.Empty;

        // Giá trị challenge để hash
        [JsonPropertyName("challenge")]
        public string ChallengeString { get; set; } = string.Empty;

        // V2 (nội bộ) vẫn dùng Difficulty là int để tính toán mục tiêu (target) nội bộ
        [JsonPropertyName("difficulty")]
        public int Difficulty { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        // Bổ sung các trường để chuẩn hoá theo tài liệu công bố (fallback /challenge)
        // Lưu ý: các trường này được map thủ công từ client fallback, không binding JSON trực tiếp
        public string DifficultyHex { get; set; } = string.Empty;
        public string NoPreMine { get; set; } = string.Empty;
        public string NoPreMineHour { get; set; } = string.Empty;
        public string LatestSubmission { get; set; } = string.Empty;
        public int Day { get; set; }
        public int ChallengeNumber { get; set; }
    }

    // Kiểu trả về mong đợi bởi ScavengerMineService.GetCurrentChallengeAsync
    // response.Code in ["active","before","after"]; response.Challenge có ChallengeId, Difficulty
    public class CurrentChallengeResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("challenge")]
        public Challenge? Challenge { get; set; }
    }

    // Challenge (theo worker) cho API nội bộ
    public class ChallengeResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("challenge")]
        public Challenge? Challenge { get; set; }
    }

    // Submit solution
    public class SolutionRequest
    {
        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        [JsonPropertyName("challengeId")]
        public string ChallengeId { get; set; } = string.Empty;

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;
    }

    public class Receipt
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        [JsonPropertyName("challengeId")]
        public string ChallengeId { get; set; } = string.Empty;

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("reward")]
        public double Reward { get; set; }
    }

    public class SolutionResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("receipt")]
        public Receipt? Receipt { get; set; }
    }

    // Kiểu mong đợi bởi ScavengerMineService khi gọi _client.SubmitSolutionAsync(nonce,...)
    public class CryptoReceipt
    {
        [JsonPropertyName("txHash")]
        public string TxHash { get; set; } = string.Empty;
    }

    public class SolutionSubmitResponse
    {
        [JsonPropertyName("cryptoReceipt")]
        public CryptoReceipt? CryptoReceipt { get; set; }
    }

    // Donate
    public class DonationRequest
    {
        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public double Amount { get; set; }
    }

    public class DonationResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class WorkToStarRateResponse
    {
        [JsonPropertyName("rate")]
        public double Rate { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }

    // Event args - mở rộng để tương thích với Form1 & Service
    public class MiningProgressEventArgs : EventArgs
    {
        // Các thuộc tính cũ
        public int HashesPerSecond { get; set; }
        public long TotalHashes { get; set; }
        public int Difficulty { get; set; }
        public string WorkerId { get; set; } = string.Empty;

        // Các thuộc tính tương thích với Form1
        public long HashCount { get; set; }                 // map từ TotalHashes
        public double HashRate { get; set; }                // map từ HashesPerSecond
        public string CurrentNonce { get; set; } = string.Empty;
    }

    public class MiningCompletedEventArgs : EventArgs
    {
        // Các thuộc tính cũ
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Receipt? Receipt { get; set; }
        public string WorkerId { get; set; } = string.Empty;

        // Các thuộc tính tương thích với Form1
        public string? Nonce { get; set; }
        public long TotalHashes { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }
}