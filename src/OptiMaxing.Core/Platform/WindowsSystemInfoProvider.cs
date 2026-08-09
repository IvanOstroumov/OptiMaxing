using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Platform;

public sealed class WindowsSystemInfoProvider : ISystemInfoProvider
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string ProcessorKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";

    public string OsDescription()
    {
        // Registry values here are always plain English/numeric regardless of Windows display
        // language (unlike text parsed from command output elsewhere in this codebase), so this
        // is safe to read directly rather than via a locale-independent enum/exit-code seam.
        using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey);
        var productName = key?.GetValue("ProductName") as string ?? "Windows";
        var displayVersion = key?.GetValue("DisplayVersion") as string;
        var build = key?.GetValue("CurrentBuildNumber") as string;

        return string.Join(" ", new[] { productName, displayVersion, build is null ? null : $"(build {build})" }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public string ProcessorName()
    {
        using var key = Registry.LocalMachine.OpenSubKey(ProcessorKey);
        return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "Неизвестный процессор";
    }

    public TimeSpan Uptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public MemoryInfo GetMemoryInfo()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref status)
            ? new MemoryInfo(status.ullTotalPhys, status.ullAvailPhys)
            : new MemoryInfo(0, 0);
    }

    public IReadOnlyList<DiskInfo> GetFixedDrives() =>
        DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => new DiskInfo(d.Name, d.AvailableFreeSpace, d.TotalSize))
            .ToList();

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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
