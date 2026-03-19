using System.Windows.Media;

namespace EAClient.Services
{
    public static class AppTheme
    {
        // Resolved from preferences
        public static bool IsDark => PreferencesService.Load().DarkMode;
        public static bool IsSleek => PreferencesService.Load().Theme == "Sleek";
        public static bool Animations => PreferencesService.Load().Animations;

        // Core colors that change with dark mode
        public static SolidColorBrush BgBrush => IsDark
            ? new SolidColorBrush(Color.FromRgb(18, 22, 32))
            : new SolidColorBrush(Color.FromRgb(245, 247, 250));

        public static SolidColorBrush CardBrush => IsDark
            ? new SolidColorBrush(Color.FromRgb(28, 35, 51))
            : new SolidColorBrush(Colors.White);

        public static SolidColorBrush TextBrush => IsDark
            ? new SolidColorBrush(Color.FromRgb(220, 225, 240))
            : new SolidColorBrush(Color.FromRgb(28, 35, 51));

        public static SolidColorBrush SubTextBrush => IsDark
            ? new SolidColorBrush(Color.FromRgb(140, 155, 180))
            : new SolidColorBrush(Colors.Gray);

        public static SolidColorBrush BorderBrush => IsDark
            ? new SolidColorBrush(Color.FromRgb(45, 55, 78))
            : new SolidColorBrush(Color.FromRgb(224, 224, 224));

        public static SolidColorBrush AccentBrush =>
            new SolidColorBrush(Color.FromRgb(0, 102, 204));

        public static SolidColorBrush SidebarBrush => IsDark
            ? new SolidColorBrush(Color.FromRgb(10, 14, 22))
            : new SolidColorBrush(Color.FromRgb(28, 35, 51));

        public static SolidColorBrush SidebarHeaderBrush => IsDark
            ? new SolidColorBrush(Color.FromRgb(6, 9, 16))
            : new SolidColorBrush(Color.FromRgb(20, 26, 40));

        public static SolidColorBrush TitleBarBrush => IsDark
            ? new SolidColorBrush(Color.FromRgb(0, 80, 160))
            : new SolidColorBrush(Color.FromRgb(0, 102, 204));

        // Sleek vs Formal
        public static double CardCornerRadius => IsSleek ? 16 : 8;
        public static double ButtonCornerRadius => IsSleek ? 20 : 6;
        public static string FontFamily => IsSleek ? "Segoe UI" : "Arial";

        public static SolidColorBrush LoginBgBrush => IsDark
            ? new SolidColorBrush(Color.FromRgb(12, 16, 26))
            : new SolidColorBrush(Colors.White);
    }
}
