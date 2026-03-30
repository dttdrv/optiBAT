using System.Runtime.InteropServices;

namespace OptiBat.Native;

/// <summary>
/// P/Invoke declarations for Windows power management, process control,
/// service management, and device APIs. Organized by DLL.
/// </summary>
internal static partial class NativeMethods
{
    // ── Process access rights ────────────────────────────────────────
    internal const uint PROCESS_QUERY_INFORMATION = 0x0400;
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    internal const uint PROCESS_SET_INFORMATION = 0x0200;
    internal const uint PROCESS_ALL_ACCESS = 0x1FFFFF;

    // ── Token rights ─────────────────────────────────────────────────
    internal const uint TOKEN_QUERY = 0x0008;
    internal const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    internal const uint SE_PRIVILEGE_ENABLED = 0x00000002;

    // ── Privilege names ──────────────────────────────────────────────
    internal const string SE_DEBUG_NAME = "SeDebugPrivilege";
    internal const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
    internal const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";

    // ── Process information classes ──────────────────────────────────
    internal const int ProcessPowerThrottling = 4;
    internal const int ProcessTimerResolutionControl = 0x22; // Undocumented: disable timer boost

    // ── Power throttling flags ───────────────────────────────────────
    internal const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    internal const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
    internal const uint PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4;

    // ── Memory priority ──────────────────────────────────────────────
    internal const int ProcessMemoryPriority = 0;
    internal const uint MEMORY_PRIORITY_VERY_LOW = 1;
    internal const uint MEMORY_PRIORITY_LOW = 2;
    internal const uint MEMORY_PRIORITY_NORMAL = 5;

    // ── Service control constants ────────────────────────────────────
    internal const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
    internal const uint SERVICE_QUERY_CONFIG = 0x0001;
    internal const uint SERVICE_CHANGE_CONFIG = 0x0002;
    internal const uint SERVICE_QUERY_STATUS = 0x0004;
    internal const uint SERVICE_START = 0x0010;
    internal const uint SERVICE_STOP = 0x0020;
    internal const uint SERVICE_ALL_ACCESS = 0xF01FF;
    internal const uint SERVICE_CONTROL_STOP = 0x00000001;
    internal const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;

    // Service start types
    internal const uint SERVICE_AUTO_START = 0x00000002;
    internal const uint SERVICE_DEMAND_START = 0x00000003;
    internal const uint SERVICE_DISABLED = 0x00000004;

    // Service states
    internal const uint SERVICE_STOPPED = 0x00000001;
    internal const uint SERVICE_RUNNING = 0x00000004;

    // ── Device setup constants ───────────────────────────────────────
    internal const uint DIGCF_PRESENT = 0x00000002;
    internal const uint DIGCF_ALLCLASSES = 0x00000004;
    internal const uint SPDRP_HARDWAREID = 0x00000001;

    // ── Power setting GUIDs ──────────────────────────────────────────
    // Processor subgroup
    internal static readonly Guid GUID_PROCESSOR_SETTINGS_SUBGROUP =
        new("54533251-82be-4824-96c1-47b60b740d00");
    // Processor min state
    internal static readonly Guid GUID_PROCESSOR_THROTTLE_MINIMUM =
        new("893dee8e-2bef-41e0-89c6-b55d0929964c");
    // Processor max state
    internal static readonly Guid GUID_PROCESSOR_THROTTLE_MAXIMUM =
        new("bc5038f7-23e0-4960-96da-33abaf5935ec");
    // Core parking min cores %
    internal static readonly Guid GUID_PROCESSOR_PARKING_CORE_THRESHOLD =
        new("0cc5b647-c1df-4637-891a-dec35c318583");
    // Disk subgroup
    internal static readonly Guid GUID_DISK_SUBGROUP =
        new("0012ee47-9041-4b5d-9b77-535fba8b1442");
    // Disk idle timeout
    internal static readonly Guid GUID_DISK_IDLE_TIMEOUT =
        new("58e39ba8-b8e6-4ef6-90d0-89ae32b258d6");
    // AHCI link power management
    internal static readonly Guid GUID_DISK_AHCI_LINK_POWER =
        new("0b2d69d7-a2a1-449c-9680-f91c70521c60");

