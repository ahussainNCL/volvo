using System.Runtime.InteropServices;
using System.Text;

namespace Nordlink.HardwareWorker;

// Native code is loaded only in this isolated executable. No PassThruConnect or vehicle messages
// are issued without known bus/addressing/session definitions.
public sealed class J2534Device : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint OpenDelegate(IntPtr name, out uint id);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint CloseDelegate(uint id);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)] private delegate uint VersionDelegate(uint id, StringBuilder firmware, StringBuilder dll, StringBuilder api);
    private IntPtr library;
    private uint deviceId;
    private bool opened;
    private CloseDelegate? close;
    public (string Firmware, string Driver, string Api) Open(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Official TOPDON J2534 libraries require Windows.");
        if (opened) throw new InvalidOperationException("Interface already open.");
        var registration = DriverDiscovery.Discover().FirstOrDefault(d => d.Exists && d.Problem is null && string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Select an existing inspected official TOPDON registration.");
        DriverDiscovery.RequireArchitecture(registration.Path);
        // Resolve DLL dependencies beside the official library and from System32, never the CWD.
        library = NativeLibrary.Load(registration.Path, typeof(J2534Device).Assembly,
            DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.System32);
        try
        {
            var open = Marshal.GetDelegateForFunctionPointer<OpenDelegate>(NativeLibrary.GetExport(library, "PassThruOpen"));
            close = Marshal.GetDelegateForFunctionPointer<CloseDelegate>(NativeLibrary.GetExport(library, "PassThruClose"));
            var version = Marshal.GetDelegateForFunctionPointer<VersionDelegate>(NativeLibrary.GetExport(library, "PassThruReadVersion"));
            Check(open(IntPtr.Zero, out deviceId)); opened = true;
            var firmware = new StringBuilder(80); var dll = new StringBuilder(80); var api = new StringBuilder(80);
            Check(version(deviceId, firmware, dll, api));
            return (firmware.ToString(), dll.ToString(), api.ToString());
        }
        catch { Dispose(); throw; }
    }
    private static void Check(uint status) { if (status != 0) throw new IOException($"J2534 returned status 0x{status:X8}; check interface power, USB and official driver."); }
    public void Dispose()
    {
        try { if (opened) { opened = false; Check(close!(deviceId)); } }
        finally { if (library != IntPtr.Zero) { NativeLibrary.Free(library); library = IntPtr.Zero; } }
    }
}
