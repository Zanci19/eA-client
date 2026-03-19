using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class SetupWizardPage : Page
    {
        private readonly Frame _frame;
        private readonly UserPreferences _prefs = new();
        private int _step = 1;

        public SetupWizardPage(Frame frame)
        {
            InitializeComponent();
            _frame = frame;
        }

        private void GoToStep(int step)
        {
            _step = step;
            Step1.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
            StepLabel.Text = $"Korak {step} od 3";
            Dot1.Fill = new SolidColorBrush(step >= 1 ? Color.FromRgb(0, 102, 204) : Color.FromRgb(204, 204, 204));
            Dot2.Fill = new SolidColorBrush(step >= 2 ? Color.FromRgb(0, 102, 204) : Color.FromRgb(204, 204, 204));
            Dot3.Fill = new SolidColorBrush(step >= 3 ? Color.FromRgb(0, 102, 204) : Color.FromRgb(204, 204, 204));
        }

        private void BtnFormal_Click(object sender, RoutedEventArgs e)
        {
            _prefs.Theme = "Formal";
            GoToStep(2);
        }

        private void BtnSleek_Click(object sender, RoutedEventArgs e)
        {
            _prefs.Theme = "Sleek";
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

            if (CredentialService.HasSaved())
                _frame.Navigate(new AutoLoginPage(_frame));
            else
                _frame.Navigate(new LoginPage());
        }
    }
}
