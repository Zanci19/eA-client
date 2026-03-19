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
        private DateTime _selectedDate;
        private List<JsonElement> _dayEntries = new();

        public FoodPage()
        {
            InitializeComponent();
            _currentMonday = GetMonday(DateTime.Today);
            _selectedDate = DateTime.Today;
            Loaded += FoodPage_Loaded;
        }

        private static DateTime GetMonday(DateTime date)
        {
            int dow = (int)date.DayOfWeek;
            return date.AddDays(dow == 0 ? -6 : -(dow - 1));
        }

        private async void FoodPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTheme();
            await LoadWeekAsync();
        }

        private async Task LoadWeekAsync()
        {
            UpdateHeader();
            ShowLoading();
            try
            {
                var to = _currentMonday.AddDays(4);
                var json = await EAsistentService.GetSchoolCateringAsync(AuthState.AccessToken, _currentMonday, to);
                _dayEntries = ExtractDayEntries(json).ToList();
                if (_selectedDate < _currentMonday || _selectedDate > to)
                {
                    _selectedDate = _currentMonday;
                }
                PopulateSelectedDay();
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju prehrane. Poskusite znova.\n{ex.Message}");
            }
        }

        private void UpdateHeader()
        {
            WeekLabel.Text = $"{_selectedDate:dddd, dd.MM.yyyy}";
            PrevDayButton.Content = _selectedDate <= _currentMonday ? "◀  Prejšnji teden" : "◀  Prejšnji dan";
            NextDayButton.Content = _selectedDate >= _currentMonday.AddDays(4) ? "Naslednji teden  ▶" : "Naslednji dan  ▶";
        }

        private void PopulateSelectedDay()
        {
            UpdateHeader();
            FoodContainer.Children.Clear();

            var match = _dayEntries.FirstOrDefault(day => DateMatches(day, _selectedDate));
            if (match.ValueKind == JsonValueKind.Undefined)
            {
                FoodContainer.Children.Add(BuildEmptyCard());
                return;
            }

            var card = BuildDayCard(match);
            FoodContainer.Children.Add(card);
            AnimationHelper.FadeInFromBelow(card, 260);
        }

        private IEnumerable<JsonElement> ExtractDayEntries(JsonElement json)
        {
            if (json.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                return items.EnumerateArray();
            if (json.TryGetProperty("days", out var days) && days.ValueKind == JsonValueKind.Array)
                return days.EnumerateArray();
            if (json.TryGetProperty("school_catering", out var schoolCatering) && schoolCatering.ValueKind == JsonValueKind.Array)
                return schoolCatering.EnumerateArray();
            return json.ValueKind == JsonValueKind.Array ? json.EnumerateArray() : Enumerable.Empty<JsonElement>();
        }

        private bool DateMatches(JsonElement day, DateTime date)
            => DateTime.TryParse(GetStr(day, "date", string.Empty), out var parsed) && parsed.Date == date.Date;

        private Border BuildEmptyCard()
        {
            var card = CreateCard();
            card.Child = new TextBlock
            {
                Text = "Za izbrani dan ni objavljenega menija.",
                Foreground = AppTheme.SubTextBrush,
                FontSize = 14
            };
            return card;
        }

        private Border BuildDayCard(JsonElement day)
        {
            var card = CreateCard();
            var stack = new StackPanel();
            var dateObj = DateTime.TryParse(GetStr(day, "date", string.Empty), out var parsed) ? parsed : _selectedDate;

            stack.Children.Add(new TextBlock
            {
                Text = dateObj.ToString("dddd, dd.MM.yyyy"),
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = AppTheme.TextBrush,
                Margin = new Thickness(0, 0, 0, 4)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Vsak zaslon pokaže en dan, menije pa lahko tudi prijaviš neposredno iz aplikacije.",
                FontSize = 13,
                Foreground = AppTheme.SubTextBrush,
                Margin = new Thickness(0, 0, 0, 18)
            });

            var rendered = AddMealSections(stack, day, dateObj);
            if (!rendered)
            {
                stack.Children.Add(new TextBlock { Text = "Ni menija.", Foreground = AppTheme.SubTextBrush, FontSize = 13 });
            }

            card.Child = stack;
            return card;
        }

        private bool AddMealSections(Panel container, JsonElement day, DateTime date)
        {
            if (!day.TryGetProperty("menus", out var menus))
            {
                return AddFlatMenus(container, FindMenus(day), string.Empty, date);
            }

            if (menus.ValueKind == JsonValueKind.Array)
            {
                return AddFlatMenus(container, menus, string.Empty, date);
            }

            if (menus.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var rendered = false;
            foreach (var mealType in MealTypeLabels)
            {
                if (!menus.TryGetProperty(mealType.Key, out var entries) || entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() == 0)
                    continue;

                rendered = true;
                container.Children.Add(BuildSectionHeader(mealType.Value));
                rendered |= AddFlatMenus(container, entries, mealType.Key, date);
            }

            return rendered;
        }

        private JsonElement FindMenus(JsonElement day)
        {
            if (day.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                return items;
            if (day.TryGetProperty("menus", out var menus) && menus.ValueKind == JsonValueKind.Array)
                return menus;
            return default;
        }

        private bool AddFlatMenus(Panel container, JsonElement menus, string typeOverride, DateTime date)
        {
            if (menus.ValueKind != JsonValueKind.Array)
                return false;

            var rendered = false;
            foreach (var menu in menus.EnumerateArray())
            {
                container.Children.Add(BuildMenuRow(menu, typeOverride, date));
                rendered = true;
            }

            return rendered;
        }

        private UIElement BuildSectionHeader(string text)
        {
            return new Border
            {
                Background = AppTheme.IsSleek ? new SolidColorBrush(Color.FromArgb(30, 0, 102, 204)) : Brushes.Transparent,
                CornerRadius = new CornerRadius(AppTheme.ButtonCornerRadius),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = AppTheme.AccentBrush
                }
            };
        }

        private UIElement BuildMenuRow(JsonElement menu, string typeOverride, DateTime date)
        {
            var type = string.IsNullOrWhiteSpace(typeOverride) ? GetStr(menu, "type", "snack") : typeOverride;
            var menuId = GetInt(menu, "menu", GetInt(menu, "id", 0));
            var name = GetStr(menu, "name", GetStr(menu, "title", "Meni"));
            var price = GetStr(menu, "price", string.Empty);
            var description = NormalizeDescription(GetStr(menu, "description", GetStr(menu, "meal", string.Empty)));
            var selected = IsMenuSelected(menu);

            var row = new Border
            {
                Background = AppTheme.IsSleek ? new SolidColorBrush(Color.FromArgb(32, 0, 102, 204)) : new SolidColorBrush(Color.FromRgb(248, 250, 253)),
                CornerRadius = new CornerRadius(AppTheme.CardCornerRadius),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12),
                BorderBrush = selected ? AppTheme.AccentBrush : AppTheme.BorderBrush,
                BorderThickness = new Thickness(selected ? 1.5 : 1)
            };

            var stack = new StackPanel();
            var top = new DockPanel();
            var chooseButton = new Button
            {
                Content = selected ? "Prijavljen" : "Prijavi meni",
                Padding = new Thickness(14, 8, 14, 8),
                Background = selected ? AppTheme.AccentBrush : AppTheme.CardBrush,
                Foreground = selected ? Brushes.White : AppTheme.TextBrush,
                BorderBrush = AppTheme.BorderBrush,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                IsEnabled = menuId > 0 && !selected,
                Tag = (type, date, menuId)
            };
            chooseButton.Click += ChooseMenu_Click;
            DockPanel.SetDock(chooseButton, Dock.Right);
            top.Children.Add(chooseButton);
            top.Children.Add(new TextBlock { Text = name, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = AppTheme.TextBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0) });
            stack.Children.Add(top);

            var meta = new string[]
            {
                MealTypeLabels.TryGetValue(type, out var label) ? label : type,
                string.IsNullOrWhiteSpace(price) ? string.Empty : $"{price} €",
                menuId > 0 ? $"ID menija: {menuId}" : string.Empty
            }.Where(x => !string.IsNullOrWhiteSpace(x));
            stack.Children.Add(new TextBlock { Text = string.Join("  •  ", meta), FontSize = 12, Foreground = AppTheme.SubTextBrush, Margin = new Thickness(0, 6, 0, 0) });

            if (!string.IsNullOrWhiteSpace(description))
            {
                stack.Children.Add(new TextBlock { Text = description, FontSize = 13, Foreground = AppTheme.SubTextBrush, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });
            }

            row.Child = stack;
            AnimationHelper.FadeInFromBelow(row, 240);
            return row;
        }

        private async void ChooseMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ValueTuple<string, DateTime, int> data)
                return;

            var original = button.Content;
            button.IsEnabled = false;
            button.Content = "Pošiljanje...";
            try
            {
                await EAsistentService.SelectMealMenuAsync(AuthState.AccessToken, data.Item1, data.Item2, data.Item3);
                ApplyMenuSelection(data.Item2, data.Item1, data.Item3);
                button.Content = "Prijavljen";
                PopulateSelectedDay();
                _ = RefreshCurrentWeekSilentlyAsync(data.Item2, data.Item1, data.Item3);
            }
            catch (Exception ex)
            {
                button.Content = original;
                button.IsEnabled = true;
                MessageBox.Show($"Prijava na meni ni uspela.\n{ex.Message}", "Prehrana", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task RefreshCurrentWeekSilentlyAsync(DateTime selectedDate, string type, int selectedMenuId)
        {
            try
            {
                var to = _currentMonday.AddDays(4);
                var json = await EAsistentService.GetSchoolCateringAsync(AuthState.AccessToken, _currentMonday, to);
                _dayEntries = ExtractDayEntries(json).ToList();
                ApplyMenuSelection(selectedDate, type, selectedMenuId);
                PopulateSelectedDay();
            }
            catch
            {
            }
        }

        private void ApplyMenuSelection(DateTime date, string type, int selectedMenuId)
        {
            var updatedDays = new List<JsonElement>();

            foreach (var day in _dayEntries)
            {
                if (!DateMatches(day, date))
                {
                    updatedDays.Add(day);
                    continue;
                }

                updatedDays.Add(UpdateMenusForDay(day, type, selectedMenuId));
            }

            _dayEntries = updatedDays;
        }

        private JsonElement UpdateMenusForDay(JsonElement day, string type, int selectedMenuId)
        {
            using var document = JsonDocument.Parse(day.GetRawText());
            var map = JsonSerializer.Deserialize<Dictionary<string, object?>>(document.RootElement.GetRawText()) ?? new();

            if (map.TryGetValue("menus", out var menusObj))
            {
                var normalizedType = string.IsNullOrWhiteSpace(type) ? string.Empty : type;
                var json = JsonSerializer.Serialize(menusObj);
                using var menusDoc = JsonDocument.Parse(json);

                if (menusDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    map["menus"] = UpdateMenuArray(menusDoc.RootElement, normalizedType, selectedMenuId);
                }
                else if (menusDoc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var nested = JsonSerializer.Deserialize<Dictionary<string, object?>>(menusDoc.RootElement.GetRawText()) ?? new();
                    foreach (var key in nested.Keys.ToList())
                    {
                        using var keyDoc = JsonDocument.Parse(JsonSerializer.Serialize(nested[key]));
                        if (keyDoc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            nested[key] = UpdateMenuArray(keyDoc.RootElement, key.Equals(type, StringComparison.OrdinalIgnoreCase) ? type : key, selectedMenuId);
                        }
                    }
                    map["menus"] = nested;
                }
            }

            var updatedJson = JsonSerializer.Serialize(map);
            return JsonDocument.Parse(updatedJson).RootElement.Clone();
        }

        private static List<Dictionary<string, object?>> UpdateMenuArray(JsonElement array, string type, int selectedMenuId)
        {
            var updated = new List<Dictionary<string, object?>>();
            foreach (var item in array.EnumerateArray())
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText()) ?? new();
                var itemType = GetStr(item, "type", type);
                var menuId = GetInt(item, "menu", GetInt(item, "id", 0));
                var isTargetType = string.IsNullOrWhiteSpace(type) || string.Equals(itemType, type, StringComparison.OrdinalIgnoreCase);
                var selected = isTargetType && menuId == selectedMenuId;
                dict["selected"] = selected;
                dict["is_selected"] = selected;
                dict["chosen"] = selected;
                dict["is_primary"] = selected;
                updated.Add(dict);
            }
            return updated;
        }

        private static bool IsMenuSelected(JsonElement menu)
            => GetBool(menu, "selected")
               || GetBool(menu, "is_primary")
               || GetBool(menu, "chosen")
               || GetBool(menu, "is_selected")
               || GetBool(menu, "active")
               || GetBool(menu, "enrolled")
               || GetBool(menu, "signed_up")
               || GetBool(menu, "subscribed");

        private static string NormalizeDescription(string text)
            => text.Replace("\r", string.Empty).Replace("\n", Environment.NewLine).Trim();

        private static string GetStr(JsonElement el, string prop, string fallback)
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString() ?? fallback;
                if (v.ValueKind == JsonValueKind.Number) return v.GetRawText();
            }
            return fallback;
        }

        private static int GetInt(JsonElement el, string prop, int fallback)
            => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var number) ? number : fallback;

        private static bool GetBool(JsonElement el, string prop)
        {
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(prop, out var v))
                return false;

            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => v.TryGetInt32(out var number) && number == 1,
                JsonValueKind.String => bool.TryParse(v.GetString(), out var parsed) && parsed,
                _ => false
            };
        }

        private Border CreateCard()
            => new()
            {
                Background = AppTheme.CardBrush,
                BorderBrush = AppTheme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(AppTheme.CardCornerRadius),
                Padding = new Thickness(22)
            };

        private void ApplyTheme()
        {
            RootGrid.Background = AppTheme.BgBrush;
            TitleBar.Background = AppTheme.TitleBarBrush;
            NavBar.Background = AppTheme.CardBrush;
            NavBar.BorderBrush = AppTheme.BorderBrush;
            WeekLabel.Foreground = AppTheme.TextBrush;
            LoadingText.Foreground = AppTheme.SubTextBrush;
            FontFamily = new FontFamily(AppTheme.FontFamily);

            foreach (var button in new[] { PrevDayButton, NextDayButton, RetryButton })
            {
                button.Background = button == RetryButton ? AppTheme.AccentBrush : (AppTheme.IsSleek ? new SolidColorBrush(Color.FromArgb(32, 0, 102, 204)) : new SolidColorBrush(Color.FromRgb(232, 240, 254)));
                button.Foreground = button == RetryButton ? Brushes.White : AppTheme.AccentBrush;
                button.BorderThickness = new Thickness(0);
            }
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

        private async void PrevDay_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDate <= _currentMonday)
            {
                _currentMonday = _currentMonday.AddDays(-7);
                _selectedDate = _currentMonday.AddDays(4);
                await LoadWeekAsync();
                return;
            }
            _selectedDate = _selectedDate.AddDays(-1);
            PopulateSelectedDay();
        }

        private async void NextDay_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDate >= _currentMonday.AddDays(4))
            {
                _currentMonday = _currentMonday.AddDays(7);
                _selectedDate = _currentMonday;
                await LoadWeekAsync();
                return;
            }
            _selectedDate = _selectedDate.AddDays(1);
            PopulateSelectedDay();
        }

        private async void Retry_Click(object sender, RoutedEventArgs e) => await LoadWeekAsync();
    }
}
