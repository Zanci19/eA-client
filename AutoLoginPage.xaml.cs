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

                var loggedIn = false;

                if (!string.IsNullOrWhiteSpace(saved.RefreshToken))
                {
                    try
                    {
                        var newToken = await EAsistentService.RefreshTokenAsync(saved.RefreshToken);
                        AuthState.AccessToken = newToken;
                        AuthState.RefreshToken = saved.RefreshToken;
                        loggedIn = !string.IsNullOrWhiteSpace(newToken);
                    }
                    catch
                    {
                        loggedIn = false;
                    }
                }

                if (!loggedIn && !string.IsNullOrWhiteSpace(saved.Username) && !string.IsNullOrWhiteSpace(saved.Password))
                {
                    StatusText.Text = $"Ponovna prijava za {saved.Username}...";
                    var result = await EAsistentService.LoginAsync(saved.Username, saved.Password);
                    AuthState.AccessToken = result.AccessToken.Token;
                    AuthState.RefreshToken = result.RefreshToken;
                    AuthState.TokenExpiration = result.AccessToken.ExpirationDate;
                    AuthState.UserId = result.User.Id;
                    AuthState.UserType = result.User.Type;
                    AuthState.DisplayName = result.User.Name;
                    loggedIn = true;
                    saved = new SavedSession
                    {
                        Username = saved.Username,
                        Password = saved.Password,
                        RefreshToken = result.RefreshToken,
                        UserId = result.User.Id,
                        SavedAtUtc = saved.SavedAtUtc
                    };
                }

                if (!loggedIn)
                {
                    throw new System.Exception("Samodejna prijava ni uspela.");
                }

                CredentialService.Save(saved.Username, saved.Password, AuthState.RefreshToken, AuthState.UserId);

                try
                {
                    var childInfo = await EAsistentService.GetChildInfoAsync(AuthState.AccessToken);
                    if (childInfo.TryGetProperty("display_name", out var dn)) AuthState.DisplayName = dn.GetString() ?? string.Empty;
                    if (childInfo.TryGetProperty("short_name", out var sn)) AuthState.ShortName = sn.GetString() ?? string.Empty;
                    if (childInfo.TryGetProperty("plus_enabled", out var pe)) AuthState.PlusEnabled = pe.GetBoolean();
                    if (childInfo.TryGetProperty("student_id", out var sid)) AuthState.StudentId = sid.GetInt32();
                    if (childInfo.TryGetProperty("type", out var ut)) AuthState.UserType = ut.GetString() ?? "child";
                    else AuthState.UserType = "child";
                }
                catch
                {
                    AuthState.UserType = string.IsNullOrWhiteSpace(AuthState.UserType) ? "child" : AuthState.UserType;
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
