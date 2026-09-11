using System.Text;
using System.Text.Json;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Notifications.Providers;

/// <summary>
/// 微信 (PushPlus HTTP API) 出站推送提供者
/// </summary>
public class WeChatProvider : INotificationProvider
{
    private readonly WeChatConfig _config;
    private readonly SimpleLogger _logger;
    private readonly HttpClient _httpClient;

    public string Name => "WeChat(PushPlus)";
    public bool IsEnabled => _config.Enabled && !string.IsNullOrWhiteSpace(_config.Token);

    public WeChatProvider(WeChatConfig config, SimpleLogger logger, HttpClient? httpClient = null)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> SendAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.Info("[WeChatProvider] 微信推送未启用或 Token 未配置，已跳过。");
            return false;
        }

        var payload = new
        {
            token = _config.Token,
            title = notification.Title,
            content = notification.Body,
            template = _config.Template,
            channel = _config.Channel
        };

        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var json = JsonSerializer.Serialize(payload, jsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, _config.ApiUrl)
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
                if (doc.RootElement.TryGetProperty("code", out var codeElem) && codeElem.GetInt32() == 200)
                {
                    _logger.Info("[WeChatProvider] 微信推送成功！");
                    return true;
                }

                var msg = doc.RootElement.TryGetProperty("msg", out var msgElem) ? msgElem.GetString() : responseBody;
                _logger.Warn($"[WeChatProvider] PushPlus 接口返回业务错误: {msg}");
                return false;
            }

            _logger.Warn($"[WeChatProvider] PushPlus 响应 HTTP 错误 {(int)response.StatusCode}: {responseBody}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"[WeChatProvider] 发送请求异常: {ex.Message}");
            throw;
        }
    }
}
