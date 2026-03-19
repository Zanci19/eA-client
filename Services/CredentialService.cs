using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EAClient.Services
{
    public sealed class SavedSession
    {
        public string Username { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int UserId { get; set; }
        public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    public static class CredentialService
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EAClient.Session.v1");

        private static string DirectoryPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EAClient");

        private static string FilePath => Path.Combine(DirectoryPath, "session.dat");

        public static void Save(string username, string refreshToken, int userId)
        {
            var payload = new SavedSession
            {
                Username = username,
                RefreshToken = refreshToken,
                UserId = userId,
                SavedAtUtc = DateTimeOffset.UtcNow
            };

            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)),
                Entropy,
                DataProtectionScope.CurrentUser);

            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllBytes(FilePath, encrypted);

            try
            {
                File.SetAttributes(FilePath, FileAttributes.Hidden);
            }
            catch
            {
            }
        }

        public static SavedSession? Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return null;
                }

                var encrypted = File.ReadAllBytes(FilePath);
                var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                var session = JsonSerializer.Deserialize<SavedSession>(Encoding.UTF8.GetString(decrypted));

                if (session == null || string.IsNullOrWhiteSpace(session.RefreshToken))
                {
                    return null;
                }

                return session;
            }
            catch
            {
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                var info = new FileInfo(FilePath);
                info.Attributes = FileAttributes.Normal;
                var fileLength = info.Length;

                using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    var wipe = new byte[fileLength];
                    RandomNumberGenerator.Fill(wipe);
                    stream.Write(wipe, 0, wipe.Length);
                    stream.Flush(true);
                }

                File.Delete(FilePath);
            }
            catch
            {
            }
        }

        public static bool HasSaved() => File.Exists(FilePath);
    }
}
