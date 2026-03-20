using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
            Loaded += DashboardPage_Loaded;
            Unloaded += DashboardPage_Unloaded;
            PreferencesService.PreferencesChanged += OnPreferencesChanged;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            UserNameLabel.Text = AuthState.DisplayName;
            UserTypeLabel.Text = AuthState.UserType == "child" ? "Dijak / Učenec" : AuthState.UserType;
            AvatarInitial.Text = string.IsNullOrWhiteSpace(AuthState.DisplayName) ? "U" : AuthState.DisplayName[0].ToString().ToUpperInvariant();

            ApplyTheme();
            NavigateTo(new OverviewPage());
        }

        private void NavHome_Click(object sender, RoutedEventArgs e) => NavigateTo(new OverviewPage());
        private void NavTimetable_Click(object sender, RoutedEventArgs e) => NavigateTo(new TimetablePage());
        private void NavFood_Click(object sender, RoutedEventArgs e) => NavigateTo(new FoodPage());
        private void NavCommunication_Click(object sender, RoutedEventArgs e) => NavigateTo(new CommunicationPage());
        private void NavGrades_Click(object sender, RoutedEventArgs e) => NavigateTo(new GradesPage());
        private void NavHomework_Click(object sender, RoutedEventArgs e) => NavigateTo(new HomeworkPage());
        private void NavAbsences_Click(object sender, RoutedEventArgs e) => NavigateTo(new AbsencesPage());
        private void NavEval_Click(object sender, RoutedEventArgs e) => NavigateTo(new EvaluationsPage());
        private void NavExams_Click(object sender, RoutedEventArgs e) => NavigateTo(new ExamsPage());
        private void NavPraises_Click(object sender, RoutedEventArgs e) => NavigateTo(new PraisesPage());
        private void NavConsents_Click(object sender, RoutedEventArgs e) => NavigateTo(new ConsentsPage());
        private void NavProfile_Click(object sender, RoutedEventArgs e) => NavigateTo(new ProfilePage());
        private void NavSettings_Click(object sender, RoutedEventArgs e) => NavigateTo(new SettingsPage());

        private void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            CredentialService.Delete();
            AuthState.Clear();
            NavigationService.Navigate(new LoginPage());
        }

        private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            PreferencesService.PreferencesChanged -= OnPreferencesChanged;
        }

        private void OnPreferencesChanged(UserPreferences prefs)
        {
            Dispatcher.Invoke(ApplyTheme);
        }

        private void NavigateTo(Page page)
        {
            AnimationHelper.NavigateWithTransition(ContentFrame, page);
        }

        private void ApplyTheme()
        {
            RootPanel.Background = AppTheme.BgBrush;
            Sidebar.Background = AppTheme.SidebarBrush;
            SidebarHeader.Background = AppTheme.SidebarHeaderBrush;
            ContentFrame.Background = AppTheme.BgBrush;
            FontFamily = new FontFamily(AppTheme.FontFamily);
            AnimationHelper.FadeIn(Sidebar, 220);
            AnimationHelper.FadeIn(ContentFrame, 260);
        }
    }
}
