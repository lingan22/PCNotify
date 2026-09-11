using System.Text;
using System.Text.Json;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Notifications.Providers;

/// <summary>
/// WxPusher 微信扫码推送提供者（免传身份证实名认证）
/// </summary>
public class WxPusherProvider : INotificationProvider
{
    private readonly WxPusherConfig _config;
    private readonly SimpleLogger _logger;
    private readonly HttpClient _httpClient;

    public string Name => "WxPusher";
    public bool IsEnabled => _config.Enabled &&
                             !string.IsNullOrWhiteSpace(_config.AppToken) &&
                             !string.IsNullOrWhiteSpace(_config.Uids);

    public WxPusherProvider(WxPusherConfig config, SimpleLogger logger, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> SendAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.Info("[WxPusherProvider] WxPusher 未启用或 AppToken/UIDs 为空，已跳过。");
            return false;
        }

        var uids = _config.Uids.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var content = $"### 🔔 {notification.Title}\n\n" +
                      $"- **设备名称**: {notification.DeviceName}\n" +
                      $"- **发生时间**: {notification.LocalTimestampFormatted}\n\n" +
                      $"{notification.Body}";

        var payload = new
        {
            appToken = _config.AppToken,
            content = content,
            summary = notification.Title,
            contentType = 3, // 3 为 Markdown 格式
            uids = uids
        };

        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var json = JsonSerializer.Serialize(payload, jsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://wxpusher.zjiecode.com/api/send/message")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("code", out var codeElem) && codeElem.GetInt32() == 1000)
                {
                    _logger.Info("[WxPusherProvider] WxPusher 微信消息发送成功！");
                    return true;
                }

                var msg = doc.RootElement.TryGetProperty("msg", out var msgElem) ? msgElem.GetString() : responseBody;
                _logger.Warn($"[WxPusherProvider] WxPusher 接口返回业务错误: {msg}");
                return false;
            }

            _logger.Warn($"[WxPusherProvider] 响应 HTTP 错误 {(int)response.StatusCode}: {responseBody}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"[WxPusherProvider] 发送请求异常: {ex.Message}");
            throw;
        }
    }
}
