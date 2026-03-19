using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class IntroPage : Page
    {
        private readonly Frame _frame;

        public IntroPage(Frame frame)
        {
            InitializeComponent();
            _frame = frame;
            Loaded += IntroPage_Loaded;
        }

        private void IntroPage_Loaded(object sender, RoutedEventArgs e)
        {
            string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "intro.mp4");

            if (!File.Exists(videoPath))
            {
                NavigateAfterIntro();
                return;
            }

            IntroVideo.Source = new Uri(videoPath);
            IntroVideo.Play();
        }

        private void IntroVideo_MediaEnded(object sender, RoutedEventArgs e)
            => NavigateAfterIntro();

        private void IntroVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
            => NavigateAfterIntro();

        private void NavigateAfterIntro()
        {
            // Check first-run setup
            if (PreferencesService.IsFirstRun())
            {
                AnimationHelper.NavigateWithTransition(_frame, new SetupWizardPage(_frame));
                return;
            }
            var prefs = PreferencesService.Load();
            if (prefs.AutoLoginEnabled && CredentialService.HasSaved())
            {
                AnimationHelper.NavigateWithTransition(_frame, new AutoLoginPage(_frame));
                return;
            }
            AnimationHelper.NavigateWithTransition(_frame, new LoginPage());
        }
    }
}
