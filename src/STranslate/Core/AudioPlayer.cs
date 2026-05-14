using NAudio.Wave;
using Microsoft.Extensions.Logging;
using STranslate.Plugin;
using System.IO;
using System.Threading;

namespace STranslate.Core;

public class AudioPlayer : IAudioPlayer
{
    private readonly ILogger<AudioPlayer> _logger;
    private readonly IHttpService _httpService;
    private WaveOutEvent? _waveOut;
    private MemoryStream? _audioStream;
    private Mp3FileReader? _mp3Reader;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;
    private int _stopping;

    public AudioPlayer(ILogger<AudioPlayer> logger, IHttpService httpService)
    {
        _logger = logger;
        _httpService = httpService;
    }

    /// <summary>
    /// 当前是否正在播放
    /// </summary>
    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    /// <summary>
    /// 播放状态改变事件
    /// </summary>
    public event EventHandler<PlaybackState>? PlaybackStateChanged;

    /// <summary>
    /// 播放进度事件
    /// </summary>
    public event EventHandler<TimeSpan>? PlaybackPositionChanged;

    public async Task PlayAsync(byte[]? audioData, CancellationToken cancellationToken = default)
    {
        if (audioData == null || audioData.Length == 0)
        {
            _logger.LogWarning("音频数据为空");
            return;
        }
        try
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // 创建内存流
            _audioStream = new MemoryStream(audioData);
            // 创建MP3读取器
            _mp3Reader = new Mp3FileReader(_audioStream);
            // 创建播放设备
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_mp3Reader);
            // 注册事件
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            // 开始播放
            _waveOut.Play();
            OnPlaybackStateChanged(PlaybackState.Playing);
            _logger.LogInformation("音频播放开始");
            // 启动进度监控
            await MonitorPlaybackProgressAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogTrace("音频播放被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "播放音频时发生错误");
            throw;
        }
        finally
        {
            // 统一清理入口：由 finally 唯一负责调用 StopAsync
            await StopAsync();
        }
    }

    public async Task PlayAsync(string audioUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            _logger.LogWarning("音频URL为空");
            return;
        }

        byte[]? audioData = null;
        try
        {
            _logger.LogInformation("开始下载音频: {AudioUrl}", audioUrl);

            audioData = await _httpService.GetAsBytesAsync(audioUrl, cancellationToken);

            _logger.LogInformation("音频下载完成: {Length} bytes", audioData?.Length ?? 0);
        }
        catch (OperationCanceledException)
        {
            _logger.LogTrace("音频下载被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载音频时发生错误: {AudioUrl}", audioUrl);
        }

        await PlayAsync(audioData, cancellationToken);
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    public async Task StopAsync()
    {
        // 并发门：用 Interlocked 保证只有第一个调用者真正执行，后来的直接返回
        if (Interlocked.CompareExchange(ref _stopping, 1, 0) != 0)
            return;

        try
        {
            _cancellationTokenSource?.Cancel();

            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Stop();

                // 将 Dispose 移到 ThreadPool，避免 UI 线程持 STA 锁时死锁
                var waveOut = _waveOut;
                _waveOut = null;
                await Task.Run(() => waveOut.Dispose());
            }

            _mp3Reader?.Dispose();
            _mp3Reader = null;

            _audioStream?.Dispose();
            _audioStream = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            OnPlaybackStateChanged(PlaybackState.Stopped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止音频播放时发生错误");
        }
        finally
        {
            // 重置并发门，允许下次播放
            Interlocked.Exchange(ref _stopping, 0);
        }
    }

    /// <summary>
    /// 暂停播放
    /// </summary>
    public void Pause()
    {
        if (_waveOut?.PlaybackState == PlaybackState.Playing)
        {
            _waveOut.Pause();
            OnPlaybackStateChanged(PlaybackState.Paused);
            _logger.LogTrace("音频播放已暂停");
        }
    }

    /// <summary>
    /// 恢复播放
    /// </summary>
    public void Resume()
    {
        if (_waveOut?.PlaybackState == PlaybackState.Paused)
        {
            _waveOut.Play();
            OnPlaybackStateChanged(PlaybackState.Playing);
            _logger.LogTrace("音频播放已恢复");
        }
    }

    /// <summary>
    /// 监控播放进度
    /// </summary>
    private async Task MonitorPlaybackProgressAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   _mp3Reader != null &&
                   _waveOut?.PlaybackState == PlaybackState.Playing)
            {
                var currentPosition = _mp3Reader.CurrentTime;
                PlaybackPositionChanged?.Invoke(this, currentPosition);

                await Task.Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogTrace("音频播放被取消(Monitor)");
            // 不再调用 StopAsync，由 PlayAsync 的 finally 统一清理
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "监控播放进度时发生错误");
        }
    }

    /// <summary>
    /// 播放停止事件处理
    /// </summary>
    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        OnPlaybackStateChanged(PlaybackState.Stopped);

        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "音频播放意外停止");
        }
        else
        {
            _logger.LogTrace("音频播放正常结束");
        }

        // 不再调用 StopAsync，由 PlayAsync 的 finally 统一清理
        // 只负责取消 CancellationToken 以通知 MonitorPlaybackProgressAsync
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// 触发播放状态改变事件
    /// </summary>
    private void OnPlaybackStateChanged(PlaybackState state)
    {
        PlaybackStateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // 使用 Task.Run 避免在 UI 线程上同步等待
            _ = Task.Run(async () => await StopAsync());
            _disposed = true;
        }
    }
}