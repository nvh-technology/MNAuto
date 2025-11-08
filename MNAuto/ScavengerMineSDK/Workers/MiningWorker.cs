using System;
using System.Threading;
using System.Threading.Tasks;
using ScavengerMineSDK.Core;
using ScavengerMineSDK.Models;
using ScavengerMineSDK.Utilities;

namespace ScavengerMineSDK.Workers
{
    public class MiningWorker : IDisposable
    {
        private readonly ScavengerMineClient _client;
        private readonly string _workerId;
        private readonly string _walletAddress;
        private CancellationTokenSource _cancellationTokenSource;
        private Task? _miningTask;
        private bool _isRunning;
        private bool _disposed = false;
        private long _totalHashes;
        private DateTime _lastProgressUpdate;

        public event EventHandler<MiningProgressEventArgs>? ProgressUpdated;
        public event EventHandler<MiningProgressEventArgs>? ProgressChanged;
        public event EventHandler<MiningCompletedEventArgs>? MiningCompleted;

        public string WorkerId => _workerId;
        public string WalletAddress => _walletAddress;
        public bool IsRunning => _isRunning;
        public long TotalHashes => _totalHashes;

        public MiningWorker(ScavengerMineClient client, string workerId, string walletAddress)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _workerId = workerId ?? throw new ArgumentNullException(nameof(workerId));
            _walletAddress = walletAddress ?? throw new ArgumentNullException(nameof(walletAddress));
            _cancellationTokenSource = new CancellationTokenSource();
            _lastProgressUpdate = DateTime.UtcNow;

            Logger.Info($"MiningWorker created: {_workerId} for wallet: {walletAddress}");
        }

