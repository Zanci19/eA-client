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
        private readonly List<(string Id, string Title, string Subtitle, int UnreadCount, JsonElement Raw)> _channels = new();
        private readonly List<(string Id, string Name, string Subtitle, JsonElement Raw)> _contactResults = new();
        private string _selectedChannelId = string.Empty;
        private int _activeInstitutionId;
        private string _currentChannelType = "message";

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

                var channelsJson = await EAsistentService.GetChannelsAsync(AuthState.AccessToken, _currentChannelType, 25);
                foreach (var item in ExtractDataItems(channelsJson))
                {
                    var id = GetStr(item, "id", string.Empty);
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var title = GetStr(item, "title", GetUserName(GetNested(item, "user"), "Pogovor"));
                    var subtitle = GetStr(item, "updated_at", string.Empty);
                    var unread = GetInt(item, "unread_count", 0);
                    _channels.Add((id, title, subtitle, unread, item));
                }

                foreach (var channel in _channels)
                {
                    ChannelsList.Items.Add(BuildChannelItem(channel.Title, channel.Subtitle, channel.UnreadCount));
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

                _ = LoadUnseenCountAsync();
            }
            catch (Exception ex)
            {
                ConversationTitle.Text = "Komunikacija ni na voljo";
                ConversationMeta.Text = ex.Message;
                MessagesPanel.Children.Clear();
                MessagesPanel.Children.Add(BuildHint("Branje komunikacije ni uspelo. Preveri prijavo ali veljavnost seje."));
            }
        }

        private async Task LoadUnseenCountAsync()
        {
            try
            {
                var countJson = await EAsistentService.GetCommunicationChannelCountAsync(AuthState.AccessToken);
                var msgCount = GetInt(countJson, "message", 0);
                var boardCount = GetInt(countJson, "board", 0);
                var smsCount = GetInt(countJson, "sms", 0);
                var total = msgCount + boardCount + smsCount;
                UnseenBadge.Text = total > 0 ? $"● {total} neprebranih" : string.Empty;
                UnseenBadge.Foreground = total > 0 ? AppTheme.AccentBrush : AppTheme.SubTextBrush;
            }
            catch
            {
                UnseenBadge.Text = string.Empty;
            }
        }

        private async Task LoadInstitutionAsync()
        {
            if (_activeInstitutionId > 0 && AuthState.CommunicationUserId > 0)
            {
                return;
            }

            var me = await EAsistentService.GetCommunicationMeAsync(AuthState.AccessToken);
            foreach (var user in ExtractDataItems(me))
            {
                if (AuthState.CommunicationUserId == 0)
                {
                    AuthState.CommunicationUserId = GetInt(user, "id", 0);
                }

                if (_activeInstitutionId > 0)
                {
                    break;
                }

                var nestedInstitutions = GetNested(user, "institutions");
                if (nestedInstitutions.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var institution in nestedInstitutions.EnumerateArray())
                {
                    if (institution.TryGetProperty("external_id", out var extId) && extId.TryGetInt32(out var institutionId))
                    {
                        _activeInstitutionId = institutionId;
                        break;
                    }
                    if (institution.TryGetProperty("id", out var id) && id.TryGetInt32(out institutionId))
                    {
                        _activeInstitutionId = institutionId;
                        break;
                    }
                }

                if (_activeInstitutionId > 0)
                {
                    break;
                }
            }
        }

        private Border BuildChannelItem(string title, string subtitle, int unreadCount = 0)
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
            var headerRow = new DockPanel();
            if (unreadCount > 0)
            {
                var badge = new Border
                {
                    Background = AppTheme.AccentBrush,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(6, 0, 0, 0),
                    Child = new TextBlock { Text = unreadCount.ToString(), Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.Bold }
                };
                DockPanel.SetDock(badge, Dock.Right);
                headerRow.Children.Add(badge);
            }
            headerRow.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = AppTheme.TextBrush, TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(headerRow);
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
            var isSystem = GetStr(message, "type", string.Empty).Equals("system", StringComparison.OrdinalIgnoreCase)
                           || GetBool(message, "is_system")
                           || (GetInt(message, "user_id", -1) == 0 && !GetBool(message, "mine"));
            var mine = !isSystem && (GetInt(message, "user_id", -1) == AuthState.CommunicationUserId || GetBool(message, "mine"));
            var files = GetNested(message, "files");

            SolidColorBrush bgBrush;
            SolidColorBrush borderBrush;
            HorizontalAlignment alignment;
            if (isSystem)
            {
                bgBrush = AppTheme.IsDark
                    ? new SolidColorBrush(Color.FromArgb(60, 140, 155, 180))
                    : new SolidColorBrush(Color.FromRgb(240, 240, 245));
                borderBrush = AppTheme.BorderBrush;
                alignment = HorizontalAlignment.Center;
            }
            else if (mine)
            {
                bgBrush = AppTheme.AccentBrush;
                borderBrush = AppTheme.AccentBrush;
                alignment = HorizontalAlignment.Right;
            }
            else
            {
                bgBrush = AppTheme.IsSleek ? new SolidColorBrush(Color.FromArgb(25, 0, 102, 204)) : AppTheme.CardBrush;
                borderBrush = AppTheme.BorderBrush;
                alignment = HorizontalAlignment.Left;
            }

            var border = new Border
            {
                HorizontalAlignment = alignment,
                Background = bgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(AppTheme.CardCornerRadius),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10),
                MaxWidth = isSystem ? 480 : 540
            };

            var stack = new StackPanel();
            if (!isSystem)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = GetUserName(GetNested(message, "user"), mine ? "Jaz" : "Pošiljatelj"),
                    FontWeight = FontWeights.Bold,
                    Foreground = mine ? Brushes.White : AppTheme.TextBrush
                });
            }
            var bodyText = StripHtml(GetStr(message, "body", string.Empty));
            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = bodyText,
                    Margin = new Thickness(0, isSystem ? 0 : 6, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = isSystem ? TextAlignment.Center : TextAlignment.Left,
                    FontStyle = isSystem ? FontStyles.Italic : FontStyles.Normal,
                    Foreground = mine ? Brushes.White : AppTheme.TextBrush
                });
            }
            if (files.ValueKind == JsonValueKind.Array && files.GetArrayLength() > 0)
            {
                var commToken = EAsistentService.GetCommunicationToken();
                foreach (var file in files.EnumerateArray())
                {
                    var mimeType = GetStr(file, "mime_type", string.Empty);
                    var fileUrl = GetStr(file, "url", string.Empty);
                    var fileName = GetStr(file, "filename", GetStr(file, "id", "datoteka"));
                    if (mimeType.StartsWith("image/") && !string.IsNullOrWhiteSpace(fileUrl) && !string.IsNullOrWhiteSpace(commToken))
                    {
                        try
                        {
                            // The komunikacija API requires the auth token as a query parameter for file access;
                            // WPF BitmapImage does not support custom request headers.
                            var imgUri = new Uri($"{fileUrl}?token={Uri.EscapeDataString(commToken)}");
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = imgUri;
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            var img = new System.Windows.Controls.Image
                            {
                                Source = bitmap,
                                MaxWidth = 320,
                                MaxHeight = 320,
                                Margin = new Thickness(0, 8, 0, 0),
                                Stretch = System.Windows.Media.Stretch.Uniform,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Cursor = System.Windows.Input.Cursors.Hand
                            };
                            img.MouseDown += (_, _) => OpenImageViewer(bitmap);
                            stack.Children.Add(img);
                        }
                        catch (Exception)
                        {
                            stack.Children.Add(new TextBlock
                            {
                                Text = $"🖼 {fileName}",
                                Margin = new Thickness(0, 4, 0, 0),
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = mine ? Brushes.White : AppTheme.SubTextBrush,
                                FontSize = 12
                            });
                        }
                    }
                    else
                    {
                        stack.Children.Add(new TextBlock
                        {
                            Text = $"📎 {fileName}",
                            Margin = new Thickness(0, 4, 0, 0),
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = mine ? Brushes.White : AppTheme.SubTextBrush,
                            FontSize = 12
                        });
                    }
                }
            }
            stack.Children.Add(new TextBlock
            {
                Text = GetStr(message, "created_at", string.Empty),
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 11,
                TextAlignment = isSystem ? TextAlignment.Center : TextAlignment.Left,
                Foreground = mine ? new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)) : AppTheme.SubTextBrush
            });
            border.Child = stack;
            return border;
        }

        private void OpenImageViewer(System.Windows.Media.Imaging.BitmapImage bitmap)
        {
            ImageViewerImage.Source = bitmap;
            ImageViewerOverlay.Visibility = Visibility.Visible;
        }

        private void ImageViewerOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ImageViewerOverlay.Visibility = Visibility.Collapsed;
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

        private async void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedChannelId))
                return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Izberi datoteko za pošiljanje",
                Filter = "Vse datoteke (*.*)|*.*|Slike (*.jpg;*.jpeg;*.png;*.gif;*.webp)|*.jpg;*.jpeg;*.png;*.gif;*.webp|Dokumenti (*.pdf;*.doc;*.docx)|*.pdf;*.doc;*.docx"
            };

            if (dialog.ShowDialog() != true) return;

            AttachButton.IsEnabled = false;
            SendButton.IsEnabled = false;
            try
            {
                var text = MessageInput.Text.Trim();
                await EAsistentService.SendChannelMessageWithFileAsync(AuthState.AccessToken, _selectedChannelId, text, dialog.FileName);
                MessageInput.Text = string.Empty;
                await LoadMessagesAsync(_selectedChannelId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pošiljanje datoteke ni uspelo.\n{ex.Message}", "Komunikacija", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                AttachButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedChannelId);
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
                var response = await EAsistentService.GetCommunicationContactsForInstitutionAsync(AuthState.AccessToken, _activeInstitutionId);
                var queryLower = query.ToLowerInvariant();

                foreach (var item in ExtractDataItems(response))
                {
                    // The contacts response has a "groups" array; each group has a "groups" object of subgroups with "members"
                    var groups = GetNested(item, "groups");
                    if (groups.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var group in groups.EnumerateArray())
                        {
                            var subgroups = GetNested(group, "groups");
                            if (subgroups.ValueKind != JsonValueKind.Object)
                                continue;

                            foreach (var subgroupProp in subgroups.EnumerateObject())
                            {
                                var members = GetNested(subgroupProp.Value, "members");
                                if (members.ValueKind != JsonValueKind.Array)
                                    continue;

                                foreach (var member in members.EnumerateArray())
                                {
                                    var id = GetStr(member, "id", string.Empty);
                                    if (string.IsNullOrWhiteSpace(id))
                                        continue;
                                    var fullName = GetUserName(member, GetStr(member, "name", "Prejemnik"));
                                    if (!fullName.ToLowerInvariant().Contains(queryLower))
                                        continue;
                                    var subtitle = GetStr(subgroupProp.Value, "title", string.Empty);
                                    _contactResults.Add((id, fullName, subtitle, member));
                                    ContactResultsList.Items.Add(BuildChannelItem(fullName, string.IsNullOrWhiteSpace(subtitle) ? $"ID: {id}" : subtitle));
                                }
                            }
                        }
                    }
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

        private async void TabMessages_Click(object sender, RoutedEventArgs e) => await SwitchChannelTypeAsync("message");
        private async void TabBoard_Click(object sender, RoutedEventArgs e) => await SwitchChannelTypeAsync("board");
        private async void TabSms_Click(object sender, RoutedEventArgs e) => await SwitchChannelTypeAsync("sms");

        private async Task SwitchChannelTypeAsync(string channelType)
        {
            if (_currentChannelType == channelType) return;
            _currentChannelType = channelType;
            UpdateTabStyles();
            await LoadChannelsAsync();
        }

        private void UpdateTabStyles()
        {
            foreach (var (btn, type) in new[] { (TabMessages, "message"), (TabBoard, "board"), (TabSms, "sms") })
            {
                btn.Background = _currentChannelType == type ? AppTheme.AccentBrush : AppTheme.CardBrush;
                btn.Foreground = _currentChannelType == type ? Brushes.White : AppTheme.TextBrush;
                btn.BorderThickness = new Thickness(0);
            }
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
            AttachButton.IsEnabled = enabled;
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
            var overlayBg = AppTheme.IsDark
                ? new SolidColorBrush(Color.FromArgb(220, 12, 16, 26))
                : new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
            ComposeOverlay.Background = overlayBg;
            ConversationTitle.Foreground = AppTheme.TextBrush;
            ConversationMeta.Foreground = AppTheme.SubTextBrush;
            ChannelsTitleBlock.Foreground = AppTheme.TextBrush;
            MessageInput.Background = AppTheme.BgBrush;
            MessageInput.Foreground = AppTheme.TextBrush;
            MessageInput.BorderBrush = AppTheme.BorderBrush;
            foreach (var textBox in new[] { NewMessageRecipientInput, NewMessageTitleInput, NewMessageBodyInput })
            {
                textBox.Background = AppTheme.BgBrush;
                textBox.Foreground = AppTheme.TextBrush;
                textBox.BorderBrush = AppTheme.BorderBrush;
            }
            foreach (var button in new[] { RefreshButton, SendButton, AttachButton, SearchRecipientsButton, SendNewMessageButton, ComposeFloatingButton, CloseComposeButton })
            {
                button.Background = AppTheme.AccentBrush;
                button.Foreground = Brushes.White;
                button.BorderThickness = new Thickness(0);
            }
            ComposeFloatingButton.Width = double.NaN;
            ComposeFloatingButton.Height = double.NaN;
            NewMessageStatus.Foreground = AppTheme.SubTextBrush;
            UpdateTabStyles();
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