    // ── Structs ──────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privileges;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_BATTERY_STATE
    {
        [MarshalAs(UnmanagedType.U1)] public bool AcOnLine;      // offset 0
        [MarshalAs(UnmanagedType.U1)] public bool BatteryPresent; // offset 1
        [MarshalAs(UnmanagedType.U1)] public bool Charging;       // offset 2
        [MarshalAs(UnmanagedType.U1)] public bool Discharging;    // offset 3
        public byte Spare1;                                        // offset 4
        public byte Spare2;                                        // offset 5
        public byte Spare3;                                        // offset 6
        public byte Tag;                                           // offset 7
        public uint MaxCapacity;                                   // offset 8  (mWh)
        public uint RemainingCapacity;                             // offset 12 (mWh)
        public int Rate;                                           // offset 16 (mW, negative = discharging)
        public uint EstimatedTime;                                 // offset 20 (seconds, 0xFFFFFFFF = unknown)
        public uint DefaultAlert1;                                 // offset 24
        public uint DefaultAlert2;                                 // offset 28
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct QUERY_SERVICE_CONFIG
    {
        public uint dwServiceType;
        public uint dwStartType;
        public uint dwErrorControl;
        public string lpBinaryPathName;
        public string lpLoadOrderGroup;
        public uint dwTagId;
        public string lpDependencies;
        public string lpServiceStartName;
        public string lpDisplayName;
    }

    // ── kernel32.dll ─────────────────────────────────────────────────

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(IntPtr hObject);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetProcessInformation(
        IntPtr hProcess,
        int ProcessInformationClass,
        IntPtr ProcessInformation,
        uint ProcessInformationSize);

    [LibraryImport("kernel32.dll")]
    internal static partial IntPtr GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint GetCurrentProcessId();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;        // 0=offline, 1=online, 255=unknown
        public byte BatteryFlag;
        public byte BatteryLifePercent;  // 0-100 or 255
        public byte SystemStatusFlag;    // 0=no saver, 1=battery saver
        public uint BatteryLifeTime;     // seconds or 0xFFFFFFFF
        public uint BatteryFullLifeTime;
    }

