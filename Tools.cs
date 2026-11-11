using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Wheat;

/// <summary>
/// Tools the model can use on this PC. By default nothing asks for confirmation (the user gave one-time
/// permission at first launch and the app runs elevated); turn on "Ask for confirmation before every action"
/// in Settings to get a prompt before each action.
/// </summary>
public static class Tools
{
    public static event Action<string>? Note;

    const int CommandTimeoutSeconds = 60;

    static bool Confirm(string what)
    {
        if (!AppSettings.AskBeforeActions) return true;

        return MessageBox.Show(
            // Giving the dialog an owner keeps it from hiding behind the chat window
            Application.OpenForms.OfType<ChatForm>().FirstOrDefault(f => f.Visible),
            "The assistant wants to do this:\n\n" + what + "\n\nAllow it?",
            "Confirmation required",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    static JsonObject Str(string desc) => new() { ["type"] = "string", ["description"] = desc };

    static JsonObject Def(string name, string desc, JsonObject props, params string[] required)
    {
        var req = new JsonArray(required.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray());
        return new JsonObject
        {
            ["name"] = name,
            ["description"] = desc,
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = props,
                ["required"] = req
            }
        };
    }

    /// <summary>Tool definitions sent to the API (Anthropic format).</summary>
    public static JsonArray Definitions() => new()
    {
        Def("open_url", "Opens a web address in the default browser (http/https only).",
            new JsonObject { ["url"] = Str("The address to open") }, "url"),
        Def("open_program", "Opens a program or file with Windows (e.g. notepad, calc, or a file path).",
            new JsonObject { ["target"] = Str("Program name or file path") }, "target"),
        Def("list_directory", "Lists the contents of a folder (up to 200 entries).",
            new JsonObject { ["path"] = Str("Folder path, e.g. C:\\Users\\name\\Desktop or %USERPROFILE%\\Downloads") }, "path"),
        Def("read_text_file", "Reads a text file (first 20000 characters).",
            new JsonObject { ["path"] = Str("File path") }, "path"),
        Def("write_text_file", "Creates a text file or overwrites an existing one.",
            new JsonObject { ["path"] = Str("File path"), ["content"] = Str("Text to write") }, "path", "content"),
        Def("run_command", $"Runs a command through cmd.exe and returns its output ({CommandTimeoutSeconds} second timeout).",
            new JsonObject { ["command"] = Str("The command to run") }, "command"),
        Def("run_powershell", $"Runs a PowerShell script and returns its output ({CommandTimeoutSeconds} second timeout).",
            new JsonObject { ["script"] = Str("The PowerShell script to run") }, "script"),
    };

    static string S(JsonObject i, string key) =>
        i[key]?.GetValue<string>() ?? throw new ArgumentException($"Missing parameter '{key}'");

    static string Trunc(string s, int max = 8000) =>
        s.Length <= max ? s : s[..max] + "\n...(truncated)";

    public static async Task<string> RunAsync(string name, JsonObject input)
    {
        Note?.Invoke($"{name} {input.ToJsonString()}");

        switch (name)
        {
            case "open_url":
            {
                var url = S(input, "url");
                if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != "http" && u.Scheme != "https"))
                    return "Error: only http/https addresses can be opened.";
                if (!Confirm("Open in browser:\n" + url)) return "The user declined.";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return "Opened.";
            }

            case "open_program":
            {
                var target = Environment.ExpandEnvironmentVariables(S(input, "target"));
                if (!Confirm("Open:\n" + target)) return "The user declined.";
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                return "Opened.";
            }

            case "list_directory":
            {
                var path = Environment.ExpandEnvironmentVariables(S(input, "path"));
                if (!Directory.Exists(path)) return "Error: folder not found.";
                var sb = new StringBuilder();
                foreach (var e in Directory.EnumerateFileSystemEntries(path).Take(200))
                    sb.AppendLine((Directory.Exists(e) ? "[D] " : "    ") + Path.GetFileName(e));
                return sb.Length == 0 ? "(empty folder)" : sb.ToString();
            }

            case "read_text_file":
            {
                var path = Environment.ExpandEnvironmentVariables(S(input, "path"));
                if (!File.Exists(path)) return "Error: file not found.";
                if (!Confirm("Read this file and send its content to the AI provider:\n" + path)) return "The user declined.";
                return Trunc(await File.ReadAllTextAsync(path), 20000);
            }

            case "write_text_file":
            {
                var path = Environment.ExpandEnvironmentVariables(S(input, "path"));
                var content = S(input, "content");
                var exists = File.Exists(path) ? "\n(WARNING: the file already exists and will be overwritten!)" : "";
                if (!Confirm($"Write file:\n{path}{exists}\n\nFirst lines:\n{Trunc(content, 300)}"))
                    return "The user declined.";
                await File.WriteAllTextAsync(path, content);
                return "Written.";
            }

            case "run_command":
            {
                var cmd = S(input, "command");
                if (!Confirm("Run command:\n\n" + cmd)) return "The user declined.";
                var psi = new ProcessStartInfo("cmd.exe", "/c " + cmd)
                {
                    StandardOutputEncoding = OemEncoding(),
                    StandardErrorEncoding = OemEncoding()
                };
                return await Exec(psi);
            }

            case "run_powershell":
            {
                var script = S(input, "script");
                if (!Confirm("Run PowerShell:\n\n" + script)) return "The user declined.";
                // -EncodedCommand avoids every quoting problem; output is forced to UTF-8.
                var wrapped = "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " + script;
                var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapped));
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded)
                {
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                return await Exec(psi);
            }

            default:
                return "Error: unknown tool: " + name;
        }
    }

    static Encoding OemEncoding()
    {
        try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
        catch { return Encoding.UTF8; }
    }

    static async Task<string> Exec(ProcessStartInfo psi)
    {
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        using var p = Process.Start(psi)!;
        p.StandardInput.Close(); // commands that wait for input (e.g. "pause") end instead of hanging

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CommandTimeoutSeconds));
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        try
        {
            await p.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            return $"Timed out after {CommandTimeoutSeconds} seconds, the command was stopped.";
        }
        return Trunc($"Exit code: {p.ExitCode}\n{await outTask}{await errTask}");
    }
}
