using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class SettingsPage : Page
    {
        private bool _loading = true;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _loading = true;

            var prefs = PreferencesService.Load();
            RbFormal.IsChecked = prefs.Theme == "Formal";
            RbSleek.IsChecked = prefs.Theme == "Sleek";
            RbLight.IsChecked = !prefs.DarkMode;
            RbDark.IsChecked = prefs.DarkMode;
            CbAnimations.IsChecked = prefs.Animations;

            UpdateCredText();
            ApplyTheme();

            _loading = false;
        }

        private void UpdateCredText()
        {
            SavedCredText.Text = CredentialService.HasSaved()
                ? "Samodejno prijavljanje je omogočeno"
                : "Ni shranjenih prijavnih podatkov";
        }

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            SaveSettings();
        }

        private void DarkMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            SaveSettings();
            ApplyTheme();
        }

        private void Anim_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            SaveSettings();
        }

        private void SaveSettings()
        {
            var prefs = PreferencesService.Load();
            prefs.Theme = RbSleek.IsChecked == true ? "Sleek" : "Formal";
            prefs.DarkMode = RbDark.IsChecked == true;
            prefs.Animations = CbAnimations.IsChecked == true;
            PreferencesService.Save(prefs);
            ShowSavedMsg();
        }

        private async void ShowSavedMsg()
        {
            const int SavedMessageDisplayDurationMs = 2000;
            SavedMsg.Visibility = Visibility.Visible;
            await Task.Delay(SavedMessageDisplayDurationMs);
            SavedMsg.Visibility = Visibility.Collapsed;
        }

        private void DeleteCred_Click(object sender, RoutedEventArgs e)
        {
            CredentialService.Delete();
            UpdateCredText();
        }

        private void ApplyTheme()
        {
            RootGrid.Background = AppTheme.BgBrush;
            TitleBar.Background = AppTheme.TitleBarBrush;
            ThemeCard.Background = AppTheme.CardBrush;
            ThemeCard.BorderBrush = AppTheme.BorderBrush;
            AccountCard.Background = AppTheme.CardBrush;
            AccountCard.BorderBrush = AppTheme.BorderBrush;
        }
    }
}
