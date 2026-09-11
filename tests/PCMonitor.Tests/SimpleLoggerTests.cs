using PCMonitor.Core.Common;
using Xunit;

namespace PCMonitor.Tests;

public class SimpleLoggerTests : IDisposable
{
    private readonly string _tempDir;

    public SimpleLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pcnotify_logtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Sanitize_MasksSensitiveTokensAndSecrets()
    {
        var rawMessage = "Request with token=mySecretToken12345 and secret: abcdef987654321";
        var sanitized = SimpleLogger.Sanitize(rawMessage);

        Assert.DoesNotContain("mySecretToken12345", sanitized);
        Assert.DoesNotContain("abcdef987654321", sanitized);
        Assert.Contains("token=my****45", sanitized);
        Assert.Contains("secret=ab****21", sanitized);
    }

    [Fact]
    public void SimpleLogger_WritesToFileCorrectly()
    {
        var logger = new SimpleLogger(_tempDir, consoleOutput: false);
        var testMsg = "PCNotify test log entry";

        logger.Info(testMsg);

        var logFiles = Directory.GetFiles(_tempDir, "pcmonitor_*.log");
        Assert.Single(logFiles);

        var content = File.ReadAllText(logFiles[0]);
        Assert.Contains("[INFO]", content);
        Assert.Contains(testMsg, content);
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
