using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Windows.SystemInfo;
using Xunit;

namespace PCMonitor.Tests;

public class SystemInfoCollectorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SimpleLogger _logger;

    public SystemInfoCollectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pcnotify_infotest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _logger = new SimpleLogger(_tempDir, consoleOutput: false);
    }

    [Fact]
    public async Task CaptureAsync_ReturnsValidHardwareSnapshot()
    {
        var collector = new SystemInfoCollector(_logger, new SystemInfoConfig
        {
            Enabled = true,
            IncludeCpu = true,
            IncludeMemory = true,
            IncludeDisks = true,
            IncludeNetwork = true
        });

        var snapshot = await collector.CaptureAsync();

        Assert.NotNull(snapshot);
        // 物理内存应大于 0
        Assert.True(snapshot.TotalMemoryGb > 0);
        Assert.True(snapshot.UsedMemoryGb >= 0);
        Assert.True(snapshot.MemoryUsagePercentage > 0 && snapshot.MemoryUsagePercentage <= 100);

        // 至少有一个系统磁盘（C盘等）
        Assert.NotEmpty(snapshot.Disks);
        Assert.Contains(snapshot.Disks, d => d.TotalGb > 0);

        // 网络状态不为空
        Assert.False(string.IsNullOrWhiteSpace(snapshot.NetworkStatus));

        // 格式化输出不为空
        Assert.NotEmpty(snapshot.ToPlainText());
        Assert.NotEmpty(snapshot.ToMarkdown());
    }

    [Fact]
    public async Task CaptureAsync_WhenIncludeDisksIsFalse_ExcludesDisksFromSnapshot()
    {
        var collector = new SystemInfoCollector(_logger, new SystemInfoConfig
        {
            Enabled = true,
            IncludeCpu = false,
            IncludeMemory = true,
            IncludeDisks = false,
            IncludeNetwork = true
        });

        var snapshot = await collector.CaptureAsync();

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Disks);
        Assert.DoesNotContain("磁盘:", snapshot.ToPlainText());
        Assert.DoesNotContain("磁盘空间:", snapshot.ToMarkdown());
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
