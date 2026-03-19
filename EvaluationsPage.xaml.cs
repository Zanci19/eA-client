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
    public partial class EvaluationsPage : Page
    {
        public EvaluationsPage()
        {
            InitializeComponent();
            Loaded += EvaluationsPage_Loaded;
        }

        private async void EvaluationsPage_Loaded(object sender, RoutedEventArgs e)
            => await LoadDataAsync();

        private async Task LoadDataAsync()
        {
            ShowLoading();
            try
            {
                var json = await EAsistentService.GetEvaluationsAsync(AuthState.AccessToken);
                PopulateEvaluations(json);
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju ocenjevanj. Poskusite znova.\n{ex.Message}");
            }
        }

        private void PopulateEvaluations(JsonElement json)
        {
            EvalContainer.Children.Clear();

            var items = new List<(string date, string subject, string type, string name, string teacher)>();
            try
            {
                JsonElement arr = default;
                if (json.TryGetProperty("evaluations", out var ev) && ev.ValueKind == JsonValueKind.Array)
                    arr = ev;
                else if (json.ValueKind == JsonValueKind.Array)
                    arr = json;

                if (arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        var date = GetStr(item, "date", "");
                        var subject = GetStr(item, "subject", GetStr(item, "subject_name", "Predmet"));
                        var type = GetStr(item, "type", GetStr(item, "kind", ""));
                        var name = GetStr(item, "name", GetStr(item, "description", GetStr(item, "title", "")));
                        var teacher = GetStr(item, "teacher", GetStr(item, "teacher_name", ""));
                        items.Add((date, subject, type, name, teacher));
                    }
                }
            }
            catch { /* ignore */ }

            if (!items.Any())
            {
                EvalContainer.Children.Add(new TextBlock
                {
                    Text = "Ni prihajajoče ocenjevanj.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(4,4,4,4)
                });
                return;
            }

            // Sort by date ascending
            var sorted = items.OrderBy(x =>
            {
                DateTime.TryParse(x.date, out var d);
                return d;
            }).ToList();

            var today = DateTime.Today;
            foreach (var (date, subject, type, name, teacher) in sorted)
            {
                DateTime.TryParse(date, out var dateObj);
                var daysUntil = dateObj != default ? (dateObj.Date - today).TotalDays : double.MaxValue;
                var isUrgent = daysUntil >= 0 && daysUntil <= 7;
                var isPast = daysUntil < 0;

                var card = new Border
                {
                    Background = isUrgent
                        ? new SolidColorBrush(Color.FromRgb(255, 245, 230))
                        : Brushes.White,
                    BorderBrush = isUrgent
                        ? new SolidColorBrush(Color.FromRgb(255, 153, 0))
                        : new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                    BorderThickness = new Thickness(isUrgent ? 2 : 1, isUrgent ? 2 : 1, isUrgent ? 2 : 1, isUrgent ? 2 : 1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 0, 0, 10),
                    Opacity = isPast ? 0.6 : 1.0
                };

                var mainRow = new DockPanel();
                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                // Subject + date row
                var subjectRow = new DockPanel();
                subjectRow.Children.Add(new TextBlock
                {
                    Text = subject,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51)),
                    VerticalAlignment = VerticalAlignment.Center
                });

                // Date badge
                var dateBadge = new Border
                {
                    Background = isUrgent
                        ? new SolidColorBrush(Color.FromRgb(255, 153, 0))
                        : new SolidColorBrush(Color.FromRgb(70, 90, 130)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10, 3, 10, 3),
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = date, Foreground = Brushes.White, FontSize = 12 }
                };
                DockPanel.SetDock(dateBadge, Dock.Right);
                subjectRow.Children.Insert(0, dateBadge);
                sp.Children.Add(subjectRow);

                // Urgent label
                if (isUrgent)
                {
                    var daysStr = daysUntil < 1 ? "Danes!" : $"Čez {(int)daysUntil} dni";
                    sp.Children.Add(new TextBlock
                    {
                        Text = $"⚠️  {daysStr}",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 100, 0)),
                        Margin = new Thickness(0, 3, 0, 0)
                    });
                }

                if (!string.IsNullOrEmpty(type))
                    sp.Children.Add(new TextBlock { Text = type, FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 0) });
                if (!string.IsNullOrEmpty(name))
                    sp.Children.Add(new TextBlock { Text = name, FontSize = 13, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                if (!string.IsNullOrEmpty(teacher))
                    sp.Children.Add(new TextBlock { Text = $"👤 {teacher}", FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 0) });

                mainRow.Children.Add(sp);
                card.Child = mainRow;
                EvalContainer.Children.Add(card);
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
