using Microsoft.Win32;

namespace ArcGisProMcpFree
{
    /// <summary>
    /// UI language: English by default, Spanish on toggle. Persisted in
    /// HKCU (no files), so the choice survives Pro restarts.
    /// </summary>
    internal static class Lang
    {
        private const string Key = @"HKEY_CURRENT_USER\SOFTWARE\IngKevin\ArcGisProMcpFree";

        public static string Current { get; private set; } = Load();

        public static bool IsSpanish => Current == "es";

        private static string Load()
        {
            try
            {
                var v = Registry.GetValue(Key, "Lang", "en") as string;
                return v == "es" ? "es" : "en";
            }
            catch
            {
                return "en";
            }
        }

        public static void Toggle()
        {
            Current = IsSpanish ? "en" : "es";
            try
            {
                Registry.SetValue(Key, "Lang", Current);
            }
            catch
            {
                // Persistence is best-effort; the session choice still applies.
            }
        }

        public static string T(string en, string es)
        {
            return IsSpanish ? es : en;
        }
    }
}
