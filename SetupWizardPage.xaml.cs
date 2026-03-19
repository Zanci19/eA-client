using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class SetupWizardPage : Page
    {
        private readonly Frame _frame;
        private readonly UserPreferences _prefs;

        public SetupWizardPage(Frame frame)
        {
            InitializeComponent();
            _frame = frame;
            _prefs = PreferencesService.Load();
            Loaded += SetupWizardPage_Loaded;
        }

        private void SetupWizardPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTheme();
            AnimationHelper.FadeInFromBelow(ContentHost, 320);
        }

        private void GoToStep(int step)
        {
            Step1.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
            StepLabel.Text = $"Korak {step} od 3";
            Dot1.Fill = step >= 1 ? AppTheme.AccentBrush : Brushes.LightGray;
            Dot2.Fill = step >= 2 ? AppTheme.AccentBrush : Brushes.LightGray;
            Dot3.Fill = step >= 3 ? AppTheme.AccentBrush : Brushes.LightGray;
            AnimationHelper.FadeInFromBelow(ContentHost, 260);
        }

        private void BtnFormal_Click(object sender, RoutedEventArgs e)
        {
            _prefs.ExperienceMode = "Formal";
            GoToStep(2);
        }

        private void BtnSleek_Click(object sender, RoutedEventArgs e)
        {
            _prefs.ExperienceMode = "Sleek";
            GoToStep(2);
        }

        private void BtnLight_Click(object sender, RoutedEventArgs e)
        {
            _prefs.DarkMode = false;
            GoToStep(3);
        }

        private void BtnDark_Click(object sender, RoutedEventArgs e)
        {
            _prefs.DarkMode = true;
            GoToStep(3);
        }

        private void BtnAnimOn_Click(object sender, RoutedEventArgs e)
        {
            _prefs.Animations = true;
            FinishSetup();
        }

        private void BtnAnimOff_Click(object sender, RoutedEventArgs e)
        {
            _prefs.Animations = false;
            FinishSetup();
        }

        private void FinishSetup()
        {
            _prefs.IsFirstRun = false;
            PreferencesService.Save(_prefs);

            if (_prefs.AutoLoginEnabled && CredentialService.HasSaved())
            {
                AnimationHelper.NavigateWithTransition(_frame, new AutoLoginPage(_frame));
                return;
            }

            AnimationHelper.NavigateWithTransition(_frame, new LoginPage());
        }

        private void ApplyTheme()
        {
            RootGrid.Background = AppTheme.BgBrush;
            TitleBar.Background = AppTheme.TitleBarBrush;
            FontFamily = new FontFamily(AppTheme.FontFamily);
        }
    }
}
