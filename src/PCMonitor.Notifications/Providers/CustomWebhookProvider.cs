using System.Text;
using System.Text.Json;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Notifications.Providers;

/// <summary>
/// 通用自定义出站 Webhook 推送提供者（兼容自建服务、Server酱、Bark、PushDeer 等）
/// </summary>
public class CustomWebhookProvider : INotificationProvider
{
    private readonly CustomWebhookConfig _config;
    private readonly SimpleLogger _logger;
    private readonly HttpClient _httpClient;

    public string Name => "自定义Webhook";
    public bool IsEnabled => _config.Enabled && !string.IsNullOrWhiteSpace(_config.Url);

    public CustomWebhookProvider(CustomWebhookConfig config, SimpleLogger logger, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> SendAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.Info("[CustomWebhookProvider] 自定义 Webhook 未启用或 URL 为空，已跳过。");
            return false;
        }

        var payload = new
        {
            title = notification.Title,
            body = notification.Body,
            type = notification.Type.ToString(),
            deviceName = notification.DeviceName,
            timestamp = notification.LocalTimestampFormatted,
            metadata = notification.Metadata
        };

        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var json = JsonSerializer.Serialize(payload, jsonOptions);

        using var request = new HttpRequestMessage(
            string.Equals(_config.Method, "GET", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Get : HttpMethod.Post,
            _config.Url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.Info("[CustomWebhookProvider] 自定义 Webhook 请求发送成功！");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.Warn($"[CustomWebhookProvider] 自定义 Webhook 响应 HTTP 错误 {(int)response.StatusCode}: {responseBody}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"[CustomWebhookProvider] 发送请求异常: {ex.Message}");
            throw;
        }
    }
}
