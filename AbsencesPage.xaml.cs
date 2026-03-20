using System;
using System.Collections.Generic;
using System.Globalization;
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
                var today = DateTime.Today;
                var schoolYearStart = today.Month >= 8 ? today.Year : today.Year - 1;
                var from = new DateTime(schoolYearStart, 8, 1);
                var to = new DateTime(schoolYearStart + 1, 7, 31);
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

            // Read summary directly from API response
            int excusedHours = 0, unexcusedHours = 0, pendingHours = 0;
            if (json.TryGetProperty("summary", out var summaryEl))
            {
                excusedHours = GetInt(summaryEl, "excused_hours", 0);
                unexcusedHours = GetInt(summaryEl, "unexcused_hours", 0);
                pendingHours = GetInt(summaryEl, "pending_hours", 0);
            }

            AddSummaryBadge("Opravičenih", excusedHours.ToString(), Color.FromRgb(0, 153, 76));
            AddSummaryBadge("Neopravičenih", unexcusedHours.ToString(), Color.FromRgb(204, 0, 0));
            if (pendingHours > 0)
                AddSummaryBadge("V čakanju", pendingHours.ToString(), Color.FromRgb(200, 120, 0));

            // Read items array
            if (!json.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
            {
                AbsencesContainer.Children.Add(new TextBlock
                {
                    Text = "Ni izostankov za prikaz.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(4, 4, 4, 4)
                });
                return;
            }

            var items = itemsEl.EnumerateArray().ToList();
            if (items.Count == 0)
            {
                AbsencesContainer.Children.Add(new TextBlock
                {
                    Text = "Ni izostankov za prikaz.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(4, 4, 4, 4)
                });
                return;
            }

            foreach (var item in items)
            {
                var date = GetStr(item, "date", string.Empty);
                var groupDate = ParseDate(date);
                var dateStr = groupDate != default
                    ? groupDate.ToString("dddd, d. MMMM yyyy", new CultureInfo("sl-SI"))
                    : date;

                AbsencesContainer.Children.Add(new TextBlock
                {
                    Text = dateStr,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 130)),
                    Margin = new Thickness(0, 12, 0, 6)
                });

                if (!item.TryGetProperty("hours", out var hoursEl) || hoursEl.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var hour in hoursEl.EnumerateArray())
                {
                    var className = GetStr(hour, "class_name", "Predmet");
                    var state = GetStr(hour, "state", string.Empty).ToLowerInvariant();
                    var from = GetStr(hour, "from", string.Empty);
                    var to = GetStr(hour, "to", string.Empty);
                    var period = GetStr(hour, "value", string.Empty);

                    var justified = state == "excused";
                    var pending = state == "pending";

                    var card = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = justified
                            ? new SolidColorBrush(Color.FromRgb(198, 239, 206))
                            : pending
                                ? new SolidColorBrush(Color.FromRgb(255, 235, 156))
                                : new SolidColorBrush(Color.FromRgb(255, 199, 206)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(14, 10, 14, 10),
                        Margin = new Thickness(0, 0, 0, 6)
                    };

                    var row = new DockPanel();
                    var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    sp.Children.Add(new TextBlock
                    {
                        Text = className,
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51))
                    });

                    var details = new List<string>();
                    if (!string.IsNullOrWhiteSpace(period)) details.Add($"{period}. ura");
                    if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to)) details.Add($"{from}–{to}");
                    if (details.Count > 0)
                    {
                        sp.Children.Add(new TextBlock { Text = string.Join("  •  ", details), FontSize = 12, Foreground = Brushes.Gray });
                    }
                    row.Children.Add(sp);

                    var stateLabel = justified ? "Opravičeno" : pending ? "V čakanju" : "Neopravičeno";
                    var stateColor = justified ? Color.FromRgb(0, 153, 76) : pending ? Color.FromRgb(200, 120, 0) : Color.FromRgb(204, 0, 0);
                    var typeBadge = new Border
                    {
                        Background = new SolidColorBrush(stateColor),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(10, 3, 10, 3),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0),
                        Child = new TextBlock
                        {
                            Text = stateLabel,
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

        private void AddSummaryBadge(string label, string value, Color color)
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(1),
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
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v))
            {
                return v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : v.ToString();
            }
            return fallback;
        }

        private static int GetInt(JsonElement el, string prop, int fallback)
            => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var value)
                ? value
                : fallback;

        private static DateTime ParseDate(string input)
            => DateTime.TryParse(input, out var date) ? date : default;

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
