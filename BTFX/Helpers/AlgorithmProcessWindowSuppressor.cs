using System.Runtime.InteropServices;
using System.Text;

namespace BTFX.Helpers;

internal static class AlgorithmProcessWindowSuppressor
{
    private const uint SnapshotProcesses = 0x00000002;
    private const uint CloseWindowMessage = 0x0010;
    private const int HideWindow = 0;
    private static readonly nint InvalidHandleValue = new(-1);

    internal static async Task SuppressWindowsAsync(
        int rootProcessId,
        Action<string>? onWindowSuppressed,
        CancellationToken cancellationToken)
    {
        var suppressedWindows = new HashSet<nint>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var parentByProcessId = GetProcessSnapshot();
                var processTree = CollectProcessTree(rootProcessId, parentByProcessId);
                SuppressVisibleWindows(processTree, suppressedWindows, onWindowSuppressed);
            }
            catch
            {
                // Window suppression is best-effort and must never interrupt analysis.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
    }

    internal static IReadOnlySet<int> CollectProcessTree(
        int rootProcessId,
        IReadOnlyDictionary<int, int> parentByProcessId)
    {
        var processTree = new HashSet<int> { rootProcessId };
        var foundDescendant = true;

        while (foundDescendant)
        {
            foundDescendant = false;

            foreach (var (processId, parentProcessId) in parentByProcessId)
            {
                if (!processTree.Contains(processId) &&
                    processTree.Contains(parentProcessId))
                {
                    processTree.Add(processId);
                    foundDescendant = true;
                }
            }
        }

        return processTree;
    }

    private static Dictionary<int, int> GetProcessSnapshot()
    {
        var parentByProcessId = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
        if (snapshot == InvalidHandleValue)
        {
            return parentByProcessId;
        }

        try
        {
            var entry = new ProcessEntry32
            {
                Size = (uint)Marshal.SizeOf<ProcessEntry32>()
            };

            if (!Process32First(snapshot, ref entry))
            {
                return parentByProcessId;
            }

            do
            {
                parentByProcessId[(int)entry.ProcessId] = (int)entry.ParentProcessId;
            }
            while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return parentByProcessId;
    }

    private static void SuppressVisibleWindows(
        IReadOnlySet<int> processTree,
        HashSet<nint> suppressedWindows,
        Action<string>? onWindowSuppressed)
    {
        EnumWindows((windowHandle, _) =>
        {
            if (!IsWindowVisible(windowHandle))
            {
                return true;
            }

            GetWindowThreadProcessId(windowHandle, out var processId);
            if (!processTree.Contains((int)processId))
            {
                return true;
            }

            ShowWindowAsync(windowHandle, HideWindow);
            PostMessage(windowHandle, CloseWindowMessage, nint.Zero, nint.Zero);

            if (suppressedWindows.Add(windowHandle))
            {
                onWindowSuppressed?.Invoke(
                    $"已关闭算法进程弹窗: PID={processId}, 标题={GetWindowTitle(windowHandle)}");
            }

            return true;
        }, nint.Zero);
    }

    private static string GetWindowTitle(nint windowHandle)
    {
        var length = GetWindowTextLength(windowHandle);
        if (length <= 0)
        {
            return "(无标题)";
        }

        var title = new StringBuilder(length + 1);
        return GetWindowText(windowHandle, title, title.Capacity) > 0
            ? title.ToString()
            : "(无标题)";
    }

    private delegate bool EnumWindowsCallback(nint windowHandle, nint parameter);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(nint windowHandle, int command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(
        nint windowHandle,
        uint message,
        nint wordParameter,
        nint longParameter);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(nint windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(
        nint windowHandle,
        StringBuilder text,
        int maximumCount);
}
