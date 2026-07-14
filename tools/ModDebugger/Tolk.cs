using System.Runtime.InteropServices;

namespace ModDebugger;

/// <summary>
/// Minimal Tolk screen reader wrapper (same trimmed P/Invoke surface the configurator
/// uses). Requires Tolk.dll and its friends next to the exe.
/// </summary>
internal static class Tolk
{
    [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Tolk_Load();

    [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool Tolk_IsLoaded();

    [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Tolk_Unload();

    [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Tolk_TrySAPI([MarshalAs(UnmanagedType.I1)] bool trySAPI);

    [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool Tolk_Output(
        [MarshalAs(UnmanagedType.LPWStr)] string str,
        [MarshalAs(UnmanagedType.I1)] bool interrupt);

    private static bool loaded;

    public static void Initialize()
    {
        try
        {
            Tolk_TrySAPI(true);
            Tolk_Load();
            loaded = Tolk_IsLoaded();
        }
        catch
        {
            loaded = false;
        }
    }

    public static void Speak(string text, bool interrupt = true)
    {
        if (!loaded || string.IsNullOrEmpty(text)) return;
        try { Tolk_Output(text, interrupt); } catch { /* best-effort */ }
    }

    public static void Shutdown()
    {
        if (!loaded) return;
        try { Tolk_Unload(); } catch { /* best-effort */ }
        loaded = false;
    }
}
