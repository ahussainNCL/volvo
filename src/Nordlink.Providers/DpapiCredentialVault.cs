using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Nordlink.Contracts;
using Nordlink.Storage;

namespace Nordlink.Providers;

public sealed class DpapiCredentialVault(WorkspaceStore store)
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Nordlink.Credentials.v1");
    public void Save(SecurityCredential metadata, ReadOnlySpan<byte> material)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Hardware credential storage requires Windows DPAPI.");
        if (store.Environment != EnvironmentKind.Hardware || metadata.Context.Environment != EnvironmentKind.Hardware)
            throw new InvalidOperationException("Hardware credentials cannot be stored in simulation.");
        if (!Guid.TryParseExact(metadata.Id, "N", out _) || material.Length is < 1 or > 1024)
            throw new ArgumentException("Invalid credential identifier or size.");
        SaveWindows(metadata, material);
    }
    [SupportedOSPlatform("windows")]
    private void SaveWindows(SecurityCredential metadata, ReadOnlySpan<byte> material)
    {
        var clear = material.ToArray();
        try
        {
            var encrypted = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
            var path = Path.Combine(store.Root, "credentials"); Directory.CreateDirectory(path);
            // Never overwrite an existing credential implicitly.
            using var stream = new FileStream(Path.Combine(path, metadata.Id + ".dpapi"), FileMode.CreateNew);
            stream.Write(encrypted);
            store.Save("credential", metadata.Id, metadata); store.Audit("credential.stored", metadata.Id);
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public byte[] Reveal(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _) || store.Get<SecurityCredential>("credential", id) is null)
            throw new KeyNotFoundException("Credential metadata not found.");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Reveal requires the Windows user who encrypted the credential.");
        return RevealWindows(id);
    }
    [SupportedOSPlatform("windows")]
    private byte[] RevealWindows(string id)
    {
        var value = ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(store.Root, "credentials", id + ".dpapi")), Entropy, DataProtectionScope.CurrentUser);
        store.Audit("credential.revealed", id); return value;
    }
}
