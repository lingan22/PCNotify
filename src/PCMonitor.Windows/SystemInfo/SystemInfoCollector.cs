using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Models;

namespace PCMonitor.Windows.SystemInfo;

/// <summary>
/// Windows 系统状态安全采集器（极简、轻量、高稳，不采集任何隐私）
/// </summary>
public class SystemInfoCollector
{
    private readonly SimpleLogger _logger;
    private readonly SystemInfoConfig _config;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
        public ulong Value => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    public SystemInfoCollector(SimpleLogger logger, SystemInfoConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new SystemInfoConfig();
    }

    /// <summary>
    /// 异步采集系统运行状态快照
    /// </summary>
    public async Task<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = new SystemSnapshot();

        if (!_config.Enabled)
        {
            return snapshot;
        }

        try
        {
            // 1. 采集内存
            if (_config.IncludeMemory)
            {
                CaptureMemory(snapshot);
            }

            // 2. 采集固定磁盘
            if (_config.IncludeDisks)
            {
                CaptureDisks(snapshot);
            }

            // 3. 采集网络状态
            if (_config.IncludeNetwork)
            {
                CaptureNetwork(snapshot);
            }

            // 4. 采集 CPU 使用率（需采样微小时间差）
            if (_config.IncludeCpu)
            {
                snapshot.CpuUsagePercentage = await SampleCpuUsageAsync(250, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("[SystemInfoCollector] 采集系统状态异常", ex);
        }

        return snapshot;
    }

    private void CaptureMemory(SystemSnapshot snapshot)
    {
        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref memStatus))
        {
            snapshot.TotalMemoryGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
            var availGb = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
            snapshot.UsedMemoryGb = Math.Max(0, snapshot.TotalMemoryGb - availGb);
        }
    }

    private void CaptureDisks(SystemSnapshot snapshot)
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .OrderBy(d => d.Name);

            foreach (var drive in drives)
            {
                var total = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                var free = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                snapshot.Disks.Add(new DiskUsageInfo
                {
                    DriveName = drive.Name.TrimEnd('\\'),
                    TotalGb = total,
                    AvailableGb = free
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"[SystemInfoCollector] 读取磁盘信息警告: {ex.Message}");
        }
    }

    private void CaptureNetwork(SystemSnapshot snapshot)
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                snapshot.NetworkStatus = "未连接网络";
                return;
            }

            var activeInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            if (activeInterfaces.Count == 0)
            {
                snapshot.NetworkStatus = "在线 (网络正常)";
                return;
            }

            var isWifi = activeInterfaces.Any(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
            var isEthernet = activeInterfaces.Any(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet);

            if (isWifi && isEthernet)
            {
                snapshot.NetworkStatus = "在线 (Wi-Fi + 以太网)";
            }
            else if (isWifi)
            {
                snapshot.NetworkStatus = "在线 (Wi-Fi)";
            }
            else if (isEthernet)
            {
                snapshot.NetworkStatus = "在线 (以太网)";
            }
            else
            {
                snapshot.NetworkStatus = "在线";
            }
        }
        catch
        {
            snapshot.NetworkStatus = "检测异常";
        }
    }

    private static async Task<double?> SampleCpuUsageAsync(int sampleMs, CancellationToken cancellationToken)
    {
        if (!GetSystemTimes(out var idle1, out var kernel1, out var user1))
        {
            return null;
        }

        await Task.Delay(sampleMs, cancellationToken);

        if (!GetSystemTimes(out var idle2, out var kernel2, out var user2))
        {
            return null;
        }

        var usr = user2.Value - user1.Value;
        var ker = kernel2.Value - kernel1.Value;
        var idl = idle2.Value - idle1.Value;

        var sys = usr + ker;
        if (sys <= 0) return 0.0;

        var cpu = (double)(sys - idl) / sys * 100.0;
        return Math.Clamp(cpu, 0.0, 100.0);
    }
}