    // ── advapi32.dll ─────────────────────────────────────────────────

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(
        IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AdjustTokenPrivileges(
        IntPtr TokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        in TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool LookupPrivilegeValueW(
        string? lpSystemName, string lpName, out LUID lpLuid);

    // Service control manager
    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr OpenSCManagerW(string? lpMachineName, string? lpDatabaseName, uint dwDesiredAccess);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr OpenServiceW(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool QueryServiceStatus(IntPtr hService, out SERVICE_STATUS lpServiceStatus);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool QueryServiceConfigW(
        IntPtr hService, IntPtr lpServiceConfig, uint cbBufSize, out uint pcbBytesNeeded);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ChangeServiceConfigW(
        IntPtr hService, uint dwServiceType, uint dwStartType, uint dwErrorControl,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpBinaryPathName,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpLoadOrderGroup,
        IntPtr lpdwTagId,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpDependencies,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpServiceStartName,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpPassword,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpDisplayName);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ControlService(IntPtr hService, uint dwControl, out SERVICE_STATUS lpServiceStatus);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool StartServiceW(IntPtr hService, uint dwNumServiceArgs, IntPtr lpServiceArgVectors);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseServiceHandle(IntPtr hSCObject);

    // ── powrprof.dll ─────────────────────────────────────────────────

    [LibraryImport("powrprof.dll")]
    internal static partial uint CallNtPowerInformation(
        int InformationLevel, IntPtr InputBuffer, uint InputBufferLength,
        IntPtr OutputBuffer, uint OutputBufferLength);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerReadDCValueIndex(
        IntPtr RootPowerKey, in Guid SchemeGuid, in Guid SubGroupOfPowerSettingsGuid,
        in Guid PowerSettingGuid, out uint AcValueIndex);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerWriteDCValueIndex(
        IntPtr RootPowerKey, in Guid SchemeGuid, in Guid SubGroupOfPowerSettingsGuid,
        in Guid PowerSettingGuid, uint DcValueIndex);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerReadACValueIndex(
        IntPtr RootPowerKey, in Guid SchemeGuid, in Guid SubGroupOfPowerSettingsGuid,
        in Guid PowerSettingGuid, out uint AcValueIndex);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerSetActiveScheme(IntPtr UserRootPowerKey, in Guid SchemeGuid);

    // SystemBatteryState = 5
    internal const int SystemBatteryState = 5;

    // ── ntdll.dll (undocumented) ─────────────────────────────────────

    [LibraryImport("ntdll.dll")]
    internal static partial int NtSetInformationProcess(
        IntPtr ProcessHandle, int ProcessInformationClass,
        IntPtr ProcessInformation, uint ProcessInformationLength);

    [LibraryImport("ntdll.dll")]
    internal static partial int NtQueryTimerResolution(
        out uint MinimumResolution, out uint MaximumResolution, out uint CurrentResolution);

    [LibraryImport("ntdll.dll")]
    internal static partial int NtSetTimerResolution(
        uint DesiredResolution, [MarshalAs(UnmanagedType.Bool)] bool SetResolution, out uint CurrentResolution);

    // ── user32.dll ───────────────────────────────────────────────────

    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr FindWindowW(string? lpClassName, string lpWindowName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    internal const int SW_RESTORE = 9;

    // ── Helper methods ───────────────────────────────────────────────

    /// <summary>
    /// Enable EcoQoS (efficiency mode) on a process handle.
    /// </summary>
    internal static unsafe bool SetProcessEcoQoS(IntPtr hProcess, bool enable)
    {
        var state = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = enable ? PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0
        };
        var ptr = (IntPtr)(&state);
        return SetProcessInformation(hProcess, ProcessPowerThrottling, ptr,
            (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
    }

    /// <summary>
    /// Set timer resolution ignore flag on a process (Windows 11+).
    /// </summary>
    internal static unsafe bool SetProcessTimerResolutionIgnore(IntPtr hProcess, bool ignore)
    {
        var state = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION,
            StateMask = ignore ? PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION : 0
        };
        var ptr = (IntPtr)(&state);
        return SetProcessInformation(hProcess, ProcessPowerThrottling, ptr,
            (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
    }

    /// <summary>
    /// Enable a named privilege on the current process token.
    /// </summary>
    internal static bool EnablePrivilege(string privilegeName)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES, out var token))
            return false;

        try
        {
            if (!LookupPrivilegeValueW(null, privilegeName, out var luid))
                return false;

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED }
            };

            return AdjustTokenPrivileges(token, false, tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            CloseHandle(token);
        }
    }

    /// <summary>
    /// Get the PID of the foreground window's owning process.
    /// </summary>
    internal static uint GetForegroundProcessId()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    /// <summary>
    /// Read the current active power scheme GUID.
    /// </summary>
    internal static Guid GetActiveScheme()
    {
        if (PowerGetActiveScheme(IntPtr.Zero, out var ptr) != 0)
            return Guid.Empty;
        try
        {
            return Marshal.PtrToStructure<Guid>(ptr);
        }
        finally
        {
            // PowerGetActiveScheme allocates via LocalAlloc.
            // FreeCoTaskMem calls LocalFree on Windows — correct deallocator.
            Marshal.FreeCoTaskMem(ptr);
        }
    }

    /// <summary>
    /// Read a DC (battery) power setting value.
    /// </summary>
    internal static uint? ReadDCValue(Guid scheme, Guid subgroup, Guid setting)
    {
        var result = PowerReadDCValueIndex(IntPtr.Zero, scheme, subgroup, setting, out var value);
        return result == 0 ? value : null;
    }

    /// <summary>
    /// Write a DC (battery) power setting value.
    /// </summary>
    internal static bool WriteDCValue(Guid scheme, Guid subgroup, Guid setting, uint value)
    {
        return PowerWriteDCValueIndex(IntPtr.Zero, scheme, subgroup, setting, value) == 0;
    }

    /// <summary>
    /// Query battery state via CallNtPowerInformation.
    /// </summary>
    internal static SYSTEM_BATTERY_STATE? GetBatteryState()
    {
        var size = (uint)Marshal.SizeOf<SYSTEM_BATTERY_STATE>();
        var ptr = Marshal.AllocHGlobal((int)size);
        try
        {
            var status = CallNtPowerInformation(SystemBatteryState, IntPtr.Zero, 0, ptr, size);
            if (status != 0) return null;
            return Marshal.PtrToStructure<SYSTEM_BATTERY_STATE>(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
