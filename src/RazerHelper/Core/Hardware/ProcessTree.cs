using System.Runtime.InteropServices;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Hardware;

/// <summary>Who started which process, from one snapshot of the process table. Read-only.</summary>
internal static class ProcessTree
{
    private const uint SnapshotProcesses = 0x00000002;
    private static readonly IntPtr InvalidHandle = new(-1);

    /// <summary>Maps each running process id to the id of the process that started it. Empty if Windows will not say.</summary>
    public static IReadOnlyDictionary<int, int> ReadParentIds()
    {
        var parents = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);

        if (snapshot == InvalidHandle)
        {
            AppLog.Error($"Could not read the process table (error {Marshal.GetLastWin32Error()}).");
            return parents;
        }

        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };

            for (var more = Process32First(snapshot, ref entry); more; more = Process32Next(snapshot, ref entry))
                parents[(int)entry.ProcessId] = (int)entry.ParentProcessId;
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return parents;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public UIntPtr DefaultHeapId;
        public uint ModuleId;
        public uint ThreadCount;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
