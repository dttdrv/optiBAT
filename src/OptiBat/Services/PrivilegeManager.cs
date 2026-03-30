using OptiBat.Native;

namespace OptiBat.Services;

/// <summary>
/// Enables required privileges for optimization operations.
/// </summary>
public static class PrivilegeManager
{
    public static void EnableAllRequired()
    {
        NativeMethods.EnablePrivilege(NativeMethods.SE_DEBUG_NAME);
        NativeMethods.EnablePrivilege(NativeMethods.SE_INCREASE_QUOTA_NAME);
        NativeMethods.EnablePrivilege(NativeMethods.SE_SHUTDOWN_NAME);
    }
}
