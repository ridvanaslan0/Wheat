using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace Wheat;

/// <summary>
/// "Start with Windows" for an app that needs administrator rights.
/// The classic Run registry key cannot launch elevated apps silently, so a Task Scheduler
/// logon task with the highest run level is used instead: no UAC prompt at every login.
/// </summary>
public static class StartupManager
{
    const string TaskName = "Wheat";

    public static bool IsEnabled() => Schtasks($"/Query /TN \"{TaskName}\"").ExitCode == 0;

    public static void Set(bool enabled)
    {
        if (enabled) Enable();
        else Disable();
    }

    static void Enable()
    {
        var exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the path of the running exe.");
        var user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        var command = SecurityElement.Escape("\"" + exe + "\"");
        var workDir = SecurityElement.Escape(Path.GetDirectoryName(exe) ?? "");

        var xml =
$@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Starts {AppInfo.Name} at logon with administrator rights.</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{user}</UserId>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{user}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
    <Enabled>true</Enabled>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{command}</Command>
      <WorkingDirectory>{workDir}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";

        var tmp = Path.Combine(Path.GetTempPath(), "Wheat-task-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            File.WriteAllText(tmp, xml, Encoding.Unicode); // UTF-16 with BOM, as declared in the XML
            var (code, output) = Schtasks($"/Create /TN \"{TaskName}\" /XML \"{tmp}\" /F");
            if (code != 0)
                throw new InvalidOperationException("schtasks failed (exit code " + code + "): " + output.Trim());
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    static void Disable()
    {
        if (!IsEnabled()) return;
        var (code, output) = Schtasks($"/Delete /TN \"{TaskName}\" /F");
        if (code != 0)
            throw new InvalidOperationException("schtasks failed (exit code " + code + "): " + output.Trim());
    }

    static (int ExitCode, string Output) Schtasks(string args)
    {
        var psi = new ProcessStartInfo("schtasks.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output);
    }
}
