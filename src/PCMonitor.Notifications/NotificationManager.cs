using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Notifications;

/// <summary>
/// 通知分发调度中心（并发独立分发、故障隔离与有限重试）
/// </summary>
public class NotificationManager
{
    private readonly List<INotificationProvider> _providers = new();
    private readonly SimpleLogger _logger;
    private readonly RetryConfig _retryConfig;

    public IReadOnlyList<INotificationProvider> Providers => _providers.AsReadOnly();

    public NotificationManager(SimpleLogger logger, RetryConfig? retryConfig = null)
    {
        _logger = logger;
        _retryConfig = retryConfig ?? new RetryConfig();
    }

    public void RegisterProvider(INotificationProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        _providers.Add(provider);
        _logger.Info($"[NotificationManager] 已注册通知渠道: {provider.Name} (启用状态: {provider.IsEnabled})");
    }

    public void ClearProviders()
    {
        _providers.Clear();
        _logger.Info("[NotificationManager] 已清空已注册通知渠道列表。");
    }

    public async Task<Dictionary<string, bool>> DispatchAsync(NotificationEvent evt, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, bool>();
        var activeProviders = _providers.Where(p => p.IsEnabled).ToList();

        if (activeProviders.Count == 0)
        {
            _logger.Warn("[NotificationManager] 当前未注册或未启用任何有效通知渠道。");
            return results;
        }

        _logger.Info($"[NotificationManager] 开始并发向 {activeProviders.Count} 个渠道分发通知 [{evt.Type}: {evt.Title}]...");

        // 并发执行所有启用的 Provider，实现完全隔离
        var tasks = activeProviders.Select(provider =>
            ExecuteWithRetryAsync(provider, evt, cancellationToken)).ToList();

        var executionResults = await Task.WhenAll(tasks);

        foreach (var (providerName, success) in executionResults)
        {
            results[providerName] = success;
        }

        return results;
    }

    private async Task<(string ProviderName, bool Success)> ExecuteWithRetryAsync(
        INotificationProvider provider,
        NotificationEvent evt,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _retryConfig.MaxRetries);
        var delaySeconds = Math.Max(1, _retryConfig.InitialDelaySeconds);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var success = await provider.SendAsync(evt, cancellationToken);
                if (success)
                {
                    return (provider.Name, true);
                }

                _logger.Warn($"[NotificationManager] 渠道 [{provider.Name}] 第 {attempt}/{maxAttempts} 次尝试返回失败。");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NotificationManager] 渠道 [{provider.Name}] 第 {attempt}/{maxAttempts} 次尝试发生异常: {ex.Message}");
            }

            // 若未达到最大重试次数，进行等待重试
            if (attempt < maxAttempts)
            {
                var currentDelay = (int)(delaySeconds * Math.Pow(_retryConfig.BackoffMultiplier, attempt - 1));
                _logger.Info($"[NotificationManager] 渠道 [{provider.Name}] 等待 {currentDelay} 秒后进行第 {attempt + 1} 次重试...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(currentDelay), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.Error($"[NotificationManager] 渠道 [{provider.Name}] 达到最大重试次数 ({maxAttempts})，最终发送失败。");
        return (provider.Name, false);
    }
}
