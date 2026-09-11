using System.Diagnostics;
using System.Reflection;

namespace PCMonitor.Common;

/// <summary>
/// 统一管理 PCNotify 全局高清应用程序图标（窗体标题栏、任务栏、系统托盘保持 100% 一致）
/// </summary>
public static class AppIconHelper
{
    private static Icon? _cachedIcon;

    public static Icon GetAppIcon()
    {
        if (_cachedIcon != null)
        {
            return _cachedIcon;
        }

        // 1. 优先从程序集嵌入式资源加载 (PCMonitor.Assets.app.ico)
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("PCMonitor.Assets.app.ico");
            if (stream != null)
            {
                _cachedIcon = new Icon(stream);
                return _cachedIcon;
            }
        }
        catch
        {
            // 降级尝试
        }

        // 2. 尝试从当前执行文件直接提取关联图标 (针对发布后的单文件或 EXE)
        try
        {
            var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            if (File.Exists(exePath))
            {
                var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                {
                    _cachedIcon = icon;
                    return _cachedIcon;
                }
            }
        }
        catch
        {
            // 降级尝试
        }

        // 3. 尝试从物理文件目录加载 Assets/app.ico
        try
        {
            var fileIco = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(fileIco))
            {
                _cachedIcon = new Icon(fileIco);
                return _cachedIcon;
            }
        }
        catch
        {
            // 降级尝试
        }

        _cachedIcon = SystemIcons.Application;
        return _cachedIcon;
    }
}
