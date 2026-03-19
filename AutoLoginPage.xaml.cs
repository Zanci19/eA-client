using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class AutoLoginPage : Page
    {
        private readonly Frame _frame;

        public AutoLoginPage(Frame frame)
        {
            InitializeComponent();
            _frame = frame;
            Loaded += AutoLoginPage_Loaded;
        }

        private async void AutoLoginPage_Loaded(object sender, RoutedEventArgs e)
        {
            await TryAutoLoginAsync();
        }

        private async Task TryAutoLoginAsync()
        {
            try
            {
                var prefs = PreferencesService.Load();
                if (!prefs.AutoLoginEnabled)
                {
                    _frame.Navigate(new LoginPage());
                    return;
                }

                var saved = CredentialService.Load();
                if (saved == null)
                {
                    _frame.Navigate(new LoginPage());
                    return;
                }

                AuthState.UserId = saved.UserId;
                StatusText.Text = string.IsNullOrWhiteSpace(saved.Username)
                    ? "Osveževanje seje..."
                    : $"Samodejna prijava za {saved.Username}...";

                var newToken = await EAsistentService.RefreshTokenAsync(saved.RefreshToken);
                AuthState.AccessToken = newToken;
                AuthState.RefreshToken = saved.RefreshToken;

                CredentialService.Save(saved.Username, saved.RefreshToken, saved.UserId);

                try
                {
                    var childInfo = await EAsistentService.GetChildInfoAsync(newToken);
                    if (childInfo.TryGetProperty("display_name", out var dn)) AuthState.DisplayName = dn.GetString() ?? string.Empty;
                    if (childInfo.TryGetProperty("short_name", out var sn)) AuthState.ShortName = sn.GetString() ?? string.Empty;
                    if (childInfo.TryGetProperty("plus_enabled", out var pe)) AuthState.PlusEnabled = pe.GetBoolean();
                    if (childInfo.TryGetProperty("student_id", out var sid)) AuthState.StudentId = sid.GetInt32();
                    if (childInfo.TryGetProperty("type", out var ut)) AuthState.UserType = ut.GetString() ?? "child";
                    else AuthState.UserType = "child";
                }
                catch
                {
                    AuthState.UserType = "child";
                }

                AnimationHelper.NavigateWithTransition(_frame, new DashboardPage());
            }
            catch
            {
                CredentialService.Delete();
                _frame.Navigate(new LoginPage());
            }
        }
    }
}
