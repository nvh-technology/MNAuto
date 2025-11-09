using Microsoft.Data.Sqlite;
using Dapper;
using MNAuto.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace MNAuto.Services
{
    public class DatabaseService
    {
        private readonly string _databasePath;
        private readonly string _connectionString;

        public DatabaseService()
        {
            // Lưu Database cùng cấp với file thực thi: ./Database/profiles.db
            var baseDir = AppContext.BaseDirectory;
            var dbDir = Path.Combine(baseDir, "Database");
            Directory.CreateDirectory(dbDir);
            _databasePath = Path.Combine(dbDir, "profiles.db");
            _connectionString = $"Data Source={_databasePath}";
            
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Profiles (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    RecoveryPhrase TEXT,
                    NightTokens REAL DEFAULT 0,
                    WalletAddress TEXT,
                    WalletPassword TEXT NOT NULL,
                    Status INTEGER NOT NULL DEFAULT 0,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PublicKey TEXT DEFAULT '',
                    Signature TEXT DEFAULT '',
                    IsRegistered INTEGER NOT NULL DEFAULT 0,
                    RegistrationReceipt TEXT DEFAULT '',
                    WorkerId TEXT DEFAULT '',
                    IsMining INTEGER NOT NULL DEFAULT 0,
                    TotalHashes INTEGER NOT NULL DEFAULT 0,
                    SolutionsFound INTEGER NOT NULL DEFAULT 0
                )");
            
            // Kiểm tra và thêm các cột mới nếu chưa có (cho database cũ)
            try
            {
                // Thêm các cột mới một cách an toàn
                connection.Execute("ALTER TABLE Profiles ADD COLUMN PublicKey TEXT DEFAULT ''");
            }
            catch { /* Column already exists */ }
            
            try
            {
                connection.Execute("ALTER TABLE Profiles ADD COLUMN Signature TEXT DEFAULT ''");
            }
            catch { /* Column already exists */ }
            
            try
            {
                connection.Execute("ALTER TABLE Profiles ADD COLUMN IsRegistered INTEGER NOT NULL DEFAULT 0");
            }
            catch { /* Column already exists */ }
            
            try
            {
                connection.Execute("ALTER TABLE Profiles ADD COLUMN RegistrationReceipt TEXT DEFAULT ''");
            }
            catch { /* Column already exists */ }
            
            try
            {
                connection.Execute("ALTER TABLE Profiles ADD COLUMN IsMining INTEGER NOT NULL DEFAULT 0");
            }
            catch { /* Column already exists */ }
            
            try
            {
                connection.Execute("ALTER TABLE Profiles ADD COLUMN TotalHashes INTEGER NOT NULL DEFAULT 0");
            }
            catch { /* Column already exists */ }
            
            try
            {
                connection.Execute("ALTER TABLE Profiles ADD COLUMN SolutionsFound INTEGER NOT NULL DEFAULT 0");
            }
            catch { /* Column already exists */ }
            
            try
            {
                connection.Execute("ALTER TABLE Profiles ADD COLUMN WorkerId TEXT DEFAULT ''");
            }
            catch { /* Column already exists */ }
        }

        public async Task<int> CreateProfileAsync(Profile profile)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var sql = @"
                INSERT INTO Profiles (Name, RecoveryPhrase, NightTokens, WalletAddress, WalletPassword, Status, CreatedAt, UpdatedAt, PublicKey, Signature, IsRegistered, RegistrationReceipt, WorkerId, IsMining, TotalHashes, SolutionsFound)
                VALUES (@Name, @RecoveryPhrase, @NightTokens, @WalletAddress, @WalletPassword, @Status, @CreatedAt, @UpdatedAt, @PublicKey, @Signature, @IsRegistered, @RegistrationReceipt, @WorkerId, @IsMining, @TotalHashes, @SolutionsFound);
                SELECT last_insert_rowid();";
            
            profile.CreatedAt = DateTime.Now;
            profile.UpdatedAt = DateTime.Now;
            
            return await connection.QuerySingleAsync<int>(sql, profile);
        }

        public async Task<Profile?> GetProfileAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var sql = "SELECT * FROM Profiles WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<Profile>(sql, new { Id = id });
        }

        public async Task<List<Profile>> GetAllProfilesAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var sql = "SELECT * FROM Profiles ORDER BY Id";
            var profiles = await connection.QueryAsync<Profile>(sql);
            return profiles.ToList();
        }

        public async Task UpdateProfileAsync(Profile profile)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var sql = @"
                UPDATE Profiles
                SET Name = @Name, RecoveryPhrase = @RecoveryPhrase, NightTokens = @NightTokens,
                    WalletAddress = @WalletAddress, WalletPassword = @WalletPassword,
                    Status = @Status, UpdatedAt = @UpdatedAt, PublicKey = @PublicKey,
                    Signature = @Signature, IsRegistered = @IsRegistered, RegistrationReceipt = @RegistrationReceipt, WorkerId = @WorkerId,
                    IsMining = @IsMining, TotalHashes = @TotalHashes, SolutionsFound = @SolutionsFound
                WHERE Id = @Id";
            
            profile.UpdatedAt = DateTime.Now;
            await connection.ExecuteAsync(sql, profile);
        }

        public async Task DeleteProfileAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var sql = "DELETE FROM Profiles WHERE Id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task UpdateProfileStatusAsync(int id, ProfileStatus status)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var sql = @"
                UPDATE Profiles 
                SET Status = @Status, UpdatedAt = @UpdatedAt
                WHERE Id = @Id";
            
            await connection.ExecuteAsync(sql, new { 
                Id = id, 
                Status = (int)status, 
                UpdatedAt = DateTime.Now 
            });
        }

        public async Task UpdateWalletAddressAsync(int id, string walletAddress)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var sql = @"
                UPDATE Profiles
                SET WalletAddress = @WalletAddress, Status = @Status, UpdatedAt = @UpdatedAt
                WHERE Id = @Id";
            
            await connection.ExecuteAsync(sql, new {
                Id = id,
                WalletAddress = walletAddress,
                Status = (int)ProfileStatus.Initialized,
                UpdatedAt = DateTime.Now
            });
        }

        /// <summary>
        /// Đồng bộ Name của tất cả profiles theo quy tắc: "Profile {Id}" để đảm bảo duy nhất và phù hợp thư mục ProfileData
        /// Xử lý an toàn UNIQUE(Name) bằng hai pha: đổi tên tạm nếu có xung đột, sau đó đặt tên cuối.
        /// </summary>
        public async Task NormalizeProfileNamesAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var tx = connection.BeginTransaction();

            var profiles = (await connection.QueryAsync<Profile>("SELECT Id, Name FROM Profiles ORDER BY Id", transaction: tx)).ToList();

            // Bản đồ hỗ trợ phát hiện xung đột
            var nameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var idToName = new Dictionary<int, string>();
            foreach (var p in profiles)
            {
                var currentName = p.Name ?? string.Empty;
                if (!nameToId.ContainsKey(currentName))
                    nameToId[currentName] = p.Id;
                idToName[p.Id] = currentName;
            }

            // Pha 1: Giải phóng các tên đích đang bị chiếm bởi profile khác
            foreach (var p in profiles)
            {
                var target = $"Profile {p.Id}";
                if (nameToId.TryGetValue(target, out var ownerId) && ownerId != p.Id)
                {
                    var temp = $"__TMP__{ownerId}__{Guid.NewGuid():N}";
                    await connection.ExecuteAsync(
                        "UPDATE Profiles SET Name = @Name, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                        new { Name = temp, UpdatedAt = DateTime.Now, Id = ownerId },
                        transaction: tx
                    );

                    // Cập nhật bản đồ
                    nameToId.Remove(target);
                    nameToId[temp] = ownerId;
                    idToName[ownerId] = temp;
                }
            }

            // Pha 2: Đặt tên cuối "Profile {Id}" cho tất cả rows chưa đúng
            foreach (var p in profiles)
            {
                var target = $"Profile {p.Id}";
                if (!string.Equals(idToName[p.Id], target, StringComparison.Ordinal))
                {
                    await connection.ExecuteAsync(
                        "UPDATE Profiles SET Name = @Name, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                        new { Name = target, UpdatedAt = DateTime.Now, Id = p.Id },
                        transaction: tx
                    );

                    // Cập nhật bản đồ
                    if (nameToId.ContainsKey(idToName[p.Id]) && nameToId[idToName[p.Id]] == p.Id)
                    {
                        nameToId.Remove(idToName[p.Id]);
                    }
                    nameToId[target] = p.Id;
                    idToName[p.Id] = target;
                }
            }

            tx.Commit();
        }
    }
}