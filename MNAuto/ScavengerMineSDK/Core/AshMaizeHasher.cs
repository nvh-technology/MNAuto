using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace ScavengerMineSDK.Core
{
    public static class AshMaizeHasher
    {
        public static string ComputeHash(string challenge, string nonce)
        {
            try
            {
                // Convert challenge and nonce to bytes
                var challengeBytes = Encoding.UTF8.GetBytes(challenge);
                var nonceBytes = Encoding.UTF8.GetBytes(nonce);
                
                // Combine challenge and nonce
                var combinedBytes = new byte[challengeBytes.Length + nonceBytes.Length];
                Buffer.BlockCopy(challengeBytes, 0, combinedBytes, 0, challengeBytes.Length);
                Buffer.BlockCopy(nonceBytes, 0, combinedBytes, challengeBytes.Length, nonceBytes.Length);
                
                // First pass: SHA256
                using var sha256 = SHA256.Create();
                var hash1 = sha256.ComputeHash(combinedBytes);
                
                // Second pass: SHA256
                var hash2 = sha256.ComputeHash(hash1);
                
                // Convert to BigInteger for mathematical operations
                var hashInt = new BigInteger(hash2, isUnsigned: true);
                
                // Apply AshMaize specific transformations
                // This is a simplified version - the actual algorithm may be more complex
                var transformedHash = ApplyAshMaizeTransform(hashInt);
                
                // Convert back to bytes and hex string
                var resultBytes = transformedHash.ToByteArray(isUnsigned: true);
                Array.Reverse(resultBytes); // Ensure big-endian
                
                return Convert.ToHexString(resultBytes).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Utilities.Logger.Error($"Error computing hash: {ex.Message}");
                throw;
            }
        }
        
        private static BigInteger ApplyAshMaizeTransform(BigInteger input)
        {
            // AshMaize specific transformation
            // This is a simplified implementation - the actual algorithm may differ
            
            // Apply modular operations
            var prime = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639936"); // 2^256
            var transformed = BigInteger.ModPow(input, 3, prime);
            
            // Add constant
            var constant = BigInteger.Parse("10000000000000000000000000000000000000000000000000000000000000000");
            transformed = (transformed + constant) % prime;
            
            return transformed;
        }
        
        public static bool IsValidHash(string hash, int difficulty)
        {
            try
            {
                // Convert hex hash to bytes
                var hashBytes = Convert.FromHexString(hash);
                
                // Check if hash meets difficulty requirement
                var target = CalculateTarget(difficulty);
                var hashValue = new BigInteger(hashBytes, isUnsigned: true);
                
                return hashValue < target;
            }
            catch (Exception ex)
            {
                Utilities.Logger.Error($"Error validating hash: {ex.Message}");
                return false;
            }
        }
        
        private static BigInteger CalculateTarget(int difficulty)
        {
            // Calculate target based on difficulty
            // Higher difficulty = lower target = harder to find valid hash
            var maxTarget = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935"); // 2^256 - 1
            
            if (difficulty <= 0)
                return maxTarget;
                
            return maxTarget / BigInteger.Pow(2, difficulty);
        }
    }
}