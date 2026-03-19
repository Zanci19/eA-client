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
    public partial class FoodPage : Page
    {
        private static readonly Dictionary<string, string> MealTypeLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["breakfast"] = "Zajtrk",
            ["snack"] = "Malica",
            ["lunch"] = "Kosilo",
            ["afternoon_snack"] = "Popoldanska malica"
        };

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

        private async void FoodPage_Loaded(object sender, RoutedEventArgs e) => await LoadWeekAsync();

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

            var dayEntries = ExtractDayEntries(json).ToList();
            if (dayEntries.Count == 0)
            {
                FoodContainer.Children.Add(new TextBlock
                {
                    Text = "Ni podatkov o prehrani za ta teden.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(4)
                });
                return;
            }

            foreach (var day in dayEntries)
            {
                FoodContainer.Children.Add(BuildDayCard(day));
            }
        }

        private IEnumerable<JsonElement> ExtractDayEntries(JsonElement json)
        {
            if (json.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                return items.EnumerateArray();
            }

            if (json.TryGetProperty("days", out var days) && days.ValueKind == JsonValueKind.Array)
            {
                return days.EnumerateArray();
            }

            if (json.TryGetProperty("school_catering", out var schoolCatering) && schoolCatering.ValueKind == JsonValueKind.Array)
            {
                return schoolCatering.EnumerateArray();
            }

            return json.ValueKind == JsonValueKind.Array
                ? json.EnumerateArray()
                : Enumerable.Empty<JsonElement>();
        }

        private Border BuildDayCard(JsonElement day)
        {
            var dateStr = GetStr(day, "date", string.Empty);
            DateTime.TryParse(dateStr, out var dateObj);

            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel();
            var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            string[] sloDayNames = { "Nedelja", "Ponedeljek", "Torek", "Sreda", "Četrtek", "Petek", "Sobota" };
            var dayName = dateObj != default ? sloDayNames[(int)dateObj.DayOfWeek] : string.Empty;
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
            {
                headerRow.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = "Danes", Foreground = Brushes.White, FontSize = 11 }
                });
            }

            stack.Children.Add(headerRow);
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var renderedSections = AddMealSections(stack, day);
            if (!renderedSections)
            {
                stack.Children.Add(new TextBlock { Text = "Ni menija.", Foreground = Brushes.Gray, FontSize = 13 });
            }

            card.Child = stack;
            return card;
        }

        private bool AddMealSections(Panel container, JsonElement day)
        {
            if (!day.TryGetProperty("menus", out var menus))
            {
                return AddLegacyMenus(container, day);
            }

            if (menus.ValueKind == JsonValueKind.Array)
            {
                return AddFlatMenus(container, menus);
            }

            if (menus.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var rendered = false;
            foreach (var mealType in MealTypeLabels)
            {
                if (!menus.TryGetProperty(mealType.Key, out var entries) || entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() == 0)
                {
                    continue;
                }

                rendered = true;
                container.Children.Add(new TextBlock
                {
                    Text = mealType.Value,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                    Margin = new Thickness(0, 6, 0, 6)
                });

                foreach (var menu in entries.EnumerateArray())
                {
                    container.Children.Add(BuildMenuRow(menu));
                }
            }

            return rendered;
        }

        private bool AddLegacyMenus(Panel container, JsonElement day)
        {
            if (day.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                return AddFlatMenus(container, items);
            }

            return false;
        }

        private bool AddFlatMenus(Panel container, JsonElement menus)
        {
            var rendered = false;
            foreach (var menu in menus.EnumerateArray())
            {
                container.Children.Add(BuildMenuRow(menu));
                rendered = true;
            }

            return rendered;
        }

        private UIElement BuildMenuRow(JsonElement menu)
        {
            var row = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 253)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();
            var titleRow = new DockPanel();
            var name = GetStr(menu, "name", GetStr(menu, "title", "Meni"));
            var price = GetStr(menu, "price", string.Empty);
            var isPrimary = GetBool(menu, "is_primary");

            var nameBlock = new TextBlock
            {
                Text = name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51)),
                TextWrapping = TextWrapping.Wrap
            };
            titleRow.Children.Add(nameBlock);

            if (!string.IsNullOrWhiteSpace(price))
            {
                var priceText = new TextBlock
                {
                    Text = $"{price} €",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(priceText, Dock.Right);
                titleRow.Children.Insert(0, priceText);
            }

            stack.Children.Add(titleRow);

            if (isPrimary)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "Privzeti izbor",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            var description = NormalizeDescription(GetStr(menu, "description", string.Empty));
            if (!string.IsNullOrWhiteSpace(description))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 12,
                    Foreground = Brushes.DimGray,
                    Margin = new Thickness(0, 6, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            row.Child = stack;
            return row;
        }

        private static string NormalizeDescription(string text)
        {
            return text
                .Replace("\r", string.Empty)
                .Replace("\n", Environment.NewLine)
                .Trim();
        }

        private static string GetStr(JsonElement el, string prop, string fallback)
        {
            if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
            {
                return v.GetString() ?? fallback;
            }

            return fallback;
        }

        private static bool GetBool(JsonElement el, string prop)
        {
            return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;
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

        private async void Retry_Click(object sender, RoutedEventArgs e) => await LoadWeekAsync();
    }
}
