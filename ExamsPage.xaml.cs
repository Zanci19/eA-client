using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class ExamsPage : Page
    {
        public ExamsPage()
        {
            InitializeComponent();
            Loaded += ExamsPage_Loaded;
        }

        private async void ExamsPage_Loaded(object sender, RoutedEventArgs e)
            => await LoadDataAsync();

        private async Task LoadDataAsync()
        {
            ShowLoading();
            try
            {
                var json = await EAsistentService.GetExamsAsync(AuthState.AccessToken);
                PopulateExams(json);
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju izpitov. Poskusite znova.\n{ex.Message}");
            }
        }

        private void PopulateExams(JsonElement json)
        {
            UpcomingContainer.Children.Clear();
            SubmittedContainer.Children.Clear();

            var upcoming = new List<JsonElement>();
            var submitted = new List<JsonElement>();

            if (json.TryGetProperty("upcoming_exams", out var upcomingArr) && upcomingArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in upcomingArr.EnumerateArray())
                    upcoming.Add(item);
            }

            if (json.TryGetProperty("submitted_exams", out var submittedArr) && submittedArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in submittedArr.EnumerateArray())
                    submitted.Add(item);
            }

            UpcomingHeader.Visibility = upcoming.Any() ? Visibility.Visible : Visibility.Collapsed;
            SubmittedHeader.Visibility = submitted.Any() ? Visibility.Visible : Visibility.Collapsed;

            if (!upcoming.Any() && !submitted.Any())
            {
                UpcomingHeader.Visibility = Visibility.Collapsed;
                SubmittedHeader.Visibility = Visibility.Collapsed;
                UpcomingContainer.Children.Add(new TextBlock
                {
                    Text = "Ni izpitov za prikaz.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(4, 4, 4, 4)
                });
                return;
            }

            foreach (var item in upcoming)
                UpcomingContainer.Children.Add(BuildExamCard(item, false));

            foreach (var item in submitted)
                SubmittedContainer.Children.Add(BuildExamCard(item, true));
        }

        private static Border BuildExamCard(JsonElement item, bool isSubmitted)
        {
            var subject = GetStr(item, "subject", GetStr(item, "subject_name", "Predmet"));
            var date = GetStr(item, "date", string.Empty);
            var title = GetStr(item, "title", GetStr(item, "name", string.Empty));
            var teacher = GetStr(item, "teacher", GetStr(item, "teacher_name", string.Empty));

            var card = new Border
            {
                Background = isSubmitted
                    ? new SolidColorBrush(Color.FromRgb(245, 250, 245))
                    : Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var sp = new StackPanel();
            var headerRow = new DockPanel();

            if (!string.IsNullOrEmpty(date))
            {
                var dateBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(70, 90, 130)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10, 3, 10, 3),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = date, Foreground = Brushes.White, FontSize = 12 }
                };
                DockPanel.SetDock(dateBadge, Dock.Right);
                headerRow.Children.Add(dateBadge);
            }

            headerRow.Children.Add(new TextBlock
            {
                Text = subject,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51)),
                VerticalAlignment = VerticalAlignment.Center
            });

            sp.Children.Add(headerRow);

            if (!string.IsNullOrEmpty(title))
                sp.Children.Add(new TextBlock { Text = title, FontSize = 13, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });

            if (!string.IsNullOrEmpty(teacher))
                sp.Children.Add(new TextBlock { Text = $"👤 {teacher}", FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 3, 0, 0) });

            if (isSubmitted)
                sp.Children.Add(new TextBlock { Text = "✓ Oddano", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0, 153, 76)), Margin = new Thickness(0, 3, 0, 0) });

            card.Child = sp;
            return card;
        }

        private static string GetStr(JsonElement el, string prop, string fallback)
        {
            if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? fallback;
            return fallback;
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
