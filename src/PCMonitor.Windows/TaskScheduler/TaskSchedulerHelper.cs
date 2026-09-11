using System.Diagnostics;
using System.Security.Principal;
using PCMonitor.Core.Common;

namespace PCMonitor.Windows.TaskScheduler;

/// <summary>
/// Windows 任务计划程序 (Task Scheduler) 管理器
/// </summary>
public class TaskSchedulerHelper
{
    public const string DefaultTaskName = "PCNotify_PCMonitor";
    private readonly SimpleLogger _logger;
    private readonly string _taskName;

    public TaskSchedulerHelper(SimpleLogger logger, string taskName = DefaultTaskName)
    {
        _logger = logger;
        _taskName = taskName;
    }

    /// <summary>
    /// 生成符合需求特性的 Task Scheduler XML 描述
    /// 包括：登录自动触发、允许电池供电运行、阻止重复运行、异常后自动重试、无超时限制
    /// </summary>
    public string GenerateTaskXml(string exePath)
    {
        var currentUser = WindowsIdentity.GetCurrent().Name;
        var workingDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

        return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>PCNotify Windows 电脑状态监控与消息推送守护任务</Description>
    <Author>{currentUser}</Author>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{currentUser}</UserId>
      <Delay>PT10S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{currentUser}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
    <RestartOnFailure>
      <Interval>PT1M</Interval>
      <Count>3</Count>
    </RestartOnFailure>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{exePath}</Command>
      <Arguments>--run</Arguments>
      <WorkingDirectory>{workingDir}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
    }

    /// <summary>
    /// 注册任务至 Windows 任务计划程序
    /// </summary>
    public bool RegisterTask(string exePath, out string output)
    {
        output = string.Empty;
        var tempXmlPath = Path.Combine(Path.GetTempPath(), $"pcnotify_task_{Guid.NewGuid():N}.xml");

        try
        {
            var xml = GenerateTaskXml(exePath);
            File.WriteAllText(tempXmlPath, xml, System.Text.Encoding.Unicode);

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Create /XML \"{tempXmlPath}\" /TN \"{_taskName}\" /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                output = "无法启动 schtasks.exe 进程。";
                _logger.Error($"[TaskSchedulerHelper] {output}");
                return false;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            output = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : $"{stdout.Trim()} {stderr.Trim()}";
            var success = process.ExitCode == 0;

            if (success)
            {
                _logger.Info($"[TaskSchedulerHelper] 成功注册任务计划: {_taskName}。");
            }
            else
            {
                _logger.Error($"[TaskSchedulerHelper] 注册任务计划失败 (ExitCode: {process.ExitCode}): {output}");
            }

            return success;
        }
        catch (Exception ex)
        {
            output = ex.Message;
            _logger.Error("[TaskSchedulerHelper] 注册任务异常", ex);
            return false;
        }
        finally
        {
            if (File.Exists(tempXmlPath))
            {
                try { File.Delete(tempXmlPath); } catch { }
            }
        }
    }

    /// <summary>
    /// 从任务计划程序中移除任务
    /// </summary>
    public bool UnregisterTask(out string output)
    {
        output = string.Empty;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Delete /TN \"{_taskName}\" /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                output = "无法启动 schtasks.exe 进程。";
                return false;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            output = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : $"{stdout.Trim()} {stderr.Trim()}";
            var success = process.ExitCode == 0;

            if (success)
            {
                _logger.Info($"[TaskSchedulerHelper] 成功卸载任务计划: {_taskName}。");
            }
            else
            {
                _logger.Warn($"[TaskSchedulerHelper] 卸载任务计划: {output}");
            }

            return success;
        }
        catch (Exception ex)
        {
            output = ex.Message;
            _logger.Error("[TaskSchedulerHelper] 卸载任务异常", ex);
            return false;
        }
    }

    /// <summary>
    /// 查询任务是否已在系统中注册
    /// </summary>
    public bool IsTaskRegistered(out string output)
    {
        output = string.Empty;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{_taskName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
