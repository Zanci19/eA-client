using System;
using System.IO;
using System.Text.Json;

namespace EAClient.Services
{
    public class UserPreferences
    {
        public string Theme { get; set; } = "Formal"; // "Formal" or "Sleek"
        public bool DarkMode { get; set; } = false;
        public bool Animations { get; set; } = true;
        public bool IsFirstRun { get; set; } = true;
    }

    public static class PreferencesService
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EAClient", "preferences.json");

        private static UserPreferences? _cache;

        public static UserPreferences Load()
        {
            if (_cache != null) return _cache;
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _cache = JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
                    return _cache;
                }
            }
            catch { }
            _cache = new UserPreferences();
            return _cache;
        }

        public static void Save(UserPreferences prefs)
        {
            _cache = prefs;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static bool IsFirstRun()
        {
            var prefs = Load();
            return prefs.IsFirstRun;
        }

        public static void MarkNotFirstRun()
        {
            var prefs = Load();
            prefs.IsFirstRun = false;
            Save(prefs);
        }
    }
}
