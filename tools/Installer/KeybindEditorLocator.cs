namespace Installer;

/// <summary>Finds DiscoElysiumKeybindEditor.exe next to this installer (dev builds keep the two tools as siblings).</summary>
public static class KeybindEditorLocator
{
    public static string? Find()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "DiscoElysiumModConfigurator.exe"),
            // pre-rename exe name, still found for older bundles
            Path.Combine(AppContext.BaseDirectory, "DiscoElysiumKeybindEditor.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "KeybindEditor", "DiscoElysiumModConfigurator.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
