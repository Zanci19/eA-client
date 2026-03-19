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
        private readonly List<(string Id, string Name, string Subtitle, JsonElement Raw)> _contactResults = new();
        private string _selectedChannelId = string.Empty;
        private int _activeInstitutionId;

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
                _selectedChannelId = string.Empty;
                UpdateComposerState();
                await LoadInstitutionAsync();

                var channelsJson = await EAsistentService.GetMessageChannelsAsync(AuthState.AccessToken, 25);
                foreach (var item in ExtractDataItems(channelsJson))
                {
                    var id = GetStr(item, "id", string.Empty);
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var title = GetStr(item, "title", GetUserName(GetNested(item, "user"), "Pogovor"));
                    var subtitle = GetStr(item, "updated_at", string.Empty);
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
                MessagesPanel.Children.Add(BuildHint("Branje komunikacije ni uspelo. Preveri prijavo ali veljavnost seje."));
            }
        }

        private async Task LoadInstitutionAsync()
        {
            if (_activeInstitutionId > 0)
            {
                return;
            }

            var institutions = await EAsistentService.GetCommunicationInstitutionsAsync(AuthState.AccessToken);
            var first = ExtractDataItems(institutions).FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("id", out var idValue) && idValue.TryGetInt32(out var institutionId))
            {
                _activeInstitutionId = institutionId;
                return;
            }

            var me = await EAsistentService.GetCommunicationMeAsync(AuthState.AccessToken);
            foreach (var user in ExtractDataItems(me))
            {
                var nestedInstitutions = GetNested(user, "institutions");
                if (nestedInstitutions.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var institution in nestedInstitutions.EnumerateArray())
                {
                    if (institution.TryGetProperty("external_id", out var extId) && extId.TryGetInt32(out institutionId))
                    {
                        _activeInstitutionId = institutionId;
                        return;
                    }
                    if (institution.TryGetProperty("id", out var id) && id.TryGetInt32(out institutionId))
                    {
                        _activeInstitutionId = institutionId;
                        return;
                    }
                }
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
            UpdateComposerState();
            await LoadMessagesAsync(_selectedChannelId);
        }

        private async Task LoadMessagesAsync(string channelId)
        {
            try
            {
                MessagesPanel.Children.Clear();
                var detailsTask = EAsistentService.GetChannelDetailsAsync(AuthState.AccessToken, channelId);
                var messagesTask = EAsistentService.GetChannelMessagesAsync(AuthState.AccessToken, channelId, 25);
                await Task.WhenAll(detailsTask, messagesTask);

                var details = ExtractDataItems(detailsTask.Result).FirstOrDefault();
                var messages = ExtractDataItems(messagesTask.Result).Reverse().ToList();
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
            var mine = GetInt(message, "user_id", -1) == AuthState.UserId || GetBool(message, "mine");
            var files = GetNested(message, "files");
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
                Text = GetUserName(GetNested(message, "user"), mine ? "Jaz" : "Pošiljatelj"),
                FontWeight = FontWeights.Bold,
                Foreground = mine ? Brushes.White : AppTheme.TextBrush
            });
            stack.Children.Add(new TextBlock
            {
                Text = StripHtml(GetStr(message, "body", string.Empty)),
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
                SendButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedChannelId);
            }
        }

        private async void SearchRecipientsButton_Click(object sender, RoutedEventArgs e)
        {
            await SearchRecipientsAsync();
        }

        private async Task SearchRecipientsAsync()
        {
            var query = NewMessageRecipientInput.Text.Trim();
            ContactResultsList.Items.Clear();
            _contactResults.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                NewMessageStatus.Text = "Vnesi ime prejemnika za iskanje.";
                return;
            }

            try
            {
                await LoadInstitutionAsync();
                var response = await EAsistentService.SearchCommunicationContactsAsync(AuthState.AccessToken, query, _activeInstitutionId);
                foreach (var user in ExtractDataItems(response))
                {
                    var id = GetStr(user, "id", string.Empty);
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var fullName = GetUserName(user, GetStr(user, "name", "Prejemnik"));
                    var subtitle = GetStr(user, "title", GetStr(user, "email", string.Empty));
                    _contactResults.Add((id, fullName, subtitle, user));
                    ContactResultsList.Items.Add(BuildChannelItem(fullName, string.IsNullOrWhiteSpace(subtitle) ? $"ID: {id}" : subtitle));
                }

                NewMessageStatus.Text = _contactResults.Count == 0
                    ? "Ni zadetkov za izbran pojem."
                    : "Izberi prejemnika s seznama spodaj.";
            }
            catch (Exception ex)
            {
                NewMessageStatus.Text = ex.Message;
            }
        }

        private void ContactResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContactResultsList.SelectedIndex < 0 || ContactResultsList.SelectedIndex >= _contactResults.Count)
            {
                return;
            }

            var selected = _contactResults[ContactResultsList.SelectedIndex];
            NewMessageRecipientInput.Text = selected.Name;
            NewMessageStatus.Text = $"Izbran prejemnik: {selected.Name}";
        }

        private async void SendNewMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContactResultsList.SelectedIndex < 0 || ContactResultsList.SelectedIndex >= _contactResults.Count)
            {
                MessageBox.Show("Najprej izberi prejemnika iz rezultatov iskanja.", "Nova komunikacija", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var message = NewMessageBodyInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Vnesi vsebino novega sporočila.", "Nova komunikacija", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SendNewMessageButton.IsEnabled = false;
            try
            {
                await LoadInstitutionAsync();
                var selected = _contactResults[ContactResultsList.SelectedIndex];
                var title = string.IsNullOrWhiteSpace(NewMessageTitleInput.Text) ? selected.Name : NewMessageTitleInput.Text.Trim();
                await EAsistentService.CreateNewMessageChannelAsync(
                    AuthState.AccessToken,
                    title,
                    message,
                    _activeInstitutionId,
                    new[] { selected.Raw });

                NewMessageBodyInput.Text = string.Empty;
                NewMessageTitleInput.Text = string.Empty;
                NewMessageRecipientInput.Text = string.Empty;
                NewMessageStatus.Text = "Novo sporočilo je bilo ustvarjeno.";
                ContactResultsList.Items.Clear();
                _contactResults.Clear();
                ComposeOverlay.Visibility = Visibility.Collapsed;
                await LoadChannelsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ustvarjanje novega sporočila ni uspelo.\n{ex.Message}", "Nova komunikacija", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                SendNewMessageButton.IsEnabled = true;
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

        private void ComposeFloatingButton_Click(object sender, RoutedEventArgs e)
            => ComposeOverlay.Visibility = Visibility.Visible;

        private void CloseComposeButton_Click(object sender, RoutedEventArgs e)
            => ComposeOverlay.Visibility = Visibility.Collapsed;

        private void UpdateComposerState()
        {
            var enabled = !string.IsNullOrWhiteSpace(_selectedChannelId);
            MessageInput.IsEnabled = enabled;
            SendButton.IsEnabled = enabled;
            if (!enabled)
            {
                MessageInput.Text = string.Empty;
            }
        }

        private void ApplyTheme()
        {
            RootGrid.Background = AppTheme.BgBrush;
            TitleBar.Background = AppTheme.TitleBarBrush;
            FontFamily = new FontFamily(AppTheme.FontFamily);
            foreach (var card in new[] { ChannelsCard, MessagesCard, ComposerCard, ComposeOverlayCard })
            {
                card.Background = AppTheme.CardBrush;
                card.BorderBrush = AppTheme.BorderBrush;
                card.BorderThickness = new Thickness(1);
                card.CornerRadius = new CornerRadius(AppTheme.CardCornerRadius);
            }
            ComposeOverlay.Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
            ConversationMeta.Foreground = AppTheme.SubTextBrush;
            MessageInput.Background = AppTheme.BgBrush;
            MessageInput.Foreground = AppTheme.TextBrush;
            MessageInput.BorderBrush = AppTheme.BorderBrush;
            foreach (var textBox in new[] { NewMessageRecipientInput, NewMessageTitleInput, NewMessageBodyInput })
            {
                textBox.Background = AppTheme.BgBrush;
                textBox.Foreground = AppTheme.TextBrush;
                textBox.BorderBrush = AppTheme.BorderBrush;
            }
            foreach (var button in new[] { RefreshButton, SendButton, SearchRecipientsButton, SendNewMessageButton, ComposeFloatingButton, CloseComposeButton })
            {
                button.Background = AppTheme.AccentBrush;
                button.Foreground = Brushes.White;
                button.BorderThickness = new Thickness(0);
            }
            ComposeFloatingButton.Width = 64;
            ComposeFloatingButton.Height = 64;
            NewMessageStatus.Foreground = AppTheme.SubTextBrush;
        }

        private static IEnumerable<JsonElement> ExtractDataItems(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("data", out var data))
                    {
                        if (data.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var nested in data.EnumerateArray())
                                yield return nested;
                        }
                        else
                        {
                            yield return data;
                        }
                    }
                    else
                    {
                        yield return item;
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                yield return root;
            }
        }

        private static JsonElement GetNested(JsonElement element, string property)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) ? value : default;

        private static string GetStr(JsonElement element, string property, string fallback)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value))
            {
                return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : value.ToString();
            }
            return fallback;
        }

        private static int GetInt(JsonElement element, string property, int fallback)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
                ? parsed
                : fallback;

        private static bool GetBool(JsonElement element, string property)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
                return false;

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => value.TryGetInt32(out var number) && number == 1,
                JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
                _ => false
            };
        }

        private static string GetUserName(JsonElement user, string fallback)
        {
            var first = GetStr(user, "firstname", string.Empty);
            var last = GetStr(user, "lastname", string.Empty);
            var combined = string.Join(" ", new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            return string.IsNullOrWhiteSpace(combined) ? fallback : combined;
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty)
                .Replace("&nbsp;", " ")
                .Trim();
        }
    }
}
