using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EAClient.Models;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class TimetablePage : Page
    {
        private DateTime _currentMonday;

        public TimetablePage()
        {
            InitializeComponent();
            _currentMonday = GetMonday(DateTime.Today);
            Loaded += TimetablePage_Loaded;
            Unloaded += TimetablePage_Unloaded;
            PreferencesService.PreferencesChanged += OnPreferencesChanged;
        }

        private async void TimetablePage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTheme();
            await LoadWeekAsync();
        }

        private void TimetablePage_Unloaded(object sender, RoutedEventArgs e)
        {
            PreferencesService.PreferencesChanged -= OnPreferencesChanged;
        }

        private void OnPreferencesChanged(UserPreferences _)
        {
            Dispatcher.Invoke(ApplyTheme);
        }

        private void ApplyTheme()
        {
            RootGrid.Background = AppTheme.BgBrush;
            TitleBar.Background = AppTheme.TitleBarBrush;
            WeekNavBar.Background = AppTheme.CardBrush;
            WeekNavBar.BorderBrush = AppTheme.BorderBrush;
            WeekLabel.Foreground = AppTheme.TextBrush;
            FontFamily = new FontFamily(AppTheme.FontFamily);
        }

        private static DateTime GetMonday(DateTime date)
        {
            int dow = (int)date.DayOfWeek;
            int daysFromMonday = dow == 0 ? 6 : dow - 1;
            return date.AddDays(-daysFromMonday);
        }

        private async Task LoadWeekAsync()
        {
            UpdateWeekLabel();
            ShowLoading();
            try
            {
                var to = _currentMonday.AddDays(4);
                var eventsTask = EAsistentService.GetTimetableAsync(AuthState.AccessToken, _currentMonday, to);
                List<(string Date, string Subject, string Type, string Name)> evaluations = new();
                try
                {
                    var evalJson = await EAsistentService.GetEvaluationsRangeAsync(AuthState.AccessToken, _currentMonday, to);
                    evaluations = ParseEvaluations(evalJson, _currentMonday, to);
                }
                catch { /* evaluations are optional */ }

                var events = await eventsTask;
                BuildTimetableGrid(events, _currentMonday, evaluations);
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju urnika. Poskusite znova.\n{ex.Message}");
            }
        }

        private static List<(string Date, string Subject, string Type, string Name)> ParseEvaluations(JsonElement json, DateTime from, DateTime to)
        {
            var result = new List<(string, string, string, string)>();
            JsonElement arr = default;

            if (json.TryGetProperty("evaluations", out var ev) && ev.ValueKind == JsonValueKind.Array)
                arr = ev;
            else if (json.ValueKind == JsonValueKind.Array)
                arr = json;

            if (arr.ValueKind != JsonValueKind.Array) return result;

            foreach (var item in arr.EnumerateArray())
            {
                var dateStr = GetStrEval(item, "date", string.Empty);
                if (!DateTime.TryParse(dateStr, out var dateVal)) continue;
                if (dateVal.Date < from.Date || dateVal.Date > to.Date) continue;

                var subject = GetStrEval(item, "subject", GetStrEval(item, "subject_name", "Test"));
                var type = GetStrEval(item, "type", GetStrEval(item, "kind", string.Empty));
                var name = GetStrEval(item, "name", GetStrEval(item, "description", GetStrEval(item, "title", string.Empty)));
                result.Add((dateStr, subject, type, name));
            }

            return result;
        }

        private static string GetStrEval(JsonElement el, string prop, string fallback)
        {
            if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? fallback;
            return fallback;
        }

        private void UpdateWeekLabel()
        {
            var to = _currentMonday.AddDays(4);
            WeekLabel.Text = $"{_currentMonday:dd.MM.yyyy}  –  {to:dd.MM.yyyy}";
        }

        private void BuildTimetableGrid(List<TimetableEvent> events, DateTime monday,
            List<(string Date, string Subject, string Type, string Name)> evaluations)
        {
            TimetableGrid.Children.Clear();
            TimetableGrid.RowDefinitions.Clear();
            TimetableGrid.ColumnDefinitions.Clear();

            bool hasEvaluations = evaluations.Any();

            if (!events.Any() && !hasEvaluations)
            {
                TimetableGrid.Children.Add(new TextBlock
                {
                    Text = "Ta teden ni pouka.",
                    FontSize = 16,
                    Foreground = AppTheme.SubTextBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20)
                });
                return;
            }

            var timeSlots = events
                .Select(e => (e.From, e.To))
                .Distinct()
                .OrderBy(t => t.From)
                .ToList();

            var headerCornerRadius = AppTheme.IsSleek ? 18d : 0d;
            var cellCornerRadius = AppTheme.IsSleek ? 16d : 4d;
            var eventCornerRadius = AppTheme.IsSleek ? 16d : 4d;
            var cellSpacing = AppTheme.IsSleek ? 8d : 0d;

            TimetableGrid.Margin = new Thickness(AppTheme.IsSleek ? 16 : 10);
            TimetableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            for (int d = 0; d < 5; d++)
                TimetableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 150 });

            // Row 0: headers; rows 1..n: time slots; row n+1 (optional): evaluations
            TimetableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(AppTheme.IsSleek ? 58 : 46) });
            foreach (var _ in timeSlots)
                TimetableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(AppTheme.IsSleek ? 88 : 68) });
            if (hasEvaluations)
                TimetableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var corner = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 26, 40)),
                CornerRadius = new CornerRadius(headerCornerRadius, 0, 0, 0),
                Margin = new Thickness(0, 0, cellSpacing, cellSpacing)
            };
            Grid.SetRow(corner, 0);
            Grid.SetColumn(corner, 0);
            TimetableGrid.Children.Add(corner);

            string[] dayNames = { "Ponedeljek", "Torek", "Sreda", "Četrtek", "Petek" };
            for (int d = 0; d < 5; d++)
            {
                var date = monday.AddDays(d);
                var isToday = date.Date == DateTime.Today;
                var bg = isToday
                    ? new SolidColorBrush(Color.FromRgb(0, 102, 204))
                    : new SolidColorBrush(Color.FromRgb(28, 35, 51));
                var header = new Border
                {
                    Background = bg,
                    CornerRadius = new CornerRadius(
                        d == 0 ? headerCornerRadius : 0,
                        d == 4 ? headerCornerRadius : 0,
                        0,
                        0),
                    Margin = new Thickness(0, 0, d == 4 ? 0 : cellSpacing, cellSpacing),
                    Padding = new Thickness(10)
                };
                header.Child = new TextBlock
                {
                    Text = $"{dayNames[d]}\n{date:dd.MM.}",
                    Foreground = Brushes.White,
                    FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(header, 0);
                Grid.SetColumn(header, d + 1);
                TimetableGrid.Children.Add(header);
            }

            for (int i = 0; i < timeSlots.Count; i++)
            {
                var (from, to) = timeSlots[i];
                int row = i + 1;

                var timeBorder = new Border
                {
                    Background = AppTheme.IsDark
                        ? new SolidColorBrush(Color.FromRgb(35, 42, 58))
                        : (AppTheme.IsSleek ? new SolidColorBrush(Color.FromRgb(250, 251, 253)) : new SolidColorBrush(Color.FromRgb(245, 247, 250))),
                    BorderBrush = AppTheme.IsDark ? AppTheme.BorderBrush : new SolidColorBrush(Color.FromRgb(220, 225, 235)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(cellCornerRadius),
                    Margin = new Thickness(0, 0, cellSpacing, row == timeSlots.Count && !hasEvaluations ? 0 : cellSpacing),
                    Padding = new Thickness(8)
                };
                timeBorder.Child = new TextBlock
                {
                    Text = $"{from}\n{to}",
                    FontSize = 11,
                    Foreground = AppTheme.SubTextBrush,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(timeBorder, row);
                Grid.SetColumn(timeBorder, 0);
                TimetableGrid.Children.Add(timeBorder);

                for (int d = 0; d < 5; d++)
                {
                    var date = monday.AddDays(d);
                    var isToday = date.Date == DateTime.Today;
                    var isCurrent = isToday && IsCurrentHour(from, to);
                    var dateStr = date.ToString("yyyy-MM-dd");
                    var ev = events.FirstOrDefault(e => e.Date == dateStr && e.From == from);

                    Color cellBg = isCurrent
                        ? (AppTheme.IsDark ? Color.FromRgb(80, 64, 20) : Color.FromRgb(255, 247, 221))
                        : isToday
                            ? (AppTheme.IsDark ? Color.FromRgb(20, 40, 70) : Color.FromRgb(241, 247, 255))
                            : AppTheme.IsDark
                                ? Color.FromRgb(28, 35, 51)
                                : (AppTheme.IsSleek ? Color.FromRgb(252, 253, 255) : Colors.White);

                    var cell = new Border
                    {
                        Background = new SolidColorBrush(cellBg),
                        BorderBrush = AppTheme.IsDark ? AppTheme.BorderBrush : new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(cellCornerRadius),
                        Margin = new Thickness(0, 0, d == 4 ? 0 : cellSpacing, row == timeSlots.Count && !hasEvaluations ? 0 : cellSpacing),
                        Padding = new Thickness(AppTheme.IsSleek ? 8 : 6)
                    };

                    if (ev != null)
                    {
                        Color evColor;
                        try { evColor = (Color)ColorConverter.ConvertFromString(ev.Color); }
                        catch { evColor = Color.FromRgb(75, 75, 191); }

                        var evBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(AppTheme.IsSleek ? (byte)40 : (byte)28, evColor.R, evColor.G, evColor.B)),
                            BorderBrush = new SolidColorBrush(evColor),
                            BorderThickness = new Thickness(AppTheme.IsSleek ? 1.5 : 3, AppTheme.IsSleek ? 1.5 : 0, AppTheme.IsSleek ? 1.5 : 0, AppTheme.IsSleek ? 1.5 : 0),
                            CornerRadius = new CornerRadius(eventCornerRadius),
                            Padding = new Thickness(AppTheme.IsSleek ? 10 : 7, AppTheme.IsSleek ? 8 : 4, AppTheme.IsSleek ? 10 : 5, AppTheme.IsSleek ? 8 : 4)
                        };
                        var sp = new StackPanel();
                        sp.Children.Add(new TextBlock
                        {
                            Text = ev.Title,
                            FontWeight = FontWeights.Bold,
                            FontSize = 12,
                            Foreground = new SolidColorBrush(evColor),
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                        if (!string.IsNullOrEmpty(ev.Classroom))
                            sp.Children.Add(new TextBlock
                            {
                                Text = ev.Classroom,
                                FontSize = 10,
                                Foreground = AppTheme.SubTextBrush
                            });
                        if (ev.Teachers.Any())
                            sp.Children.Add(new TextBlock
                            {
                                Text = ev.Teachers[0],
                                FontSize = 10,
                                Foreground = AppTheme.SubTextBrush,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            });
                        evBorder.Child = sp;
                        cell.Child = evBorder;
                    }

                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, d + 1);
                    TimetableGrid.Children.Add(cell);
                }
            }

            // Evaluations/tests row
            if (hasEvaluations)
            {
                int evalRow = timeSlots.Count + 1;

                // Label cell
                var labelBorder = new Border
                {
                    Background = AppTheme.IsDark ? new SolidColorBrush(Color.FromRgb(35, 42, 58)) : new SolidColorBrush(Color.FromRgb(245, 247, 250)),
                    BorderBrush = AppTheme.IsDark ? AppTheme.BorderBrush : new SolidColorBrush(Color.FromRgb(220, 225, 235)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(cellCornerRadius),
                    Margin = new Thickness(0, 0, cellSpacing, 0),
                    Padding = new Thickness(8)
                };
                labelBorder.Child = new TextBlock
                {
                    Text = "Testi",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = AppTheme.SubTextBrush,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(labelBorder, evalRow);
                Grid.SetColumn(labelBorder, 0);
                TimetableGrid.Children.Add(labelBorder);

                for (int d = 0; d < 5; d++)
                {
                    var date = monday.AddDays(d);
                    var dateStr = date.ToString("yyyy-MM-dd");
                    var dayEvals = evaluations.Where(e => e.Date.StartsWith(dateStr)).ToList();

                    var evalCell = new Border
                    {
                        Background = dayEvals.Any()
                            ? (AppTheme.IsDark ? new SolidColorBrush(Color.FromRgb(60, 30, 10)) : new SolidColorBrush(Color.FromRgb(255, 245, 230)))
                            : (AppTheme.IsDark ? new SolidColorBrush(Color.FromRgb(28, 35, 51)) : new SolidColorBrush(Colors.White)),
                        BorderBrush = dayEvals.Any()
                            ? new SolidColorBrush(Color.FromRgb(220, 120, 0))
                            : (AppTheme.IsDark ? AppTheme.BorderBrush : new SolidColorBrush(Color.FromRgb(228, 232, 240))),
                        BorderThickness = new Thickness(dayEvals.Any() ? 2 : 1),
                        CornerRadius = new CornerRadius(cellCornerRadius),
                        Margin = new Thickness(0, 0, d == 4 ? 0 : cellSpacing, 0),
                        Padding = new Thickness(AppTheme.IsSleek ? 8 : 6),
                        MinHeight = 40
                    };

                    if (dayEvals.Any())
                    {
                        var sp = new StackPanel();
                        foreach (var (_, subject, type, name) in dayEvals)
                        {
                            var label = string.IsNullOrWhiteSpace(name) ? subject : $"{subject}: {name}";
                            if (!string.IsNullOrWhiteSpace(type)) label = $"[{type}] {label}";
                            sp.Children.Add(new TextBlock
                            {
                                Text = label,
                                FontSize = 11,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = AppTheme.IsDark
                                    ? new SolidColorBrush(Color.FromRgb(255, 160, 60))
                                    : new SolidColorBrush(Color.FromRgb(200, 100, 0)),
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 0, 0, 2)
                            });
                        }
                        evalCell.Child = sp;
                    }

                    Grid.SetRow(evalCell, evalRow);
                    Grid.SetColumn(evalCell, d + 1);
                    TimetableGrid.Children.Add(evalCell);
                }
            }
        }

        private static bool IsCurrentHour(string from, string to)
        {
            var now = DateTime.Now.TimeOfDay;
            if (TimeSpan.TryParse(from, out var f) && TimeSpan.TryParse(to, out var t))
                return now >= f && now < t;
            return false;
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

        private async void PrevWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentMonday = _currentMonday.AddDays(-7);
            await LoadWeekAsync();
        }

        private async void NextWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentMonday = _currentMonday.AddDays(7);
            await LoadWeekAsync();
        }

        private async void Retry_Click(object sender, RoutedEventArgs e)
            => await LoadWeekAsync();
    }
}
