using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EAClient.Services;

namespace EAClient.Pages
{
    public partial class OverviewPage : Page
    {
        public OverviewPage()
        {
            InitializeComponent();
            Loaded += OverviewPage_Loaded;
        }

        private async void OverviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            ShowLoading();
            try
            {
                var today = DateTime.Today;

                // Load all sections, failing gracefully per section
                var todayEvents = await TryGetTimetable(today);
                var hwJson = await TryGetJson(() => EAsistentService.GetHomeworkAsync(AuthState.AccessToken, today, today.AddDays(7)));
                var evJson = await TryGetJson(() => EAsistentService.GetEvaluationsAsync(AuthState.AccessToken));
                var foodJson = await TryGetJson(() => EAsistentService.GetSchoolCateringAsync(AuthState.AccessToken, today, today));

                // Today timetable
                TodayTimetablePanel.Children.Clear();
                if (todayEvents == null || !todayEvents.Any())
                {
                    AddGrayText(TodayTimetablePanel, "Danes ni pouka.");
                }
                else
                {
                    foreach (var ev in todayEvents.OrderBy(x => x.From))
                    {
                        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                        row.Children.Add(new TextBlock { Text = $"{ev.From}–{ev.To}", Width = 105, Foreground = Brushes.Gray, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                        row.Children.Add(new TextBlock { Text = ev.Title, FontWeight = FontWeights.Bold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
                        if (!string.IsNullOrEmpty(ev.Classroom))
                            row.Children.Add(new TextBlock { Text = $"  ({ev.Classroom})", Foreground = Brushes.Gray, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                        TodayTimetablePanel.Children.Add(row);
                    }
                }

                // Homework
                HomeworkPanel.Children.Clear();
                PopulateHomework(hwJson);

                // Evaluations
                EvaluationsPanel.Children.Clear();
                PopulateEvaluations(evJson);

                // Food
                FoodPanel.Children.Clear();
                PopulateFood(foodJson);

                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju podatkov. Poskusite znova.\n{ex.Message}");
            }
        }

        private static async Task<System.Collections.Generic.List<EAClient.Models.TimetableEvent>?> TryGetTimetable(DateTime date)
        {
            try { return await EAsistentService.GetTimetableAsync(AuthState.AccessToken, date, date); }
            catch { return null; }
        }

        private static async Task<JsonElement?> TryGetJson(Func<Task<JsonElement>> action)
        {
            try { return await action(); }
            catch { return null; }
        }

        private void PopulateHomework(JsonElement? json)
        {
            if (json == null) { AddGrayText(HomeworkPanel, "Ni domačih nalog."); return; }
            try
            {
                var arr = FindArray(json.Value, "homework", "homeworks");
                if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
                { AddGrayText(HomeworkPanel, "Ni domačih nalog."); return; }

                foreach (var item in arr.EnumerateArray().Take(6))
                {
                    var subject = GetStr(item, "subject", "Predmet");
                    var desc = GetStr(item, "description", GetStr(item, "name", ""));
                    var date = GetStr(item, "date", "");
                    var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 6) };
                    sp.Children.Add(new TextBlock { Text = $"{subject}  –  {date}", FontWeight = FontWeights.Bold, FontSize = 13 });
                    if (!string.IsNullOrEmpty(desc))
                        sp.Children.Add(new TextBlock { Text = desc, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12 });
                    HomeworkPanel.Children.Add(sp);
                }
            }
            catch { AddGrayText(HomeworkPanel, "Ni domačih nalog."); }
        }

        private void PopulateEvaluations(JsonElement? json)
        {
            if (json == null) { AddGrayText(EvaluationsPanel, "Ni prihajajoče ocenjevanj."); return; }
            try
            {
                var arr = FindArray(json.Value, "evaluations");
                if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
                { AddGrayText(EvaluationsPanel, "Ni prihajajoče ocenjevanj."); return; }

                var today = DateTime.Today;
                foreach (var item in arr.EnumerateArray().Take(6))
                {
                    var subject = GetStr(item, "subject", "Predmet");
                    var name = GetStr(item, "name", GetStr(item, "description", ""));
                    var date = GetStr(item, "date", "");
                    var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 6) };
                    var isUrgent = DateTime.TryParse(date, out var dt) && (dt - today).TotalDays <= 7;
                    sp.Children.Add(new TextBlock
                    {
                        Text = $"{date}  –  {subject}",
                        FontWeight = FontWeights.Bold,
                        FontSize = 13,
                        Foreground = isUrgent ? Brushes.OrangeRed : Brushes.Black
                    });
                    if (!string.IsNullOrEmpty(name))
                        sp.Children.Add(new TextBlock { Text = name, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12 });
                    EvaluationsPanel.Children.Add(sp);
                }
            }
            catch { AddGrayText(EvaluationsPanel, "Ni prihajajoče ocenjevanj."); }
        }

        private void PopulateFood(JsonElement? json)
        {
            if (json == null) { AddGrayText(FoodPanel, "Ni podatkov o prehrani."); return; }
            try
            {
                var days = FindArray(json.Value, "days", "menus");
                if (days.ValueKind != JsonValueKind.Array || days.GetArrayLength() == 0)
                { AddGrayText(FoodPanel, "Ni podatkov o prehrani."); return; }

                foreach (var day in days.EnumerateArray().Take(1))
                {
                    FoodPanel.Children.Add(new TextBlock { Text = GetStr(day, "date", ""), FontWeight = FontWeights.Bold, FontSize = 13 });
                    var menus = FindArray(day, "menus", "items");
                    if (menus.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var m in menus.EnumerateArray())
                        {
                            var mname = GetStr(m, "name", "");
                            var price = GetStr(m, "price", "");
                            var line = string.IsNullOrEmpty(price) ? mname : $"{mname}  –  {price} €";
                            FoodPanel.Children.Add(new TextBlock { Text = $"• {line}", Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(6, 1, 0, 1) });
                        }
                    }
                }
            }
            catch { AddGrayText(FoodPanel, "Ni podatkov o prehrani."); }
        }

        private static JsonElement FindArray(JsonElement el, params string[] keys)
        {
            foreach (var k in keys)
                if (el.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array)
                    return v;
            if (el.ValueKind == JsonValueKind.Array) return el;
            return default;
        }

        private static string GetStr(JsonElement el, string prop, string fallback)
        {
            if (el.TryGetProperty(prop, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString() ?? fallback;
                if (v.ValueKind == JsonValueKind.Number) return v.GetRawText();
            }
            return fallback;
        }

        private static void AddGrayText(StackPanel panel, string text)
            => panel.Children.Add(new TextBlock { Text = text, Foreground = Brushes.Gray, FontSize = 13 });

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
