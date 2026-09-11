using System.Text;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Notifications.Providers;

/// <summary>
/// ntfy 出站 HTTPS 推送提供者
/// </summary>
public class NtfyProvider : INotificationProvider
{
    private readonly NtfyConfig _config;
    private readonly SimpleLogger _logger;
    private readonly HttpClient _httpClient;

    public string Name => "ntfy";
    public bool IsEnabled => _config.Enabled && !string.IsNullOrWhiteSpace(_config.Topic);

    public NtfyProvider(NtfyConfig config, SimpleLogger logger, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> SendAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.Warn("[NtfyProvider] ntfy 渠道未启用或 Topic 为空，跳过发送。");
            return false;
        }

        var baseUrl = _config.ServerUrl.TrimEnd('/');
        var targetUrl = $"{baseUrl}/{_config.Topic}";

        // 构造消息正文
        var bodyText = notification.Body;

        using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl)
        {
            Content = new StringContent(bodyText, Encoding.UTF8, "text/plain")
        };

        // 设置 ntfy 头信息 (Title, Priority, Tags)
        request.Headers.TryAddWithoutValidation("Title", EncodeHeaderValue(notification.Title));
        request.Headers.TryAddWithoutValidation("Priority", _config.Priority);

        var tag = notification.Type == EventType.PC_BOOT ? "computer" : "unlock";
        var tags = string.IsNullOrWhiteSpace(_config.Tags) ? tag : $"{_config.Tags},{tag}";
        request.Headers.TryAddWithoutValidation("Tags", tags);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.Info($"[NtfyProvider] 推送成功 -> Topic: '{_config.Topic[..Math.Min(8, _config.Topic.Length)]}***'");
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.Warn($"[NtfyProvider] 推送返回非成功状态码 {(int)response.StatusCode}: {errorBody}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"[NtfyProvider] 发送请求异常: {ex.Message}");
            throw; // 抛出异常由上层 Retry 机制调度
        }
    }

    /// <summary>
    /// 对 HTTP Header 中的非 ASCII 字符进行 RFC 2047 Base64 编码，防止中文标题报错
    /// </summary>
    private static string EncodeHeaderValue(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.All(c => c < 128)) return text;

        var bytes = Encoding.UTF8.GetBytes(text);
        var base64 = Convert.ToBase64String(bytes);
        return $"=?utf-8?B?{base64}?=";
    }
}
