using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        private async void TimetablePage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadWeekAsync();
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
                var events = await EAsistentService.GetTimetableAsync(AuthState.AccessToken, _currentMonday, to);
                BuildTimetableGrid(events, _currentMonday);
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju urnika. Poskusite znova.\n{ex.Message}");
            }
        }

        private void UpdateWeekLabel()
        {
            var to = _currentMonday.AddDays(4);
            WeekLabel.Text = $"{_currentMonday:dd.MM.yyyy}  –  {to:dd.MM.yyyy}";
        }

        private void BuildTimetableGrid(List<TimetableEvent> events, DateTime monday)
        {
            TimetableGrid.Children.Clear();
            TimetableGrid.RowDefinitions.Clear();
            TimetableGrid.ColumnDefinitions.Clear();

            if (!events.Any())
            {
                TimetableGrid.Children.Add(new TextBlock
                {
                    Text = "Ta teden ni pouka.",
                    FontSize = 16,
                    Foreground = Brushes.Gray,
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

            TimetableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(AppTheme.IsSleek ? 58 : 46) });
            foreach (var _ in timeSlots)
                TimetableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(AppTheme.IsSleek ? 88 : 68) });

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
                    Background = AppTheme.IsSleek ? new SolidColorBrush(Color.FromRgb(250, 251, 253)) : new SolidColorBrush(Color.FromRgb(245, 247, 250)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(220, 225, 235)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(cellCornerRadius),
                    Margin = new Thickness(0, 0, cellSpacing, row == timeSlots.Count ? 0 : cellSpacing),
                    Padding = new Thickness(8)
                };
                timeBorder.Child = new TextBlock
                {
                    Text = $"{from}\n{to}",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
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
                        ? Color.FromRgb(255, 247, 221)
                        : isToday
                            ? Color.FromRgb(241, 247, 255)
                            : AppTheme.IsSleek ? Color.FromRgb(252, 253, 255) : Colors.White;

                    var cell = new Border
                    {
                        Background = new SolidColorBrush(cellBg),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(cellCornerRadius),
                        Margin = new Thickness(0, 0, d == 4 ? 0 : cellSpacing, row == timeSlots.Count ? 0 : cellSpacing),
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
                                Text = $"📍 {ev.Classroom}",
                                FontSize = 10,
                                Foreground = Brushes.Gray
                            });
                        if (ev.Teachers.Any())
                            sp.Children.Add(new TextBlock
                            {
                                Text = ev.Teachers[0],
                                FontSize = 10,
                                Foreground = Brushes.Gray,
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
