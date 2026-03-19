namespace EAClient.Services
{
    public static class AuthState
    {
        public static string AccessToken { get; set; } = string.Empty;
        public static string RefreshToken { get; set; } = string.Empty;
        public static string TokenExpiration { get; set; } = string.Empty;
        public static string DisplayName { get; set; } = string.Empty;
        public static string ShortName { get; set; } = string.Empty;
        public static bool PlusEnabled { get; set; }
        public static int StudentId { get; set; }
        public static int UserId { get; set; }
        public static string UserType { get; set; } = string.Empty;

        public static void Clear()
        {
            AccessToken = string.Empty;
            RefreshToken = string.Empty;
            TokenExpiration = string.Empty;
            DisplayName = string.Empty;
            ShortName = string.Empty;
            PlusEnabled = false;
            StudentId = 0;
            UserId = 0;
            UserType = string.Empty;
        }
    }
}
