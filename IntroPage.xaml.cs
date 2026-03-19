using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

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
                _frame.Navigate(new LoginPage());
                return;
            }

            IntroVideo.Source = new Uri(videoPath);
            IntroVideo.Play();
        }

        private void IntroVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            _frame.Navigate(new LoginPage());
        }

        private void IntroVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _frame.Navigate(new LoginPage());
        }
    }
}
