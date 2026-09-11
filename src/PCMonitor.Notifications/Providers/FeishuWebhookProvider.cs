using System.Text;
using System.Text.Json;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Notifications.Providers;

/// <summary>
/// 飞书自定义机器人 Webhook 推送提供者（免实名，移动端秒弹大横幅卡片）
/// </summary>
public class FeishuWebhookProvider : INotificationProvider
{
    private readonly FeishuConfig _config;
    private readonly SimpleLogger _logger;
    private readonly HttpClient _httpClient;

    public string Name => "飞书(Feishu)";
    public bool IsEnabled => _config.Enabled && !string.IsNullOrWhiteSpace(_config.WebhookUrl);

    public FeishuWebhookProvider(FeishuConfig config, SimpleLogger logger, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> SendAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.Info("[FeishuWebhookProvider] 飞书 Webhook 未启用或 URL 为空，已跳过。");
            return false;
        }

        var headerColor = notification.Type == EventType.PC_BOOT ? "green" : "blue";
        var contentMd = $"**设备名称**: {notification.DeviceName}\n" +
                        $"**发生时间**: {notification.LocalTimestampFormatted}\n\n" +
                        $"{notification.Body}";

        var payload = new
        {
            msg_type = "interactive",
            card = new
            {
                header = new
                {
                    title = new
                    {
                        tag = "plain_text",
                        content = $"🔔 {notification.Title}"
                    },
                    template = headerColor
                },
                elements = new object[]
                {
                    new
                    {
                        tag = "div",
                        text = new
                        {
                            tag = "lark_md",
                            content = contentMd
                        }
                    }
                }
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
                var isSuccess = false;

                if (doc.RootElement.TryGetProperty("code", out var codeElem) && codeElem.GetInt32() == 0)
                {
                    isSuccess = true;
                }
                else if (doc.RootElement.TryGetProperty("StatusCode", out var statusElem) && statusElem.GetInt32() == 0)
                {
                    isSuccess = true;
                }

                if (isSuccess)
                {
                    _logger.Info("[FeishuWebhookProvider] 飞书 Webhook 消息发送成功！");
                    return true;
                }

                var msg = doc.RootElement.TryGetProperty("msg", out var msgElem) ? msgElem.GetString() : responseBody;
                _logger.Warn($"[FeishuWebhookProvider] 飞书接口返回业务错误: {msg}");
                return false;
            }

            _logger.Warn($"[FeishuWebhookProvider] 响应 HTTP 错误 {(int)response.StatusCode}: {responseBody}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"[FeishuWebhookProvider] 发送请求异常: {ex.Message}");
            throw;
        }
    }
}
