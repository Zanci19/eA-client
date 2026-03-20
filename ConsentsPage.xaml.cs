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
    public partial class ConsentsPage : Page
    {
        public ConsentsPage()
        {
            InitializeComponent();
            Loaded += ConsentsPage_Loaded;
        }

        private async void ConsentsPage_Loaded(object sender, RoutedEventArgs e)
            => await LoadDataAsync();

        private async Task LoadDataAsync()
        {
            ShowLoading();
            try
            {
                var json = await EAsistentService.GetConsentsAsync(AuthState.AccessToken);
                PopulateItems(json);
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju soglasij. Poskusite znova.\n{ex.Message}");
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
                    Text = "Ni soglasij za prikaz.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(4, 4, 4, 4)
                });
                return;
            }

            var sorted = items.OrderByDescending(x =>
            {
                DateTime.TryParse(GetStr(x, "deadline", string.Empty), out var d);
                return d;
            }).ToList();

            foreach (var item in sorted)
                ItemsContainer.Children.Add(BuildCard(item));
        }

        private static Border BuildCard(JsonElement item)
        {
            var uuid = GetStr(item, "uuid", string.Empty);
            var type = GetStr(item, "type", string.Empty);
            var state = GetStr(item, "state", string.Empty);
            var deadline = GetStr(item, "deadline", string.Empty);
            var title = GetStr(item, "title", "Soglasje");

            var isSigned = state.Equals("soglasam", StringComparison.OrdinalIgnoreCase);
            var deadlineDate = DateTime.TryParse(deadline, out var dd) ? dd : (DateTime?)null;
            var isExpired = deadlineDate.HasValue && deadlineDate.Value.Date < DateTime.Today;

            var card = new Border
            {
                Background = isSigned
                    ? new SolidColorBrush(Color.FromRgb(240, 253, 244))
                    : isExpired
                        ? new SolidColorBrush(Color.FromRgb(252, 245, 245))
                        : Brushes.White,
                BorderBrush = isSigned
                    ? new SolidColorBrush(Color.FromRgb(134, 239, 172))
                    : isExpired
                        ? new SolidColorBrush(Color.FromRgb(252, 165, 165))
                        : new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var sp = new StackPanel();

            var headerRow = new DockPanel();

            var statusBadge = new Border
            {
                Background = isSigned
                    ? new SolidColorBrush(Color.FromRgb(0, 153, 76))
                    : isExpired
                        ? new SolidColorBrush(Color.FromRgb(180, 40, 40))
                        : new SolidColorBrush(Color.FromRgb(200, 130, 0)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = isSigned ? "✓ Podpisano" : isExpired ? "Poteklo" : "Čaka",
                    Foreground = Brushes.White,
                    FontSize = 12
                }
            };
            DockPanel.SetDock(statusBadge, Dock.Right);
            headerRow.Children.Add(statusBadge);

            headerRow.Children.Add(new TextBlock
            {
                Text = CapitalizeType(type),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51)),
                VerticalAlignment = VerticalAlignment.Center
            });

            sp.Children.Add(headerRow);
            sp.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(50, 60, 80)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });

            if (!string.IsNullOrEmpty(deadline))
                sp.Children.Add(new TextBlock
                {
                    Text = $"📅 Rok: {deadline}",
                    FontSize = 12,
                    Foreground = isExpired ? new SolidColorBrush(Color.FromRgb(180, 40, 40)) : Brushes.Gray,
                    Margin = new Thickness(0, 4, 0, 0)
                });

            card.Child = sp;
            return card;
        }

        private static string CapitalizeType(string type)
            => string.IsNullOrEmpty(type) ? "Soglasje" : char.ToUpperInvariant(type[0]) + (type.Length > 1 ? type[1..] : string.Empty);

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
