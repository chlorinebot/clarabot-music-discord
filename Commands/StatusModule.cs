using Discord;
using Discord.WebSocket;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clara_bot.Commands
{
    public class StatusModule : IDisposable
    {
        private readonly DiscordSocketClient _client;
        private readonly TimeSpan _idleTimeout;
        private Timer? _idleTimer;
        private bool _isIdle;
        private readonly object _lock = new object();
        private bool _disposed;
        private readonly BotLoggingService? _loggingService;

        public StatusModule(DiscordSocketClient client, TimeSpan? idleTimeout = null, BotLoggingService? loggingService = null)
        {
            _client = client;
            _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(5);
            _loggingService = loggingService;
        }

        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StatusModule));

            lock (_lock)
            {
                _idleTimer?.Dispose();
                _idleTimer = new Timer(OnIdleTimerElapsed, null, _idleTimeout, Timeout.InfiniteTimeSpan);
                (_loggingService ?? new BotLoggingService()).Log($"StatusModule đã khởi động. Timeout: {_idleTimeout.TotalMinutes} phút.");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _idleTimer?.Dispose();
                _idleTimer = null;
                (_loggingService ?? new BotLoggingService()).Log("StatusModule đã dừng.");
            }
        }

        public void RecordInteraction()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                if (_idleTimer != null)
                {
                    _idleTimer.Change(_idleTimeout, Timeout.InfiniteTimeSpan);
                }

                if (_isIdle)
                {
                    _isIdle = false;
                    _ = SetOnlineAsync();
                }
            }
        }

        private void OnIdleTimerElapsed(object? state)
        {
            lock (_lock)
            {
                if (!_isIdle && !_disposed)
                {
                    _isIdle = true;
                    _ = SetIdleAsync();
                }
            }
        }

        private async Task SetIdleAsync()
        {
            try
            {
                await _client.SetStatusAsync(UserStatus.Idle);
                await _client.SetActivityAsync(new Game("Đang chờ một tình yêu sẽ đến...", ActivityType.Watching));
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Bot chuyển sang trạng thái chờ (idle).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Lỗi khi đặt trạng thái idle: {ex.Message}");
            }
        }

        private async Task SetOnlineAsync()
        {
            try
            {
                await _client.SetStatusAsync(UserStatus.Online);
                await _client.SetActivityAsync(new Game("/heyclara", ActivityType.Listening));
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Bot chuyển về trạng thái trực tuyến (online).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Lỗi khi đặt trạng thái online: {ex.Message}");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _idleTimer?.Dispose();
                _idleTimer = null;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] StatusModule đã được dispose.");
            }
        }
    }
}
