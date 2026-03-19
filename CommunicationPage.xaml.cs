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
    public partial class CommunicationPage : Page
    {
        private readonly List<(string Id, string Title, string Subtitle, JsonElement Raw)> _channels = new();
        private string _selectedChannelId = string.Empty;

        public CommunicationPage()
        {
            InitializeComponent();
            Loaded += CommunicationPage_Loaded;
        }

        private async void CommunicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTheme();
            await LoadChannelsAsync();
        }

        private async Task LoadChannelsAsync()
        {
            try
            {
                ConversationTitle.Text = "Nalaganje pogovorov...";
                ChannelsList.Items.Clear();
                _channels.Clear();
                var channelsJson = await EAsistentService.GetMessageChannelsAsync(AuthState.AccessToken, 25);
                var items = ExtractArray(channelsJson, "data", "channels");
                foreach (var item in items.EnumerateArray())
                {
                    var id = GetStr(item, "id", string.Empty);
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var title = GetStr(item, "title", GetNestedStr(item, new[] { "to", "name" }, GetNestedStr(item, new[] { "user", "name" }, "Pogovor")));
                    var subtitle = GetStr(item, "updated_at", GetNestedStr(item, new[] { "last_message", "message" }, string.Empty));
                    _channels.Add((id, title, subtitle, item));
                }

                foreach (var channel in _channels)
                {
                    ChannelsList.Items.Add(BuildChannelItem(channel.Title, channel.Subtitle));
                }

                if (_channels.Count == 0)
                {
                    ConversationTitle.Text = "Ni sporočil";
                    ConversationMeta.Text = "Komunikacijski API ni vrnil nobenega kanala.";
                    MessagesPanel.Children.Clear();
                    MessagesPanel.Children.Add(BuildHint("Ni pogovorov za prikaz."));
                    return;
                }

                ChannelsList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ConversationTitle.Text = "Komunikacija ni na voljo";
                ConversationMeta.Text = ex.Message;
                MessagesPanel.Children.Clear();
                MessagesPanel.Children.Add(BuildHint("Branje komunikacije ni uspelo. Preveri prijavo ali veljavnost žetona."));
            }
        }

        private Border BuildChannelItem(string title, string subtitle)
        {
            var border = new Border
            {
                Background = AppTheme.IsSleek ? new SolidColorBrush(Color.FromArgb(26, 0, 102, 204)) : AppTheme.CardBrush,
                BorderBrush = AppTheme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(AppTheme.CardCornerRadius),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = AppTheme.TextBrush, TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Foreground = AppTheme.SubTextBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            border.Child = stack;
            return border;
        }

        private async void ChannelsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChannelsList.SelectedIndex < 0 || ChannelsList.SelectedIndex >= _channels.Count)
                return;

            _selectedChannelId = _channels[ChannelsList.SelectedIndex].Id;
            ConversationTitle.Text = _channels[ChannelsList.SelectedIndex].Title;
            ConversationMeta.Text = "Nalaganje sporočil...";
            await LoadMessagesAsync(_selectedChannelId);
        }

        private async Task LoadMessagesAsync(string channelId)
        {
            try
            {
                MessagesPanel.Children.Clear();
                var detailsTask = EAsistentService.GetChannelDetailsAsync(AuthState.AccessToken, channelId);
                var messagesTask = EAsistentService.GetChannelMessagesAsync(AuthState.AccessToken, channelId, 20);
                await Task.WhenAll(detailsTask, messagesTask);
                var details = detailsTask.Result;
                var messages = ExtractArray(messagesTask.Result, "data", "messages").EnumerateArray().Reverse().ToList();
                ConversationMeta.Text = GetStr(details, "updated_at", "Pripravljen za pošiljanje in branje sporočil.");

                if (messages.Count == 0)
                {
                    MessagesPanel.Children.Add(BuildHint("V tem pogovoru še ni sporočil."));
                    return;
                }

                foreach (var message in messages)
                {
                    var bubble = BuildMessageBubble(message);
                    MessagesPanel.Children.Add(bubble);
                    AnimationHelper.FadeInFromBelow(bubble, 220);
                }
            }
            catch (Exception ex)
            {
                ConversationMeta.Text = ex.Message;
                MessagesPanel.Children.Clear();
                MessagesPanel.Children.Add(BuildHint("Sporočil ni bilo mogoče naložiti."));
            }
        }

        private Border BuildMessageBubble(JsonElement message)
        {
            var mine = GetNestedStr(message, new[] { "author", "id" }, string.Empty) == AuthState.UserId.ToString() || GetBool(message, "mine");
            var files = ExtractArray(message, "files");
            var border = new Border
            {
                HorizontalAlignment = mine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = mine ? AppTheme.AccentBrush : (AppTheme.IsSleek ? new SolidColorBrush(Color.FromArgb(25, 0, 102, 204)) : AppTheme.CardBrush),
                BorderBrush = mine ? AppTheme.AccentBrush : AppTheme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(AppTheme.CardCornerRadius),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10),
                MaxWidth = 540
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = GetNestedStr(message, new[] { "author", "name" }, GetStr(message, "user", mine ? "Jaz" : "Pošiljatelj")),
                FontWeight = FontWeights.Bold,
                Foreground = mine ? Brushes.White : AppTheme.TextBrush
            });
            stack.Children.Add(new TextBlock
            {
                Text = GetStr(message, "message", GetStr(message, "text", string.Empty)),
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = mine ? Brushes.White : AppTheme.TextBrush
            });
            if (files.ValueKind == JsonValueKind.Array && files.GetArrayLength() > 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"Priponke: {string.Join(", ", files.EnumerateArray().Select(file => GetStr(file, "filename", GetStr(file, "id", "datoteka"))))}",
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = mine ? Brushes.White : AppTheme.SubTextBrush,
                    FontSize = 12
                });
            }
            stack.Children.Add(new TextBlock
            {
                Text = GetStr(message, "created_at", string.Empty),
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 11,
                Foreground = mine ? new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)) : AppTheme.SubTextBrush
            });
            border.Child = stack;
            return border;
        }

        private TextBlock BuildHint(string text)
            => new() { Text = text, Foreground = AppTheme.SubTextBrush, FontSize = 13, TextWrapping = TextWrapping.Wrap };

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedChannelId) || string.IsNullOrWhiteSpace(MessageInput.Text))
                return;

            SendButton.IsEnabled = false;
            var text = MessageInput.Text.Trim();
            try
            {
                await EAsistentService.SendChannelMessageAsync(AuthState.AccessToken, _selectedChannelId, text);
                MessageInput.Text = string.Empty;
                await LoadMessagesAsync(_selectedChannelId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pošiljanje ni uspelo.\n{ex.Message}", "Komunikacija", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedChannelId))
            {
                await LoadChannelsAsync();
                return;
            }
            await LoadMessagesAsync(_selectedChannelId);
        }

        private void ApplyTheme()
        {
            RootGrid.Background = AppTheme.BgBrush;
            TitleBar.Background = AppTheme.TitleBarBrush;
            FontFamily = new FontFamily(AppTheme.FontFamily);
            foreach (var card in new[] { ChannelsCard, MessagesCard, ComposerCard })
            {
                card.Background = AppTheme.CardBrush;
                card.BorderBrush = AppTheme.BorderBrush;
                card.BorderThickness = new Thickness(1);
                card.CornerRadius = new CornerRadius(AppTheme.CardCornerRadius);
            }
            ConversationMeta.Foreground = AppTheme.SubTextBrush;
            MessageInput.Background = AppTheme.BgBrush;
            MessageInput.Foreground = AppTheme.TextBrush;
            MessageInput.BorderBrush = AppTheme.BorderBrush;
            foreach (var button in new[] { RefreshButton, SendButton })
            {
                button.Background = AppTheme.AccentBrush;
                button.Foreground = Brushes.White;
                button.BorderThickness = new Thickness(0);
            }
        }

        private static JsonElement ExtractArray(JsonElement root, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (root.TryGetProperty(key, out var value))
                {
                    if (value.ValueKind == JsonValueKind.Array) return value;
                    root = value;
                }
            }
            return root.ValueKind == JsonValueKind.Array ? root : default;
        }

        private static string GetStr(JsonElement element, string property, string fallback)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value))
            {
                if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? fallback;
                if (value.ValueKind == JsonValueKind.Number) return value.GetRawText();
            }
            return fallback;
        }

        private static string GetNestedStr(JsonElement element, IReadOnlyList<string> path, string fallback)
        {
            var current = element;
            foreach (var segment in path)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                    return fallback;
            }
            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString() ?? fallback,
                JsonValueKind.Number => current.GetRawText(),
                _ => fallback
            };
        }

        private static bool GetBool(JsonElement element, string property)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    }
}
