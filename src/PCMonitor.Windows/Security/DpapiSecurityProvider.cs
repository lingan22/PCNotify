using System.Runtime.InteropServices;
using System.Text;
using PCMonitor.Core.Common;
using PCMonitor.Core.Interfaces;

namespace PCMonitor.Windows.Security;

/// <summary>
/// 基于 Windows 原生数据保护 API (DPAPI - CryptProtectData / CryptUnprotectData) 的零依赖凭据保护提供者
/// </summary>
public class DpapiSecurityProvider : ISecurityProvider
{
    private const string EncryptedPrefix = "enc:";
    private readonly SimpleLogger? _logger;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    private const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        StringBuilder? ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public DpapiSecurityProvider(SimpleLogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 对明文字符串使用当前 Windows 登录用户上下文进行 DPAPI 加密，前缀标记为 enc:
    /// </summary>
    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        // 避免重复加密
        if (plainText.StartsWith(EncryptedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return plainText;
        }

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var inHandle = Marshal.AllocHGlobal(plainBytes.Length);
            Marshal.Copy(plainBytes, 0, inHandle, plainBytes.Length);

            var inBlob = new DATA_BLOB { cbData = plainBytes.Length, pbData = inHandle };
            var outBlob = new DATA_BLOB();
            var emptyEntropy = new DATA_BLOB();

            try
            {
                var success = CryptProtectData(
                    ref inBlob,
                    "PCNotify_Credential",
                    ref emptyEntropy,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CRYPTPROTECT_UI_FORBIDDEN,
                    ref outBlob);

                if (!success)
                {
                    var err = Marshal.GetLastWin32Error();
                    _logger?.Warn($"[DpapiSecurityProvider] DPAPI 加密失败，错误码: {err}，将保留原值。");
                    return plainText;
                }

                var cipherBytes = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, cipherBytes, 0, outBlob.cbData);
                return EncryptedPrefix + Convert.ToBase64String(cipherBytes);
            }
            finally
            {
                Marshal.FreeHGlobal(inHandle);
                if (outBlob.pbData != IntPtr.Zero)
                {
                    LocalFree(outBlob.pbData);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"[DpapiSecurityProvider] 加密异常: {ex.Message}");
            return plainText;
        }
    }

    /// <summary>
    /// 解密带 enc: 前缀的密文字符串；若为普通明文则直接返回原值（向后兼容）
    /// </summary>
    public string Unprotect(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return string.Empty;
        }

        if (!cipherText.StartsWith(EncryptedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // 非密文字符串，直接返回原值
            return cipherText;
        }

        try
        {
            var base64 = cipherText[EncryptedPrefix.Length..];
            var cipherBytes = Convert.FromBase64String(base64);

            var inHandle = Marshal.AllocHGlobal(cipherBytes.Length);
            Marshal.Copy(cipherBytes, 0, inHandle, cipherBytes.Length);

            var inBlob = new DATA_BLOB { cbData = cipherBytes.Length, pbData = inHandle };
            var outBlob = new DATA_BLOB();
            var emptyEntropy = new DATA_BLOB();

            try
            {
                var success = CryptUnprotectData(
                    ref inBlob,
                    null,
                    ref emptyEntropy,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CRYPTPROTECT_UI_FORBIDDEN,
                    ref outBlob);

                if (!success)
                {
                    var err = Marshal.GetLastWin32Error();
                    _logger?.Warn($"[DpapiSecurityProvider] DPAPI 解密失败 (可能非当前用户加密)，错误码: {err}");
                    return cipherText;
                }

                var plainBytes = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, plainBytes, 0, outBlob.cbData);
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                Marshal.FreeHGlobal(inHandle);
                if (outBlob.pbData != IntPtr.Zero)
                {
                    LocalFree(outBlob.pbData);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"[DpapiSecurityProvider] 解密异常: {ex.Message}");
            return cipherText;
        }
    }
}
