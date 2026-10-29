using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

namespace Nordlink.HardwareWorker;

// Extract signer metadata from the PE WIN_CERTIFICATE. This does not claim chain/file trust.
// Signature trust must be checked with the official installer / Windows Authenticode tooling.
public static class AuthenticodeMetadata
{
    public static string? Publisher(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var directory = pe.PEHeaders.PEHeader?.CertificateTableDirectory;
            if (directory is null || directory.Value.Size < 8) return null;
            var offset = directory.Value.RelativeVirtualAddress; // This directory uses a file offset, not an RVA.
            if (offset < 0 || (long)offset + directory.Value.Size > stream.Length) return null;
            stream.Position = offset; var header = new byte[8]; stream.ReadExactly(header);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
            if (length < 8 || length > 4 * 1024 * 1024 || length > directory.Value.Size ||
                BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6)) != 2) return null;
            var data = new byte[length - 8]; stream.ReadExactly(data);
            var signed = new SignedCms(); signed.Decode(data);
            return signed.SignerInfos.Count > 0 ? signed.SignerInfos[0].Certificate?.Subject : null;
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or CryptographicException or ArgumentException)
        { return null; }
    }
}
