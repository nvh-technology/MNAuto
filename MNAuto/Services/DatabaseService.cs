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
    }
}