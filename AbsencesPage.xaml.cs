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
    public partial class AbsencesPage : Page
    {
        public AbsencesPage()
        {
            InitializeComponent();
            Loaded += AbsencesPage_Loaded;
        }

        private async void AbsencesPage_Loaded(object sender, RoutedEventArgs e)
            => await LoadDataAsync();

        private async Task LoadDataAsync()
        {
            ShowLoading();
            try
            {
                var year = DateTime.Today.Year;
                var from = new DateTime(year, 1, 1);
                var to = new DateTime(year, 12, 31);
                var json = await EAsistentService.GetAbsencesAsync(AuthState.AccessToken, from, to);
                PopulateAbsences(json);
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju izostankov. Poskusite znova.\n{ex.Message}");
            }
        }

        private void PopulateAbsences(JsonElement json)
        {
            AbsencesContainer.Children.Clear();
            SummaryPanel.Children.Clear();

            var items = new List<(string date, string subject, string type, string reason, int hours)>();
            try
            {
                JsonElement arr = default;
                if (json.TryGetProperty("absences", out var abs) && abs.ValueKind == JsonValueKind.Array)
                    arr = abs;
                else if (json.ValueKind == JsonValueKind.Array)
                    arr = json;

                if (arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        var date = GetStr(item, "date", "");
                        var subject = GetStr(item, "subject", GetStr(item, "subject_name", "Predmet"));
                        var type = GetStr(item, "type", GetStr(item, "justified", ""));
                        var reason = GetStr(item, "reason", GetStr(item, "excuse", ""));
                        var hours = 1;
                        if (item.TryGetProperty("hours", out var h) && h.ValueKind == JsonValueKind.Number)
                            hours = h.GetInt32();
                        items.Add((date, subject, type, reason, hours));
                    }
                }
            }
            catch { /* ignore */ }

            // Summary
            var totalHours = items.Sum(x => x.hours);
            var justifiedHours = items.Where(x => IsJustified(x.type)).Sum(x => x.hours);
            var unjustifiedHours = totalHours - justifiedHours;

            AddSummaryBadge("Skupaj ur", totalHours.ToString(), Color.FromRgb(70, 90, 130));
            AddSummaryBadge("Opravičenih", justifiedHours.ToString(), Color.FromRgb(0, 153, 76));
            AddSummaryBadge("Neopravičenih", unjustifiedHours.ToString(), Color.FromRgb(204, 0, 0));

            if (!items.Any())
            {
                AbsencesContainer.Children.Add(new TextBlock
                {
                    Text = "Ni izostankov za prikaz.",
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
                DateTime.TryParse(group.Key, out var groupDate);
                var dateStr = groupDate != default
                    ? groupDate.ToString("dddd, d. MMMM yyyy", new System.Globalization.CultureInfo("sl-SI"))
                    : group.Key;

                AbsencesContainer.Children.Add(new TextBlock
                {
                    Text = dateStr,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 130)),
                    Margin = new Thickness(0, 12, 0, 6)
                });

                foreach (var (_, subject, type, reason, hours) in group)
                {
                    var justified = IsJustified(type);
                    var card = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = justified
                            ? new SolidColorBrush(Color.FromRgb(198, 239, 206))
                            : new SolidColorBrush(Color.FromRgb(255, 199, 206)),
                        BorderThickness = new Thickness(1,1,1,1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(14, 10, 14, 10),
                        Margin = new Thickness(0, 0, 0, 6)
                    };

                    var row = new DockPanel();
                    var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    sp.Children.Add(new TextBlock
                    {
                        Text = subject,
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51))
                    });
                    if (!string.IsNullOrEmpty(reason))
                        sp.Children.Add(new TextBlock { Text = reason, FontSize = 12, Foreground = Brushes.Gray });
                    row.Children.Add(sp);

                    // Type badge on right
                    var typeColor = justified ? Color.FromRgb(0, 153, 76) : Color.FromRgb(204, 0, 0);
                    var typeBadge = new Border
                    {
                        Background = new SolidColorBrush(typeColor),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(10, 3, 10, 3),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0),
                        Child = new TextBlock
                        {
                            Text = justified ? "Opravičeno" : "Neopravičeno",
                            Foreground = Brushes.White,
                            FontSize = 11
                        }
                    };
                    DockPanel.SetDock(typeBadge, Dock.Right);
                    row.Children.Insert(0, typeBadge);

                    card.Child = row;
                    AbsencesContainer.Children.Add(card);
                }
            }
        }

        private static bool IsJustified(string type)
        {
            var t = type.ToLowerInvariant();
            return t.Contains("opravič") || t.Contains("justified") || t == "1" || t == "true";
        }

        private void AddSummaryBadge(string label, string value, Color color)
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(1,1,1,1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 12, 0)
            };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            badge.Child = sp;
            SummaryPanel.Children.Add(badge);
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
