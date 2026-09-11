using System.Net;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Models;
using PCMonitor.Notifications;
using PCMonitor.Notifications.Providers;
using Xunit;

namespace PCMonitor.Tests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> syncHandler)
    {
        _handler = req => Task.FromResult(syncHandler(req));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request);
    }
}

public class ProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SimpleLogger _logger;

    public ProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pcnotify_providertest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _logger = new SimpleLogger(_tempDir, consoleOutput: false);
    }

    [Fact]
    public async Task NtfyProvider_SendsPostRequest_WithHeadersAndContent()
    {
        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = new HttpClient(mockHandler);
        var config = new NtfyConfig
        {
            Enabled = true,
            ServerUrl = "https://ntfy.sh",
            Topic = "test_topic_unit",
            Priority = "high"
        };

        var provider = new NtfyProvider(config, _logger, client);
        var evt = new NotificationEvent
        {
            Type = EventType.PC_UNLOCK,
            Title = "测试标题",
            Body = "测试详情内容"
        };

        var result = await provider.SendAsync(evt);

        Assert.True(result);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://ntfy.sh/test_topic_unit", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task WeChatProvider_SendsPushPlusJsonPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockHandler = new MockHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            if (req.Content != null)
            {
                capturedBody = await req.Content.ReadAsStringAsync();
            }
            var jsonResponse = "{\"code\":200,\"msg\":\"请求成功\",\"data\":\"abc\"}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };
        });

        var client = new HttpClient(mockHandler);
        var config = new WeChatConfig
        {
            Enabled = true,
            Token = "test_token_123",
            ApiUrl = "https://www.pushplus.plus/send"
        };

        var provider = new WeChatProvider(config, _logger, client);
        var evt = new NotificationEvent
        {
            Type = EventType.PC_BOOT,
            Title = "电脑已登录",
            Body = "详情"
        };

        var result = await provider.SendAsync(evt);

        Assert.True(result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://www.pushplus.plus/send", capturedRequest.RequestUri?.ToString());
        Assert.Contains("test_token_123", capturedBody);
        Assert.Contains("电脑已登录", capturedBody);
    }

    [Fact]
    public async Task NotificationManager_DispatchesToEnabledProviders_AndRetriesOnFailure()
    {
        var attemptCount = 0;
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                // 第一次抛出网络异常模拟断网
                throw new HttpRequestException("Simulated network down");
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = new HttpClient(mockHandler);
        var ntfyConfig = new NtfyConfig { Enabled = true, Topic = "test_topic" };
        var retryConfig = new RetryConfig
        {
            MaxRetries = 3,
            InitialDelaySeconds = 1,
            BackoffMultiplier = 1.0
        };

        var manager = new NotificationManager(_logger, retryConfig);
        manager.RegisterProvider(new NtfyProvider(ntfyConfig, _logger, client));

        var evt = new NotificationEvent
        {
            Type = EventType.PC_UNLOCK,
            Title = "重试测试",
            Body = "内容"
        };

        var results = await manager.DispatchAsync(evt);

        Assert.True(results["ntfy"]);
        Assert.Equal(2, attemptCount); // 验证发生过 1 次重试并在第 2 次成功
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }
}
