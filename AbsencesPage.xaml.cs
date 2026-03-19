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

            var items = ExtractAbsences(json).ToList();

            var totalHours = items.Sum(x => x.hours);
            var justifiedHours = items.Where(x => IsJustified(x.type, x.raw)).Sum(x => x.hours);
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
                    Margin = new Thickness(4, 4, 4, 4)
                });
                return;
            }

            var grouped = items
                .GroupBy(x => x.date)
                .OrderByDescending(g => ParseDate(g.Key));

            foreach (var group in grouped)
            {
                var groupDate = ParseDate(group.Key);
                var dateStr = groupDate != default
                    ? groupDate.ToString("dddd, d. MMMM yyyy", new CultureInfo("sl-SI"))
                    : group.Key;

                AbsencesContainer.Children.Add(new TextBlock
                {
                    Text = dateStr,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 130)),
                    Margin = new Thickness(0, 12, 0, 6)
                });

                foreach (var (_, subject, type, reason, hours, raw) in group)
                {
                    var justified = IsJustified(type, raw);
                    var card = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = justified
                            ? new SolidColorBrush(Color.FromRgb(198, 239, 206))
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
                        Text = subject,
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51))
                    });

                    var details = new List<string>();
                    if (!string.IsNullOrWhiteSpace(reason)) details.Add(reason);
                    if (hours > 0) details.Add(hours == 1 ? "1 ura" : $"{hours} ur");
                    if (details.Count > 0)
                    {
                        sp.Children.Add(new TextBlock { Text = string.Join("  •  ", details), FontSize = 12, Foreground = Brushes.Gray });
                    }
                    row.Children.Add(sp);

                    var typeBadge = new Border
                    {
                        Background = new SolidColorBrush(justified ? Color.FromRgb(0, 153, 76) : Color.FromRgb(204, 0, 0)),
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

        private IEnumerable<(string date, string subject, string type, string reason, int hours, JsonElement raw)> ExtractAbsences(JsonElement json)
        {
            foreach (var item in EnumerateObjectsDeep(json))
            {
                var date = GetStr(item, "date", GetStr(item, "absence_date", string.Empty));
                if (string.IsNullOrWhiteSpace(date))
                {
                    continue;
                }

                var subject = GetStr(item, "subject", GetStr(item, "subject_name", GetNestedStr(item, new[] { "subject", "name" }, "Predmet")));
                var type = GetStr(item, "type", GetStr(item, "absence_type", GetStr(item, "justified", string.Empty)));
                var reason = GetStr(item, "reason", GetStr(item, "excuse", GetStr(item, "description", string.Empty)));
                var hours =
                    GetInt(item, "hours", 0) > 0 ? GetInt(item, "hours", 0) :
                    GetInt(item, "duration", 0) > 0 ? GetInt(item, "duration", 0) :
                    GetInt(item, "lessons", 0) > 0 ? GetInt(item, "lessons", 0) : 1;

                yield return (date, subject, type, reason, hours, item);
            }
        }

        private static IEnumerable<JsonElement> EnumerateObjectsDeep(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("absences", out var absences) && absences.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in absences.EnumerateArray())
                    {
                        foreach (var nested in EnumerateObjectsDeep(child))
                            yield return nested;
                    }
                    yield break;
                }

                if (HasLikelyAbsenceShape(element))
                {
                    yield return element;
                }

                foreach (var property in element.EnumerateObject())
                {
                    foreach (var nested in EnumerateObjectsDeep(property.Value))
                        yield return nested;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in EnumerateObjectsDeep(item))
                        yield return nested;
                }
            }
        }

        private static bool HasLikelyAbsenceShape(JsonElement element)
            => element.TryGetProperty("date", out _)
               && (element.TryGetProperty("subject", out _)
                   || element.TryGetProperty("subject_name", out _)
                   || element.TryGetProperty("hours", out _)
                   || element.TryGetProperty("justified", out _)
                   || element.TryGetProperty("absence_type", out _));

        private static bool IsJustified(string type, JsonElement raw)
        {
            var t = type.ToLowerInvariant();
            if (t.Contains("opravič") || t.Contains("justified") || t == "1" || t == "true")
                return true;
            if (t.Contains("neopravi") || t == "0" || t == "false")
                return false;

            return GetBool(raw, "justified")
                || GetBool(raw, "is_justified")
                || GetBool(raw, "excused")
                || GetBool(raw, "is_excused");
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

        private static string GetNestedStr(JsonElement el, string[] path, string fallback)
        {
            var current = el;
            foreach (var part in path)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
                    return fallback;
            }
            return current.ValueKind == JsonValueKind.String ? current.GetString() ?? fallback : current.ToString();
        }

        private static int GetInt(JsonElement el, string prop, int fallback)
            => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var value)
                ? value
                : fallback;

        private static bool GetBool(JsonElement el, string prop)
        {
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(prop, out var value))
                return false;

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => value.TryGetInt32(out var number) && number == 1,
                JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
                _ => false
            };
        }

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
