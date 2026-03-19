using System;
using System.IO;
using System.Text.Json;

namespace EAClient.Services
{
    public class UserPreferences
    {
        public string ExperienceMode { get; set; } = "Formal";
        public bool DarkMode { get; set; }
        public bool Animations { get; set; } = true;
        public bool IsFirstRun { get; set; } = true;
        public bool AutoLoginEnabled { get; set; } = true;
    }

    public static class PreferencesService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EAClient", "preferences.json");

        private static UserPreferences? _cache;

        public static event Action<UserPreferences>? PreferencesChanged;

        public static UserPreferences Load()
        {
            if (_cache != null)
            {
                return _cache;
            }

            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _cache = JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
                    Normalize(_cache);
                    return _cache;
                }
            }
            catch
            {
            }

            _cache = new UserPreferences();
            return _cache;
        }

        public static void Save(UserPreferences prefs)
        {
            Normalize(prefs);
            _cache = prefs;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(prefs, JsonOptions));
            }
            catch
            {
            }

            PreferencesChanged?.Invoke(prefs);
        }

        public static bool IsFirstRun() => Load().IsFirstRun;

        public static void MarkNotFirstRun()
        {
            var prefs = Load();
            prefs.IsFirstRun = false;
            Save(prefs);
        }

        private static void Normalize(UserPreferences prefs)
        {
            if (string.IsNullOrWhiteSpace(prefs.ExperienceMode))
            {
                prefs.ExperienceMode = "Formal";
            }
        }
    }
}