        public async Task StartAsync()
        {
            if (_isRunning)
            {
                Logger.Warning($"MiningWorker {_workerId} is already running");
                return;
            }

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _totalHashes = 0;

            Logger.Info($"Starting mining worker: {_workerId}");

            _miningTask = Task.Run(async () =>
            {
                await MiningLoop(_cancellationTokenSource.Token);
            }, _cancellationTokenSource.Token);

            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (!_isRunning)
            {
                Logger.Warning($"MiningWorker {_workerId} is not running");
                return;
            }

            Logger.Info($"Stopping mining worker: {_workerId}");

            _cancellationTokenSource.Cancel();
            
            if (_miningTask != null)
            {
                try
                {
                    await _miningTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
            }

            _isRunning = false;
            Logger.Info($"Mining worker {_workerId} stopped");
        }

        private async Task MiningLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Get challenge from server
                    var challengeResponse = await _client.GetChallengeAsync(_workerId);
                    
                    if (!challengeResponse.Success || challengeResponse.Challenge == null)
                    {
                        Logger.Warning($"Failed to get challenge for worker {_workerId}");
                        await Task.Delay(5000, cancellationToken); // Wait 5 seconds before retrying
                        continue;
                    }

                    var challenge = challengeResponse.Challenge;
                    Logger.Info($"Received challenge {challenge.ChallengeId} with difficulty {challenge.Difficulty}");

                    // Mine for solution
                    var solution = await MineChallenge(challenge, cancellationToken);
                    
                    if (solution != null)
                    {
                        // Submit solution
                        var solutionRequest = new SolutionRequest
                        {
                            WorkerId = _workerId,
                            ChallengeId = challenge.ChallengeId,
                            Nonce = solution
                        };

                        var solutionResponse = await _client.SubmitSolutionAsync(solutionRequest);
                        
                        var completedArgs = new MiningCompletedEventArgs
                        {
                            Success = solutionResponse.Success,
                            Message = solutionResponse.Message,
                            Receipt = solutionResponse.Receipt,
                            WorkerId = _workerId
                        };

                        MiningCompleted?.Invoke(this, completedArgs);
                        
                        if (solutionResponse.Success)
                        {
                            Logger.Info($"Solution accepted for worker {_workerId}, reward: {solutionResponse.Receipt?.Reward}");
                        }
                        else
                        {
                            Logger.Warning($"Solution rejected for worker {_workerId}: {solutionResponse.Message}");
                        }
                    }
                    else
                    {
                        Logger.Warning($"No solution found for challenge {challenge.ChallengeId}");
                    }

                    // Small delay before getting next challenge
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Mining loop cancelled for worker {_workerId}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in mining loop for worker {_workerId}", ex, _workerId);
                
                var completedArgs = new MiningCompletedEventArgs
                {
                    Success = false,
                    Message = $"Mining error: {ex.Message}",
                    WorkerId = _workerId
                };

                MiningCompleted?.Invoke(this, completedArgs);
            }
        }

        private async Task<string?> MineChallenge(Challenge challenge, CancellationToken cancellationToken)
        {
            Logger.Info($"Starting to mine challenge {challenge.ChallengeId} with difficulty {challenge.Difficulty}");

            var nonce = 0UL;
            var startTime = DateTime.UtcNow;
            var lastHashRateUpdate = startTime;

            while (!cancellationToken.IsCancellationRequested)
            {
                var nonceString = nonce.ToString();
                var hash = AshMaizeHasher.ComputeHash(challenge.ChallengeString, nonceString);
                
                _totalHashes++;

                // Check if hash meets difficulty requirement
                if (AshMaizeHasher.IsValidHash(hash, challenge.Difficulty))
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    Logger.Info($"Found solution for challenge {challenge.ChallengeId} in {elapsed.TotalSeconds:F2} seconds, nonce: {nonceString}");
                    return nonceString;
                }

                // Update progress periodically
                var now = DateTime.UtcNow;
                if ((now - lastHashRateUpdate).TotalSeconds >= 1) // Update every second
                {
                    var hashesPerSecond = _totalHashes / Math.Max(1, (now - startTime).TotalSeconds);
                    
                    var progressArgs = new MiningProgressEventArgs
                    {
                        HashesPerSecond = (int)hashesPerSecond,
                        TotalHashes = _totalHashes,
                        Difficulty = challenge.Difficulty,
                        WorkerId = _workerId,
                        HashRate = hashesPerSecond,
                        HashCount = _totalHashes,
                        CurrentNonce = nonceString
                    };

                    ProgressUpdated?.Invoke(this, progressArgs);
                    ProgressChanged?.Invoke(this, progressArgs);
                    lastHashRateUpdate = now;
                }

                nonce++;

                // Prevent excessive CPU usage
                if (nonce % 1000 == 0)
                {
                    await Task.Delay(1, cancellationToken);
                }
            }

            return null;
        }

        public async Task<string?> FindNonceAsync(Challenge challenge, int numThreads, CancellationToken cancellationToken)
        {
            try
            {
                _totalHashes = 0;
                var start = DateTime.UtcNow;

                var nonce = await MineChallenge(challenge, cancellationToken);

                var completedArgs = new MiningCompletedEventArgs
                {
                    Success = !string.IsNullOrEmpty(nonce),
                    Message = !string.IsNullOrEmpty(nonce) ? "Found nonce" : "No nonce found",
                    WorkerId = _workerId,
                    Nonce = nonce,
                    TotalHashes = _totalHashes,
                    Duration = DateTime.UtcNow - start
                };

                MiningCompleted?.Invoke(this, completedArgs);
                return nonce;
            }
            catch (OperationCanceledException)
            {
                var completedArgs = new MiningCompletedEventArgs
                {
                    Success = false,
                    Message = "Mining cancelled",
                    WorkerId = _workerId
                };
                MiningCompleted?.Invoke(this, completedArgs);
                return null;
            }
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
                if (_isRunning)
                {
                    StopAsync().GetAwaiter().GetResult();
                }

                _cancellationTokenSource?.Dispose();
                _disposed = true;
                Logger.Info($"MiningWorker {_workerId} disposed");
            }
        }
    }
}