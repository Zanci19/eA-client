using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EAClient.Services
{
    public static class CredentialService
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EAClient", "session.dat");

        public static void Save(string refreshToken, int userId)
        {
            var data = JsonSerializer.Serialize(new { refreshToken, userId });
            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(data), null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, encrypted);
        }

        public static (string refreshToken, int userId)? Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var encrypted = File.ReadAllBytes(FilePath);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var json = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(decrypted));
                var rt = json.GetProperty("refreshToken").GetString() ?? string.Empty;
                var uid = json.GetProperty("userId").GetInt32();
                if (string.IsNullOrEmpty(rt)) return null;
                return (rt, uid);
            }
            catch { return null; }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch { }
        }

        public static bool HasSaved() => File.Exists(FilePath);
    }
}
