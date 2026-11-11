namespace Wheat;

/// <summary>Shared application constants.</summary>
public static class AppInfo
{
    public const string Name = "Wheat";
    public const string Version = "1.0.0";

    static Icon? _icon;
    static bool _iconLoaded;

    /// <summary>The icon embedded in the exe (null if it cannot be read).</summary>
    public static Icon? AppIcon
    {
        get
        {
            if (_iconLoaded) return _icon;
            _iconLoaded = true;
            try
            {
                var path = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(path)) _icon = Icon.ExtractAssociatedIcon(path);
            }
            catch { /* fall back to the drawn icon */ }
            return _icon;
        }
    }
}
