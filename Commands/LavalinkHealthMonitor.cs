using Lavalink4NET;

namespace Clara_bot.Commands;

/// <summary>
/// Starts Lavalink4NET when the node becomes available and continuously reports
/// node outages/recovery. After StartAsync succeeds, Lavalink4NET owns websocket
/// reconnect/resume; this monitor also covers the important case where Lavalink
/// was offline while the Discord bot started.
/// </summary>
public sealed class LavalinkHealthMonitor : IAsyncDisposable
{
    private static readonly Uri VersionEndpoint = new("http://127.0.0.1:2333/version");
    private readonly IAudioService _audioService;
    private readonly BotLoggingService _logger;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private Task? _monitorTask;
    private bool _audioServiceStarted;
    private bool? _lastNodeHealth;

    public LavalinkHealthMonitor(IAudioService audioService, BotLoggingService logger)
    {
        _audioService = audioService;
        _logger = logger;
    }

    public void Start()
    {
        _monitorTask ??= Task.Run(() => MonitorAsync(_stopping.Token));
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        var retryAttempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var isHealthy = await IsNodeHealthyAsync(cancellationToken).ConfigureAwait(false);

            if (isHealthy)
            {
                if (_lastNodeHealth == false)
                {
                    _logger.Log("✅ Lavalink đã hoạt động trở lại; đang khôi phục kết nối node...");
                }

                retryAttempt = 0;
                await EnsureAudioServiceStartedAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                retryAttempt++;
                if (_lastNodeHealth != false)
                {
                    _logger.Log("⚠️ Mất kết nối Lavalink. Bot sẽ tự động kết nối lại khi node hoạt động.");
                }
            }

            _lastNodeHealth = isHealthy;
            var delay = isHealthy
                ? TimeSpan.FromSeconds(10)
                : TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(retryAttempt, 5))));

            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task EnsureAudioServiceStartedAsync(CancellationToken cancellationToken)
    {
        if (_audioServiceStarted)
        {
            return;
        }

        await _startLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_audioServiceStarted)
            {
                return;
            }

            try
            {
                await _audioService.StartAsync(cancellationToken).ConfigureAwait(false);
                _audioServiceStarted = true;
                _logger.Log("✅ Lavalink4NET đã kết nối; tự động reconnect/resume đã sẵn sàng.");
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.Log($"⚠️ Chưa thể kết nối Lavalink: {ex.Message}. Sẽ thử lại.");
            }
        }
        finally
        {
            _startLock.Release();
        }
    }

    private async Task<bool> IsNodeHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, VersionEndpoint);
            request.Headers.TryAddWithoutValidation("Authorization", "youshallnotpass");
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_monitorTask is not null)
        {
            try
            {
                await _monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _stopping.Dispose();
        _startLock.Dispose();
        _httpClient.Dispose();
    }
}
