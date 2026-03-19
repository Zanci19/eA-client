using System.Windows;
using EAClient.Pages;

namespace EAClient
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new IntroPage(MainFrame));
        }
    }
}
