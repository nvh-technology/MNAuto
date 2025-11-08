using System;

namespace MNAuto.Models
{
    /// <summary>
    /// Tóm tắt trạng thái Challenge toàn cục để hiển thị trong UI (list profile).
    /// Dữ liệu được lấy từ ScavengerMineService (wrapper của SDK).
    /// </summary>
    public class ChallengeSummary
    {
        // Trạng thái tổng quát của challenge: "active" / "before" / "after" / "unknown" / "error"
        public string Code { get; set; } = "unknown";

        // ID challenge hiện tại (nếu có)
        public string CurrentChallengeId { get; set; } = string.Empty;

        // Ngày (theo tài liệu công bố fallback /challenge)
        public int Day { get; set; }

        // Số thứ tự challenge trong ngày (theo tài liệu công bố)
        public int ChallengeNumber { get; set; }

        // Độ khó dạng hex (fallback /challenge cung cấp)
        public string DifficultyHex { get; set; } = string.Empty;

        // Độ khó (tương thích SDK: bits được ước lượng từ hex hoặc dùng int difficulty v2)
        public int DifficultyBits { get; set; }

        // Phân loại độ khó thân thiện (Easy/Medium/Hard)
        public string DifficultyCategory { get; set; } = string.Empty;

        // Thời điểm submission gần nhất (raw string từ API)
        public string LatestSubmissionRaw { get; set; } = string.Empty;

        // “Tuổi” của submission gần nhất (ví dụ: “12m ago”)
        public string LatestSubmissionAge { get; set; } = string.Empty;

        // Thời gian đến challenge kế tiếp (nếu có thể suy ra)
        public TimeSpan? NextChallengeIn { get; set; }

        // Chuỗi hiển thị countdown “Next challenge in” (ví dụ: “00:19:42”); nếu không có sẽ là “N/A”
        public string NextChallengeInText { get; set; } = "N/A";

        // Nhãn tình trạng miner (ví dụ: “ACTIVE”, “INACTIVE”)
        public string MinerStatusLabel { get; set; } = string.Empty;
    }
}