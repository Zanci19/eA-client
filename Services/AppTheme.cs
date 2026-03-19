using System.Windows.Media;

namespace EAClient.Services
{
    public static class AppTheme
    {
        public static bool IsDark => PreferencesService.Load().DarkMode;
        public static bool IsSleek => PreferencesService.Load().ExperienceMode == "Sleek";
        public static bool Animations => PreferencesService.Load().Animations;

        public static SolidColorBrush BgBrush => IsDark
            ? CreateBrush(18, 22, 32)
            : CreateBrush(245, 247, 250);

        public static SolidColorBrush LoginBgBrush => IsDark
            ? CreateBrush(12, 16, 26)
            : new(Colors.White);

        public static SolidColorBrush CardBrush => IsDark
            ? CreateBrush(28, 35, 51)
            : new(Colors.White);

        public static SolidColorBrush TextBrush => IsDark
            ? CreateBrush(220, 225, 240)
            : CreateBrush(28, 35, 51);

        public static SolidColorBrush SubTextBrush => IsDark
            ? CreateBrush(140, 155, 180)
            : CreateBrush(102, 102, 102);

        public static SolidColorBrush BorderBrush => IsDark
            ? CreateBrush(45, 55, 78)
            : CreateBrush(228, 232, 240);

        public static SolidColorBrush AccentBrush => CreateBrush(0, 102, 204);
        public static SolidColorBrush AccentHoverBrush => CreateBrush(0, 82, 163);

        public static SolidColorBrush SidebarBrush => IsDark
            ? CreateBrush(10, 14, 22)
            : CreateBrush(28, 35, 51);

        public static SolidColorBrush SidebarHeaderBrush => IsDark
            ? CreateBrush(6, 9, 16)
            : CreateBrush(20, 26, 40);

        public static SolidColorBrush TitleBarBrush => IsDark
            ? CreateBrush(0, 80, 160)
            : AccentBrush;

        public static SolidColorBrush NavHoverBrush => IsDark
            ? CreateBrush(37, 48, 70)
            : CreateBrush(42, 58, 88);

        public static SolidColorBrush ChoiceCardBrush => IsDark
            ? CreateBrush(24, 29, 42)
            : new(Colors.White);

        public static SolidColorBrush ChoiceSelectedBrush => IsDark
            ? CreateBrush(12, 46, 88)
            : CreateBrush(230, 242, 255);

        public static double CardCornerRadius => IsSleek ? 20 : 10;
        public static double ButtonCornerRadius => IsSleek ? 22 : 6;
        public static string FontFamily => IsSleek ? "Segoe UI" : "Arial";

        private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
