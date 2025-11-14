using System;

namespace MNAuto.Models
{
    public class Profile
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RecoveryPhrase { get; set; } = string.Empty;
        public double NightTokens { get; set; }
        public string WalletAddress { get; set; } = string.Empty;
        public string WalletPassword { get; set; } = string.Empty;
        public ProfileStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Thông tin ScavengerMine
        public string PublicKey { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public bool IsRegistered { get; set; } = false;
        public string RegistrationReceipt { get; set; } = string.Empty;
        // WorkerId lưu lại mã worker trả về từ đăng ký (nếu không có sẽ tạm dùng WalletAddress)
        public string WorkerId { get; set; } = string.Empty;
        public bool IsMining { get; set; } = false;
        public long TotalHashes { get; set; } = 0;
        public int SolutionsFound { get; set; } = 0;
        
        // Thông tin donate
        public bool HasDonated { get; set; } = false;
        public string DestinationAddress { get; set; } = string.Empty;
        public string DonationId { get; set; } = string.Empty;
        public DateTime? DonationTimestamp { get; set; }
    }

    public enum ProfileStatus
    {
        Initializing = 0,    // Đang khởi tạo (chưa có wallet Address)
        NotStarted = 1,      // Chưa khởi động
        Running = 2,         // Đang chạy
        Stopped = 3,         // Đã dừng
        Mining = 4,          // Đang đào ScavengerMine

        // Bổ sung trạng thái cho vòng đời khởi tạo ví
        NotInitialized = 5,  // Chưa khởi tạo
        Initialized = 6,     // Đã khởi tạo (có ví/địa chỉ, sẵn sàng chạy)
        InitError = 7        // Lỗi khởi tạo
    }
}