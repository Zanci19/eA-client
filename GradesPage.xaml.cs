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
    public partial class GradesPage : Page
    {
        public GradesPage()
        {
            InitializeComponent();
            Loaded += GradesPage_Loaded;
        }

        private async void GradesPage_Loaded(object sender, RoutedEventArgs e)
            => await LoadDataAsync();

        private async Task LoadDataAsync()
        {
            ShowLoading();
            try
            {
                var year = DateTime.Today.Year;
                var from = new DateTime(year, 1, 1);
                var to = new DateTime(year, 12, 31);
                var json = await EAsistentService.GetGradesAsync(AuthState.AccessToken, from, to);
                PopulateGrades(json);
                ShowContent();
            }
            catch (Exception ex)
            {
                ShowError($"Napaka pri nalaganju ocen. Poskusite znova.\n{ex.Message}");
            }
        }

        private void PopulateGrades(JsonElement json)
        {
            GradesContainer.Children.Clear();

            if (!AuthState.PlusEnabled)
            {
                var notice = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 243, 205)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                    BorderThickness = new Thickness(1,1,1,1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 0, 0, 16)
                };
                notice.Child = new TextBlock
                {
                    Text = "ℹ️  Ocene so na voljo samo za Plus naročnike.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                };
                GradesContainer.Children.Add(notice);
            }

            // Try to find grades array in various response shapes
            var gradesList = new List<(string subject, int grade, string date, string teacher, string note)>();
            try
            {
                // Shape 1: { "grades": [...] } flat list
                if (json.TryGetProperty("grades", out var gradesArr) && gradesArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in gradesArr.EnumerateArray())
                    {
                        var subject = GetStr(g, "subject", GetStr(g, "subject_name", "Predmet"));
                        var gradeVal = 0;
                        if (g.TryGetProperty("grade", out var gv))
                        {
                            if (gv.ValueKind == JsonValueKind.Number) gradeVal = gv.GetInt32();
                            else if (gv.ValueKind == JsonValueKind.String && int.TryParse(gv.GetString(), out var gi)) gradeVal = gi;
                        }
                        var date = GetStr(g, "date_of_entry", GetStr(g, "date", ""));
                        var teacher = GetStr(g, "teacher_name", GetStr(g, "teacher", ""));
                        var note = GetStr(g, "comment", GetStr(g, "note", ""));
                        gradesList.Add((subject, gradeVal, date, teacher, note));
                    }
                }
                // Shape 2: { "subjects": [{ "name":..., "grades":[...] }] }
                else if (json.TryGetProperty("subjects", out var subjects) && subjects.ValueKind == JsonValueKind.Array)
                {
                    foreach (var subj in subjects.EnumerateArray())
                    {
                        var subjName = GetStr(subj, "name", "Predmet");
                        if (subj.TryGetProperty("grades", out var sg) && sg.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var g in sg.EnumerateArray())
                            {
                                var gradeVal = 0;
                                if (g.TryGetProperty("grade", out var gv))
                                {
                                    if (gv.ValueKind == JsonValueKind.Number) gradeVal = gv.GetInt32();
                                    else if (gv.ValueKind == JsonValueKind.String && int.TryParse(gv.GetString(), out var gi)) gradeVal = gi;
                                }
                                gradesList.Add((subjName, gradeVal, GetStr(g, "date", ""), GetStr(g, "teacher", ""), GetStr(g, "note", "")));
                            }
                        }
                    }
                }
            }
            catch { /* fall through to empty state */ }

            if (!gradesList.Any())
            {
                GradesContainer.Children.Add(new TextBlock
                {
                    Text = "Ni ocen za prikaz.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(4,4,4,4)
                });
                return;
            }

            // Group by subject and render
            var bySubject = gradesList.GroupBy(g => g.subject).OrderBy(g => g.Key);
            foreach (var group in bySubject)
            {
                var gradeValues = group.Where(g => g.grade > 0).Select(g => g.grade).ToList();
                var avg = gradeValues.Any() ? gradeValues.Average() : 0;

                var subjectCard = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                    BorderThickness = new Thickness(1,1,1,1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16,16,16,16),
                    Margin = new Thickness(0, 0, 0, 12)
                };

                var sp = new StackPanel();

                // Subject header row
                var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                headerRow.Children.Add(new TextBlock
                {
                    Text = group.Key,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(28, 35, 51)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                DockPanel.SetDock(headerRow.Children[0] as UIElement ?? new UIElement(), Dock.Left);

                if (avg > 0)
                {
                    var avgText = new TextBlock
                    {
                        Text = $"Povprečje: {avg:F2}",
                        FontSize = 13,
                        Foreground = GetAverageColor(avg),
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(avgText, Dock.Right);
                    headerRow.Children.Insert(0, avgText);
                }
                sp.Children.Add(headerRow);

                // Separator
                sp.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                    Margin = new Thickness(0, 0, 0, 10)
                });

                // Grade entries
                foreach (var (_, grade, date, teacher, note) in group)
                {
                    var gradeRow = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };

                    // Grade badge
                    var badge = new Border
                    {
                        Width = 36,
                        Height = 36,
                        CornerRadius = new CornerRadius(18),
                        Background = GetGradeBadgeColor(grade),
                        Margin = new Thickness(0, 0, 12, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    badge.Child = new TextBlock
                    {
                        Text = grade > 0 ? grade.ToString() : "–",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(badge, Dock.Left);
                    gradeRow.Children.Add(badge);

                    var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    if (!string.IsNullOrEmpty(teacher))
                        info.Children.Add(new TextBlock { Text = teacher, FontSize = 12, Foreground = Brushes.DimGray });
                    if (!string.IsNullOrEmpty(note))
                        info.Children.Add(new TextBlock { Text = note, FontSize = 12, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap });
                    gradeRow.Children.Add(info);

                    if (!string.IsNullOrEmpty(date))
                    {
                        var dateTb = new TextBlock
                        {
                            Text = date,
                            FontSize = 11,
                            Foreground = Brushes.Gray,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        DockPanel.SetDock(dateTb, Dock.Right);
                        gradeRow.Children.Insert(0, dateTb);
                    }

                    sp.Children.Add(gradeRow);
                }

                subjectCard.Child = sp;
                GradesContainer.Children.Add(subjectCard);
            }
        }

        private static Brush GetAverageColor(double avg)
        {
            if (avg >= 4.5) return new SolidColorBrush(Color.FromRgb(0, 153, 76));
            if (avg >= 3.5) return new SolidColorBrush(Color.FromRgb(0, 102, 204));
            if (avg >= 2.5) return new SolidColorBrush(Color.FromRgb(255, 153, 0));
            return new SolidColorBrush(Color.FromRgb(204, 0, 0));
        }

        private static Brush GetGradeBadgeColor(int grade)
        {
            return grade switch
            {
                5 => new SolidColorBrush(Color.FromRgb(0, 153, 76)),
                4 => new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                3 => new SolidColorBrush(Color.FromRgb(255, 153, 0)),
                2 => new SolidColorBrush(Color.FromRgb(255, 80, 0)),
                1 => new SolidColorBrush(Color.FromRgb(204, 0, 0)),
                _ => new SolidColorBrush(Color.FromRgb(150, 150, 150))
            };
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
