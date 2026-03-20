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
    public partial class PraisesPage : Page
    {
        public PraisesPage()
        {
            InitializeComponent();
            Loaded += PraisesPage_Loaded;
        }

        private async void PraisesPage_Loaded(object sender, RoutedEventArgs e)
            => await LoadDataAsync();

        private async Task LoadDataAsync()
        {
            ShowLoading();
            try
            {
                var json = await EAsistentService.GetPraisesAsync(AuthState.AccessToken);
                PopulateItems(json);
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju pohval. Poskusite znova.\n{ex.Message}");
            }
        }

        private void PopulateItems(JsonElement json)
        {
            ItemsContainer.Children.Clear();

            var items = new List<JsonElement>();
            if (json.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                    items.Add(item);
            }
            else if (json.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in json.EnumerateArray())
                    items.Add(item);
            }

            if (!items.Any())
            {
                ItemsContainer.Children.Add(new TextBlock
                {
                    Text = "Ni pohval ali izboljšav za prikaz.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(4, 4, 4, 4)
                });
                return;
            }

            var sorted = items.OrderByDescending(x =>
            {
                DateTime.TryParse(GetStr(x, "date", string.Empty), out var d);
                return d;
            }).ToList();

            foreach (var item in sorted)
                ItemsContainer.Children.Add(BuildCard(item));
        }

        private static Border BuildCard(JsonElement item)
        {
            var type = GetStr(item, "type", string.Empty);
            var category = GetStr(item, "category", string.Empty);
            var text = GetStr(item, "text", string.Empty);
            var course = GetStr(item, "course", string.Empty);
            var date = GetStr(item, "date", string.Empty);
            var author = GetStr(item, "author", string.Empty);
            var courseColor = GetStr(item, "course_color", "#0066CC");

            var isPraise = type.Equals("praise", StringComparison.OrdinalIgnoreCase);

            Color parsedColor;
            try { parsedColor = (Color)ColorConverter.ConvertFromString(courseColor); }
            catch { parsedColor = Color.FromRgb(0, 102, 204); }
            var accentBrush = new SolidColorBrush(parsedColor);

            var card = new Border
            {
                Background = isPraise
                    ? new SolidColorBrush(Color.FromRgb(240, 253, 244))
                    : new SolidColorBrush(Color.FromRgb(255, 251, 235)),
                BorderBrush = isPraise
                    ? new SolidColorBrush(Color.FromRgb(134, 239, 172))
                    : new SolidColorBrush(Color.FromRgb(253, 230, 138)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var sp = new StackPanel();

            var headerRow = new DockPanel();

            if (!string.IsNullOrEmpty(date))
            {
                var dateBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(70, 90, 130)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10, 3, 10, 3),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = date, Foreground = Brushes.White, FontSize = 12 }
                };
                DockPanel.SetDock(dateBadge, Dock.Right);
                headerRow.Children.Add(dateBadge);
            }

            var subjectPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            subjectPanel.Children.Add(new TextBlock
            {
                Text = isPraise ? "⭐ " : "💡 ",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });
            if (!string.IsNullOrEmpty(course))
            {
                subjectPanel.Children.Add(new Border
                {
                    Background = accentBrush,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 6, 0),
                    Child = new TextBlock { Text = course, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold }
                });
            }
            if (!string.IsNullOrEmpty(category))
            {
                subjectPanel.Children.Add(new TextBlock
                {
                    Text = category,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51)),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            headerRow.Children.Add(subjectPanel);
            sp.Children.Add(headerRow);

            if (!string.IsNullOrEmpty(text))
                sp.Children.Add(new TextBlock { Text = text, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(50, 60, 80)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });

            if (!string.IsNullOrEmpty(author))
                sp.Children.Add(new TextBlock { Text = $"👤 {author}", FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 0) });

            card.Child = sp;
            return card;
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
