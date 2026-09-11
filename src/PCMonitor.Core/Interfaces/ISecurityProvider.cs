namespace PCMonitor.Core.Interfaces;

/// <summary>
/// 敏感数据安全加解密提供者接口
/// </summary>
public interface ISecurityProvider
{
    /// <summary>
    /// 加密明文字符串
    /// </summary>
    string Protect(string plainText);

    /// <summary>
    /// 解密密文字符串（若非密文则返回原样）
    /// </summary>
    string Unprotect(string cipherText);
}
