using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AcadMcp.Companion.Agent.Settings;

/// <summary>
/// Bring-your-own-key storage. Each provider's API key is encrypted with Windows DPAPI
/// (current-user scope) and written to %LocalAppData%\AutoCAD AI\&lt;provider&gt;.key.
/// Keys never leave the machine, are never stored in plaintext, and are not part of any
/// installer payload.
/// </summary>
public static class SecureKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AutoCAD-AI.Companion.v1");

    private static string KeyPath(ProviderKind kind)
        => Path.Combine(AppPaths.DataDir, $"{kind.ToString().ToLowerInvariant()}.key");

    public static bool HasKey(ProviderKind kind) => File.Exists(KeyPath(kind));

    public static void SaveKey(ProviderKind kind, string apiKey)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var plain = Encoding.UTF8.GetBytes(apiKey);
        var cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(KeyPath(kind), cipher);
        Array.Clear(plain);
    }

    public static string? LoadKey(ProviderKind kind)
    {
        var path = KeyPath(kind);
        if (!File.Exists(path)) return null;
        try
        {
            var cipher = File.ReadAllBytes(path);
            var plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            // Key encrypted under a different user/machine, or corrupted.
            return null;
        }
    }

    public static void DeleteKey(ProviderKind kind)
    {
        var path = KeyPath(kind);
        if (File.Exists(path)) File.Delete(path);
    }
}
