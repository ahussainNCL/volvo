using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Win32;
using Nordlink.Contracts;

namespace Nordlink.HardwareWorker;

public static class DriverDiscovery
{
    public static string Architecture(string path)
    {
        using var stream = File.OpenRead(path); using var pe = new PEReader(stream);
        return pe.PEHeaders.CoffHeader.Machine switch
        {
            Machine.I386 => "x86", Machine.Amd64 => "x64", _ => "unsupported"
        };
    }
    public static DriverRegistration[] Discover()
    {
        if (!OperatingSystem.IsWindows()) return [];
        return DiscoverWindows();
    }
    [SupportedOSPlatform("windows")]
    private static DriverRegistration[] DiscoverWindows()
    {
        var result = new List<DriverRegistration>();
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var root = baseKey.OpenSubKey(@"SOFTWARE\PassThruSupport.04.04");
                if (root is null) continue;
                foreach (var name in root.GetSubKeyNames())
                {
                    using var key = root.OpenSubKey(name);
                    var vendor = key?.GetValue("Vendor")?.ToString() ?? "";
                    var display = key?.GetValue("Name")?.ToString() ?? name;
                    // Only official TOPDON registrations are candidates for this application.
                    if (!(name + display + vendor).Contains("TOPDON", StringComparison.OrdinalIgnoreCase)) continue;
                    var path = key?.GetValue("FunctionLibrary")?.ToString() ?? "";
                    var exists = Path.IsPathFullyQualified(path) && File.Exists(path);
                    string arch = "unknown"; string? version = null, publisher = null;
                    string? problem = exists ? null : "Registered FunctionLibrary is missing or not an absolute existing path. Repair the official installer.";
                    if (exists)
                    {
                        try
                        {
                            arch = Architecture(path); version = FileVersionInfo.GetVersionInfo(path).FileVersion;
                            publisher = AuthenticodeMetadata.Publisher(path);
                            if (arch == "unsupported") problem = "Unsupported PE architecture.";
                        }
                        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
                        { problem = "Registered file cannot be inspected as a PE library."; }
                    }
                    result.Add(new(display, view.ToString(), path, exists, arch, version, publisher, problem));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            { result.Add(new("Registry inspection failed", view.ToString(), "", false, "unknown", null, null, "Registry view could not be inspected; driver health is unknown.")); }
        }
        return result.ToArray();
    }
    public static void RequireArchitecture(string path)
    {
        var expected = RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X86 ? "x86" : "x64";
        if (Architecture(path) != expected) throw new BadImageFormatException("Worker and J2534 DLL architectures do not match.");
    }
}
