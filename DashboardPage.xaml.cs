using System.Windows;
using System.Windows.Controls;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
            Loaded += DashboardPage_Loaded;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            UserNameLabel.Text = AuthState.DisplayName;
            UserTypeLabel.Text = AuthState.UserType == "child" ? "Dijak / Učenec" : AuthState.UserType;
            AvatarInitial.Text = string.IsNullOrWhiteSpace(AuthState.DisplayName)
                ? "U"
                : AuthState.DisplayName[0].ToString().ToUpperInvariant();

            ContentFrame.Navigate(new OverviewPage());
        }

        private void NavHome_Click(object sender, RoutedEventArgs e)
            => ContentFrame.Navigate(new OverviewPage());

        private void NavTimetable_Click(object sender, RoutedEventArgs e)
            => ContentFrame.Navigate(new TimetablePage());

        private void NavGrades_Click(object sender, RoutedEventArgs e)
            => ContentFrame.Navigate(new GradesPage());

        private void NavFood_Click(object sender, RoutedEventArgs e)
            => ContentFrame.Navigate(new FoodPage());

        private void NavHomework_Click(object sender, RoutedEventArgs e)
            => ContentFrame.Navigate(new HomeworkPage());

        private void NavAbsences_Click(object sender, RoutedEventArgs e)
            => ContentFrame.Navigate(new AbsencesPage());

        private void NavEval_Click(object sender, RoutedEventArgs e)
            => ContentFrame.Navigate(new EvaluationsPage());

        private void NavProfile_Click(object sender, RoutedEventArgs e)
            => ContentFrame.Navigate(new ProfilePage());

        private void NavSettings_Click(object sender, RoutedEventArgs e)
            => ContentFrame.Navigate(new SettingsPage());

        private void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            CredentialService.Delete();
            AuthState.Clear();
            NavigationService.Navigate(new LoginPage());
        }
    }
}
