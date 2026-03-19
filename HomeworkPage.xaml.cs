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
    public partial class HomeworkPage : Page
    {
        public HomeworkPage()
        {
            InitializeComponent();
            Loaded += HomeworkPage_Loaded;
        }

        private async void HomeworkPage_Loaded(object sender, RoutedEventArgs e)
            => await LoadDataAsync();

        private async Task LoadDataAsync()
        {
            ShowLoading();
            try
            {
                var year = DateTime.Today.Year;
                var from = new DateTime(year, 1, 1);
                var to = new DateTime(year, 12, 31);
                var json = await EAsistentService.GetHomeworkAsync(AuthState.AccessToken, from, to);
                PopulateHomework(json);
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju domačih nalog. Poskusite znova.\n{ex.Message}");
            }
        }

        private void PopulateHomework(JsonElement json)
        {
            HwContainer.Children.Clear();

            var items = new List<(string date, string subject, string description, string teacher)>();
            try
            {
                JsonElement arr = default;
                if (json.TryGetProperty("homework", out var hw) && hw.ValueKind == JsonValueKind.Array)
                    arr = hw;
                else if (json.TryGetProperty("homeworks", out var hws) && hws.ValueKind == JsonValueKind.Array)
                    arr = hws;
                else if (json.ValueKind == JsonValueKind.Array)
                    arr = json;

                if (arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        var date = GetStr(item, "date", "");
                        var subject = GetStr(item, "subject", GetStr(item, "subject_name", "Predmet"));
                        var desc = GetStr(item, "description", GetStr(item, "name", GetStr(item, "content", "")));
                        var teacher = GetStr(item, "teacher", GetStr(item, "teacher_name", ""));
                        items.Add((date, subject, desc, teacher));
                    }
                }
            }
            catch { /* ignore */ }

            if (!items.Any())
            {
                HwContainer.Children.Add(new TextBlock
                {
                    Text = "Ni domačih nalog za prikaz.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(4,4,4,4)
                });
                return;
            }

            // Group by date, newest first
            var grouped = items
                .GroupBy(x => x.date)
                .OrderByDescending(g =>
                {
                    DateTime.TryParse(g.Key, out var d);
                    return d;
                });

            foreach (var group in grouped)
            {
                // Date header
                DateTime.TryParse(group.Key, out var groupDate);
                var isToday = groupDate.Date == DateTime.Today;
                var dateStr = groupDate != default
                    ? groupDate.ToString("dddd, d. MMMM yyyy", new System.Globalization.CultureInfo("sl-SI"))
                    : group.Key;

                var dateHeader = new TextBlock
                {
                    Text = dateStr,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = isToday
                        ? new SolidColorBrush(Color.FromRgb(0, 102, 204))
                        : new SolidColorBrush(Color.FromRgb(100, 110, 130)),
                    Margin = new Thickness(0, 12, 0, 6)
                };
                HwContainer.Children.Add(dateHeader);

                foreach (var (_, subject, description, teacher) in group)
                {
                    var card = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                        BorderThickness = new Thickness(1,1,1,1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16, 12, 16, 12),
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    var sp = new StackPanel();
                    var headerRow = new DockPanel();

                    headerRow.Children.Add(new TextBlock
                    {
                        Text = subject,
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51)),
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    if (!string.IsNullOrEmpty(teacher))
                    {
                        var teacherTb = new TextBlock
                        {
                            Text = teacher,
                            FontSize = 12,
                            Foreground = Brushes.Gray,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        DockPanel.SetDock(teacherTb, Dock.Right);
                        headerRow.Children.Insert(0, teacherTb);
                    }

                    sp.Children.Add(headerRow);

                    if (!string.IsNullOrEmpty(description))
                        sp.Children.Add(new TextBlock
                        {
                            Text = description,
                            FontSize = 13,
                            Foreground = Brushes.DimGray,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 6, 0, 0)
                        });

                    card.Child = sp;
                    HwContainer.Children.Add(card);
                }
            }
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
