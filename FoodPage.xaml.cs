using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class FoodPage : Page
    {
        private DateTime _currentMonday;

        public FoodPage()
        {
            InitializeComponent();
            _currentMonday = GetMonday(DateTime.Today);
            Loaded += FoodPage_Loaded;
        }

        private static DateTime GetMonday(DateTime date)
        {
            int dow = (int)date.DayOfWeek;
            return date.AddDays(dow == 0 ? -6 : -(dow - 1));
        }

        private async void FoodPage_Loaded(object sender, RoutedEventArgs e)
            => await LoadWeekAsync();

        private async Task LoadWeekAsync()
        {
            UpdateWeekLabel();
            ShowLoading();
            try
            {
                var to = _currentMonday.AddDays(4);
                var json = await EAsistentService.GetSchoolCateringAsync(AuthState.AccessToken, _currentMonday, to);
                PopulateFood(json);
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju prehrane. Poskusite znova.\n{ex.Message}");
            }
        }

        private void UpdateWeekLabel()
        {
            var to = _currentMonday.AddDays(4);
            WeekLabel.Text = $"{_currentMonday:dd.MM.yyyy}  –  {to:dd.MM.yyyy}";
        }

        private void PopulateFood(JsonElement json)
        {
            FoodContainer.Children.Clear();

            // Find the days array in various response shapes
            JsonElement days = default;
            if (json.TryGetProperty("days", out var d) && d.ValueKind == JsonValueKind.Array)
                days = d;
            else if (json.TryGetProperty("school_catering", out var sc) && sc.ValueKind == JsonValueKind.Array)
                days = sc;
            else if (json.ValueKind == JsonValueKind.Array)
                days = json;

            if (days.ValueKind != JsonValueKind.Array || days.GetArrayLength() == 0)
            {
                FoodContainer.Children.Add(new TextBlock
                {
                    Text = "Ni podatkov o prehrani za ta teden.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(4,4,4,4)
                });
                return;
            }

            foreach (var day in days.EnumerateArray())
            {
                var dateStr = GetStr(day, "date", "");
                DateTime.TryParse(dateStr, out var dateObj);

                var card = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                    BorderThickness = new Thickness(1,1,1,1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16,16,16,16),
                    Margin = new Thickness(0, 0, 0, 12)
                };

                var sp = new StackPanel();
                var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

                string[] sloDayNames = { "Nedelja", "Ponedeljek", "Torek", "Sreda", "Četrtek", "Petek", "Sobota" };
                var dayName = dateObj != default ? sloDayNames[(int)dateObj.DayOfWeek] : "";
                var isToday = dateObj.Date == DateTime.Today;

                headerRow.Children.Add(new TextBlock
                {
                    Text = $"{dayName}, {dateStr}",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = isToday
                        ? new SolidColorBrush(Color.FromRgb(0, 102, 204))
                        : new SolidColorBrush(Color.FromRgb(28, 35, 51))
                });
                if (isToday)
                    headerRow.Children.Add(new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(8, 3, 8, 3),
                        Margin = new Thickness(10, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock { Text = "Danes", Foreground = Brushes.White, FontSize = 11 }
                    });

                sp.Children.Add(headerRow);
                sp.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                    Margin = new Thickness(0, 0, 0, 10)
                });

                // Find menus array
                JsonElement menus = default;
                if (day.TryGetProperty("menus", out var m) && m.ValueKind == JsonValueKind.Array)
                    menus = m;
                else if (day.TryGetProperty("items", out var it) && it.ValueKind == JsonValueKind.Array)
                    menus = it;

                if (menus.ValueKind == JsonValueKind.Array && menus.GetArrayLength() > 0)
                {
                    foreach (var menu in menus.EnumerateArray())
                    {
                        var name = GetStr(menu, "name", GetStr(menu, "title", ""));
                        var price = GetStr(menu, "price", "");
                        var type = GetStr(menu, "type", "");

                        var menuRow = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

                        var nameSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                        if (!string.IsNullOrEmpty(type))
                            nameSp.Children.Add(new TextBlock { Text = type, FontSize = 11, Foreground = Brushes.Gray });
                        nameSp.Children.Add(new TextBlock { Text = name, FontSize = 13, TextWrapping = TextWrapping.Wrap });
                        menuRow.Children.Add(nameSp);

                        if (!string.IsNullOrEmpty(price))
                        {
                            var priceTb = new TextBlock
                            {
                                Text = $"{price} €",
                                FontSize = 13,
                                FontWeight = FontWeights.Bold,
                                Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            DockPanel.SetDock(priceTb, Dock.Right);
                            menuRow.Children.Insert(0, priceTb);
                        }

                        sp.Children.Add(menuRow);
                    }
                }
                else
                {
                    sp.Children.Add(new TextBlock { Text = "Ni menija.", Foreground = Brushes.Gray, FontSize = 13 });
                }

                card.Child = sp;
                FoodContainer.Children.Add(card);
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
