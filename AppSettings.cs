using System.Security.Cryptography;
using System.Text;

namespace Wheat;

/// <summary>Default info for one AI provider.</summary>
/// <param name="Anthropic">true: Anthropic Messages protocol, false: OpenAI-compatible chat/completions</param>
public record Provider(
    string Id,
    string Name,
    bool Anthropic,
    string DefaultUrl,
    string DefaultModel,
    string KeySite,
    bool KeyOptional = false);

/// <summary>Selected provider, model/URL settings, behavior flags, and per-provider API keys encrypted with DPAPI.</summary>
public static class AppSettings
{
    // Model names and URLs can be changed in the Settings window.
    public static readonly Provider[] Providers =
    {
        new("anthropic", "Claude (Anthropic)", true,
            "https://api.anthropic.com/v1/messages", "claude-sonnet-5-5", "console.anthropic.com"),
        new("openai", "ChatGPT (OpenAI)", false,
            "https://api.openai.com/v1/chat/completions", "gpt-6-luna", "platform.openai.com/api-keys"),
        new("gemini", "Gemini (Google)", false,
            "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", "gemini-3.8-flash", "aistudio.google.com/apikey"),
        new("deepseek", "DeepSeek", false,
            "https://api.deepseek.com/chat/completions", "deepseek-flash", "platform.deepseek.com"),
        new("kimi", "Kimi (Moonshot)", false,
            "https://api.moonshot.ai/v1/chat/completions", "kimi-k3.1-pro", "platform.kimi.ai"),
        new("groq", "Groq (fast, free tier)", false,
            "https://api.groq.com/openai/v1/chat/completions", "qwen/qwen3.8-27b", "console.groq.com/keys"),
        new("custom", "Custom (OpenAI-compatible / local)", false,
            "http://localhost:11434/v1/chat/completions", "llama3.1", "Works with OpenRouter, Ollama, LM Studio, etc.", true),
    };

    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wheat");
    static readonly string CfgFile = Path.Combine(Dir, "config.ini");
    static readonly Dictionary<string, string> Cfg = new();

    static AppSettings()
    {
        try
        {
            if (!File.Exists(CfgFile)) return;
            foreach (var line in File.ReadAllLines(CfgFile))
            {
                var i = line.IndexOf('=');
                if (i > 0) Cfg[line[..i].Trim()] = line[(i + 1)..].Trim();
            }
        }
        catch { /* keep defaults */ }
    }

    static string? Get(string key) => Cfg.TryGetValue(key, out var v) && v.Length > 0 ? v : null;

    static void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllLines(CfgFile, Cfg.Select(kv => kv.Key + "=" + kv.Value));
    }

    // ---- Provider settings ------------------------------------------------

    public static Provider Current => Providers.FirstOrDefault(p => p.Id == Get("provider")) ?? Providers[0];

    public static string Model(Provider p) => Get("model." + p.Id) ?? p.DefaultModel;
    public static string Url(Provider p) => Get("url." + p.Id) ?? p.DefaultUrl;

    /// <summary>Ready to talk to the selected provider? (key present, or not required)</summary>
    public static bool Ready => Current.KeyOptional || LoadKey() != null;

    /// <summary>Reasoning effort: "" (default), "low" or "none". Only used with OpenAI-compatible providers.</summary>
    public static string Effort(Provider p) => Get("effort." + p.Id) ?? "";

    public static void Configure(Provider p, string model, string url, string effort = "")
    {
        Cfg["provider"] = p.Id;
        Cfg["model." + p.Id] = model;
        Cfg["url." + p.Id] = url;
        Cfg["effort." + p.Id] = effort;
        Save();
    }

    // ---- Behavior flags ---------------------------------------------------

    /// <summary>True once the user accepted the one-time permission dialog.</summary>
    public static bool Consented => Get("consent") == "1";

    public static void SetConsented()
    {
        Cfg["consent"] = "1";
        Save();
    }

    /// <summary>When true, every action asks for confirmation (off by default).</summary>
    public static bool AskBeforeActions => Get("ask") == "1";

    public static void SetAskBeforeActions(bool value)
    {
        Cfg["ask"] = value ? "1" : "0";
        Save();
    }

    // ---- API keys ---------------------------------------------------------

    static string KeyPath(string id) => Path.Combine(Dir, $"api_{id}.key");

    public static bool HasKey(Provider p) => LoadKey(p.Id) != null;

    public static string? LoadKey() => LoadKey(Current.Id);

    public static string? LoadKey(string id)
    {
        try
        {
            var path = KeyPath(id);
            if (!File.Exists(path)) return null;
            var raw = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(raw);
        }
        catch { return null; }
    }

    public static void SaveKey(string id, string key)
    {
        Directory.CreateDirectory(Dir);
        var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(KeyPath(id), enc);
    }
}
