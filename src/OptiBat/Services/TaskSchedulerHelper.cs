using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace OptiBat.Services;

/// <summary>
/// Manages a Windows Task Scheduler task for silent elevation.
/// Creates a task that runs the app as admin on logon, bypassing UAC.
/// Same pattern as optiRAM.
/// </summary>
public static class TaskSchedulerHelper
{
    private const string TASK_NAME = "optiBAT";

    public static bool TaskExists()
    {
        try
        {
            var result = RunSchtasks($"/Query /TN \"{TASK_NAME}\" /FO LIST");
            return result.ExitCode == 0;
        }
        catch { return false; }
    }

    public static bool RunTask()
    {
        try
        {
            var result = RunSchtasks($"/Run /TN \"{TASK_NAME}\"");
            return result.ExitCode == 0;
        }
        catch { return false; }
    }

    public static bool CreateTask(bool startAtLogon = false)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath)) return false;

        var userName = WindowsIdentity.GetCurrent().Name;
        var xml = BuildTaskXml(exePath, userName, startAtLogon);

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, xml);
            var result = RunSchtasks($"/Create /TN \"{TASK_NAME}\" /XML \"{tempFile}\" /F");
            return result.ExitCode == 0;
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    public static bool DeleteTask()
    {
        try
        {
            var result = RunSchtasks($"/Delete /TN \"{TASK_NAME}\" /F");
            return result.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string BuildTaskXml(string exePath, string userName, bool startAtLogon)
    {
        var triggerXml = startAtLogon ? $@"
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{EscapeXml(userName)}</UserId>
    </LogonTrigger>
  </Triggers>" : "  <Triggers />";

        return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>optiBAT — Battery Life Optimizer</Description>
    <URI>\{TASK_NAME}</URI>
  </RegistrationInfo>
{triggerXml}
  <Principals>
    <Principal id=""Author"">
      <UserId>{EscapeXml(userName)}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>5</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{EscapeXml(exePath)}</Command>
    </Exec>
  </Actions>
</Task>";
    }

    private static string EscapeXml(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&apos;");

    private static ProcessResult RunSchtasks(string arguments)
    {
        var psi = new ProcessStartInfo("schtasks.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(10000);

        return new ProcessResult(proc.ExitCode, stdout, stderr);
    }

    private record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
