using System.Text;
using System.Text.Json;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Notifications.Providers;

/// <summary>
/// 企业微信群机器人 Webhook 推送提供者（免身份证实名，可直接推送到个人普通微信）
/// </summary>
public class WeComWebhookProvider : INotificationProvider
{
    private readonly WeComConfig _config;
    private readonly SimpleLogger _logger;
    private readonly HttpClient _httpClient;

    public string Name => "企业微信(WeCom)";
    public bool IsEnabled => _config.Enabled && !string.IsNullOrWhiteSpace(_config.WebhookUrl);

    public WeComWebhookProvider(WeComConfig config, SimpleLogger logger, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> SendAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.Info("[WeComWebhookProvider] 企业微信 Webhook 未启用或 URL 为空，已跳过。");
            return false;
        }

        var markdownContent = $"### 🔔 {notification.Title}\n" +
                              $"> **设备名称**: <font color=\"comment\">{notification.DeviceName}</font>\n" +
                              $"> **发生时间**: <font color=\"comment\">{notification.LocalTimestampFormatted}</font>\n\n" +
                              $"{notification.Body}";

        var payload = new
        {
            msgtype = "markdown",
            markdown = new
            {
                content = markdownContent
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
                    _logger.Info("[WeComWebhookProvider] 企业微信 Webhook 消息发送成功！");
                    return true;
                }

                var errMsg = doc.RootElement.TryGetProperty("errmsg", out var msgElem) ? msgElem.GetString() : responseBody;
                _logger.Warn($"[WeComWebhookProvider] 接口返回业务错误: {errMsg}");
                return false;
            }

            _logger.Warn($"[WeComWebhookProvider] 响应 HTTP 错误 {(int)response.StatusCode}: {responseBody}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"[WeComWebhookProvider] 发送请求异常: {ex.Message}");
            throw;
        }
    }
}
