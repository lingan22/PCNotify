using System.Text;

namespace PCMonitor.Core.Models;

public class DiskUsageInfo
{
    public string DriveName { get; set; } = string.Empty;
    public double TotalGb { get; set; }
    public double AvailableGb { get; set; }
    public double UsedGb => Math.Max(0, TotalGb - AvailableGb);
    public double FreePercentage => TotalGb > 0 ? (AvailableGb / TotalGb) * 100.0 : 0;

    public override string ToString() => $"{DriveName} [{AvailableGb:F1}GB 可用 / {TotalGb:F1}GB 总计 ({FreePercentage:F0}% 空闲)]";
}

/// <summary>
/// 硬件与系统状态快照
/// </summary>
public class SystemSnapshot
{
    public double? CpuUsagePercentage { get; set; }
    public double TotalMemoryGb { get; set; }
    public double UsedMemoryGb { get; set; }
    public double MemoryUsagePercentage => TotalMemoryGb > 0 ? (UsedMemoryGb / TotalMemoryGb) * 100.0 : 0;
    public List<DiskUsageInfo> Disks { get; set; } = new();
    public string NetworkStatus { get; set; } = "未连接网络";

    /// <summary>
    /// 生成 Markdown 格式的硬件与网络状态排版
    /// </summary>
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("### 💻 电脑运行状态");
        if (CpuUsagePercentage.HasValue)
        {
            sb.AppendLine($"- **CPU 负荷**: `{CpuUsagePercentage.Value:F1}%`");
        }
        if (TotalMemoryGb > 0)
        {
            sb.AppendLine($"- **内存使用**: `{UsedMemoryGb:F1} GB / {TotalMemoryGb:F1} GB` ({MemoryUsagePercentage:F0}%)");
        }
        if (Disks.Count > 0)
        {
            var diskStr = string.Join(", ", Disks.Select(d => $"{d.DriveName} 剩余 {d.AvailableGb:F1}GB"));
            sb.AppendLine($"- **磁盘空间**: {diskStr}");
        }
        sb.AppendLine($"- **网络连通**: `{NetworkStatus}`");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 生成纯文本一览
    /// </summary>
    public string ToPlainText()
    {
        var parts = new List<string>();
        if (CpuUsagePercentage.HasValue)
        {
            parts.Add($"CPU: {CpuUsagePercentage.Value:F0}%");
        }
        if (TotalMemoryGb > 0)
        {
            parts.Add($"内存: {UsedMemoryGb:F1}G/{TotalMemoryGb:F1}G ({MemoryUsagePercentage:F0}%)");
        }
        if (Disks.Count > 0)
        {
            var diskStr = string.Join(" | ", Disks.Select(d => $"{d.DriveName} 余{d.AvailableGb:F0}G"));
            parts.Add($"磁盘: {diskStr}");
        }
        parts.Add($"网络: {NetworkStatus}");
        return string.Join(" | ", parts);
    }
}
