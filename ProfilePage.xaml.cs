using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class ProfilePage : Page
    {
        public ProfilePage()
        {
            InitializeComponent();
            Loaded += ProfilePage_Loaded;
        }

        private async void ProfilePage_Loaded(object sender, RoutedEventArgs e)
            => await LoadDataAsync();

        private async Task LoadDataAsync()
        {
            ShowLoading();
            try
            {
                // Try to refresh child info if available
                try
                {
                    var childInfo = await EAsistentService.GetChildInfoAsync(AuthState.AccessToken);
                    if (childInfo.TryGetProperty("display_name", out var dn))
                        AuthState.DisplayName = dn.GetString() ?? AuthState.DisplayName;
                    if (childInfo.TryGetProperty("short_name", out var sn))
                        AuthState.ShortName = sn.GetString() ?? AuthState.ShortName;
                    if (childInfo.TryGetProperty("plus_enabled", out var pe))
                        AuthState.PlusEnabled = pe.GetBoolean();
                    if (childInfo.TryGetProperty("student_id", out var sid))
                        AuthState.StudentId = sid.GetInt32();
                }
                catch { /* use existing AuthState data */ }

                PopulateProfile();
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju profila. Poskusite znova.\n{ex.Message}");
            }
        }

        private void PopulateProfile()
        {
            var name = AuthState.DisplayName;
            AvatarText.Text = string.IsNullOrWhiteSpace(name) ? "U" : name[0].ToString().ToUpperInvariant();
            DisplayNameText.Text = name;
            UserTypeText.Text = AuthState.UserType == "child" ? "Dijak / Učenec" : AuthState.UserType;

            InfoPanel.Children.Clear();
            AddInfoRow(InfoPanel, "Ime in priimek", AuthState.DisplayName);
            AddInfoRow(InfoPanel, "Kratko ime", AuthState.ShortName);
            AddInfoRow(InfoPanel, "ID učenca", AuthState.StudentId > 0 ? AuthState.StudentId.ToString() : "–");
            AddInfoRow(InfoPanel, "Vrsta računa", AuthState.UserType);

            var plusColor = AuthState.PlusEnabled
                ? new SolidColorBrush(Color.FromRgb(0, 153, 76))
                : new SolidColorBrush(Color.FromRgb(150, 150, 150));
            var plusBadge = new Border
            {
                Background = new SolidColorBrush(AuthState.PlusEnabled
                    ? Color.FromRgb(230, 255, 240)
                    : Color.FromRgb(240, 240, 240)),
                BorderBrush = plusColor,
                BorderThickness = new Thickness(1,1,1,1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                Child = new TextBlock
                {
                    Text = AuthState.PlusEnabled ? "✓ Plus aktiviran" : "✗ Plus ni aktiviran",
                    Foreground = plusColor,
                    FontSize = 12
                }
            };
            AddInfoRowWithWidget(InfoPanel, "eAsistent Plus", plusBadge);

            TokenPanel.Children.Clear();
            AddInfoRow(TokenPanel, "Iztetek žetona", string.IsNullOrEmpty(AuthState.TokenExpiration)
                ? "Ni podatka"
                : AuthState.TokenExpiration);
        }

        private static void AddInfoRow(StackPanel panel, string label, string value)
        {
            var row = new DockPanel { Margin = new Thickness(0, 5, 0, 5) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = Brushes.Gray,
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51)),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(row);
            panel.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(240, 242, 245)),
                Margin = new Thickness(0, 3, 0, 3)
            });
        }

        private static void AddInfoRowWithWidget(StackPanel panel, string label, UIElement widget)
        {
            var row = new DockPanel { Margin = new Thickness(0, 5, 0, 5) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = Brushes.Gray,
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(widget);
            panel.Children.Add(row);
            panel.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(240, 242, 245)),
                Margin = new Thickness(0, 3, 0, 3)
            });
        }

        private void ShowLoading()
        {
            LoadingPanel.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowContent()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Visible;
        }

        private void ShowError(string msg)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ContentPanel.Visibility = Visibility.Collapsed;
            ErrorText.Text = msg;
        }

        private async void Retry_Click(object sender, RoutedEventArgs e)
            => await LoadDataAsync();
    }
}
