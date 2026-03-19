using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await DoLoginAsync();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                _ = DoLoginAsync();
        }

        private async Task DoLoginAsync()
        {
            var username = UsernameBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ErrorText.Text = "Vnesite uporabniško ime in geslo.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            ErrorText.Visibility = Visibility.Collapsed;
            StatusText.Text = "Prijava v teku...";
            StatusText.Visibility = Visibility.Visible;
            LoginButton.IsEnabled = false;
            UsernameBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;

            try
            {
                var result = await EAsistentService.LoginAsync(username, password);

                AuthState.AccessToken = result.AccessToken.Token;
                AuthState.RefreshToken = result.RefreshToken;
                AuthState.TokenExpiration = result.AccessToken.ExpirationDate;
                AuthState.UserId = result.User.Id;
                AuthState.UserType = result.User.Type;
                AuthState.DisplayName = result.User.Name;

                // Enrich with child info
                try
                {
                    var childInfo = await EAsistentService.GetChildInfoAsync(result.AccessToken.Token);
                    if (childInfo.TryGetProperty("display_name", out var dn))
                        AuthState.DisplayName = dn.GetString() ?? AuthState.DisplayName;
                    if (childInfo.TryGetProperty("short_name", out var sn))
                        AuthState.ShortName = sn.GetString() ?? string.Empty;
                    if (childInfo.TryGetProperty("plus_enabled", out var pe))
                        AuthState.PlusEnabled = pe.GetBoolean();
                    if (childInfo.TryGetProperty("student_id", out var sid))
                        AuthState.StudentId = sid.GetInt32();
                }
                catch { /* Use defaults from login response */ }

                // Save credentials for auto-login
                CredentialService.Save(result.RefreshToken, result.User.Id);

                NavigationService.Navigate(new DashboardPage());
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Napaka pri prijavi: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                StatusText.Visibility = Visibility.Collapsed;
                LoginButton.IsEnabled = true;
                UsernameBox.IsEnabled = true;
                PasswordBox.IsEnabled = true;
            }
        }
    }
}
