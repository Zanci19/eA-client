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
            RbFormal.IsChecked = prefs.ExperienceMode == "Formal";
            RbSleek.IsChecked = prefs.ExperienceMode == "Sleek";
            RbLight.IsChecked = !prefs.DarkMode;
            RbDark.IsChecked = prefs.DarkMode;
            CbAnimations.IsChecked = prefs.Animations;
            CbAutoLogin.IsChecked = prefs.AutoLoginEnabled;

            UpdateCredText();
            ApplyTheme();
            AnimationHelper.FadeInFromBelow(ContentStack, 260);

            _loading = false;
        }

        private void UpdateCredText()
        {
            var saved = CredentialService.Load();
            SavedCredText.Text = saved != null
                ? $"Samodejna prijava je omogočena za uporabnika {saved.Username}."
                : "Ni shranjene šifrirane seje.";
            BtnDeleteCred.IsEnabled = saved != null;
        }

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loading)
            {
                SaveSettings();
            }
        }

        private void DarkMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loading)
            {
                SaveSettings();
                ApplyTheme();
            }
        }

        private void Anim_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loading)
            {
                SaveSettings();
            }
        }

        private void AutoLogin_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading)
            {
                return;
            }

            SaveSettings();
            if (CbAutoLogin.IsChecked != true)
            {
                CredentialService.Delete();
                UpdateCredText();
            }
        }

        private void SaveSettings()
        {
            var prefs = PreferencesService.Load();
            prefs.ExperienceMode = RbSleek.IsChecked == true ? "Sleek" : "Formal";
            prefs.DarkMode = RbDark.IsChecked == true;
            prefs.Animations = CbAnimations.IsChecked == true;
            prefs.AutoLoginEnabled = CbAutoLogin.IsChecked == true;
            PreferencesService.Save(prefs);
            ShowSavedMsg();
            UpdateCredText();
        }

        private async void ShowSavedMsg()
        {
            SavedMsg.Visibility = Visibility.Visible;
            await Task.Delay(1800);
            SavedMsg.Visibility = Visibility.Collapsed;
        }

        private void DeleteCred_Click(object sender, RoutedEventArgs e)
        {
            CredentialService.Delete();
            var prefs = PreferencesService.Load();
            prefs.AutoLoginEnabled = false;
            PreferencesService.Save(prefs);
            CbAutoLogin.IsChecked = false;
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
            FontFamily = new FontFamily(AppTheme.FontFamily);

            // Update all text elements
            var headerBrush = AppTheme.TextBrush;
            var labelBrush = AppTheme.TextBrush;
            var hintBrush = AppTheme.SubTextBrush;
            var dividerBrush = AppTheme.BorderBrush;

            TbAppearanceHeader.Foreground = headerBrush;
            TbAccountHeader.Foreground = headerBrush;
            TbExperienceLabel.Foreground = labelBrush;
            TbExperienceHint.Foreground = hintBrush;
            TbThemeLabel.Foreground = labelBrush;
            TbAnimLabel.Foreground = labelBrush;
            TbAnimHint.Foreground = hintBrush;
            TbAutoLoginLabel.Foreground = labelBrush;
            SavedCredText.Foreground = hintBrush;
            TbDeleteLabel.Foreground = labelBrush;
            TbDeleteHint.Foreground = hintBrush;

            DividerA.Background = dividerBrush;
            DividerB.Background = dividerBrush;
            DividerC.Background = dividerBrush;
        }
    }
}
