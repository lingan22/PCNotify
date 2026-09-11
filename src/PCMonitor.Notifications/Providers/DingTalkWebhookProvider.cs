using System.Text;
using System.Text.Json;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Notifications.Providers;

/// <summary>
/// 钉钉自定义机器人 Webhook 推送提供者
/// </summary>
public class DingTalkWebhookProvider : INotificationProvider
{
    private readonly DingTalkConfig _config;
    private readonly SimpleLogger _logger;
    private readonly HttpClient _httpClient;

    public string Name => "钉钉(DingTalk)";
    public bool IsEnabled => _config.Enabled && !string.IsNullOrWhiteSpace(_config.WebhookUrl);

    public DingTalkWebhookProvider(DingTalkConfig config, SimpleLogger logger, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> SendAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.Info("[DingTalkWebhookProvider] 钉钉 Webhook 未启用或 URL 为空，已跳过。");
            return false;
        }

        var markdownText = $"### 🔔 {notification.Title}\n\n" +
                           $"- **设备名称**: {notification.DeviceName}\n" +
                           $"- **发生时间**: {notification.LocalTimestampFormatted}\n\n" +
                           $"{notification.Body}";

        var payload = new
        {
            msgtype = "markdown",
            markdown = new
            {
                title = notification.Title,
                text = markdownText
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var json = JsonSerializer.Serialize(payload, jsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, _config.WebhookUrl)
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
                if (doc.RootElement.TryGetProperty("errcode", out var errCodeElem) && errCodeElem.GetInt32() == 0)
                {
                    _logger.Info("[DingTalkWebhookProvider] 钉钉 Webhook 消息发送成功！");
                    return true;
                }

                var errMsg = doc.RootElement.TryGetProperty("errmsg", out var msgElem) ? msgElem.GetString() : responseBody;
                _logger.Warn($"[DingTalkWebhookProvider] 钉钉接口返回业务错误: {errMsg}");
                return false;
            }

            _logger.Warn($"[DingTalkWebhookProvider] 响应 HTTP 错误 {(int)response.StatusCode}: {responseBody}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"[DingTalkWebhookProvider] 发送请求异常: {ex.Message}");
            throw;
        }
    }
}
