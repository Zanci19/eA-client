using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EAClient.Models;

namespace EAClient.Services
{
    public static class EAsistentService
    {
        private static readonly CookieContainer _cookies = new();

        private static readonly HttpClient _client = CreateClient("https://www.easistent.com");
        private static readonly HttpClient _communicationClient = CreateClient("https://komunikacija.easistent.com");

        private static string _communicationToken = string.Empty;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private static HttpClient CreateClient(string? baseAddress = null, bool allowAutoRedirect = true)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = allowAutoRedirect,
                UseCookies = true,
                CookieContainer = _cookies,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            if (!string.IsNullOrWhiteSpace(baseAddress))
            {
                client.BaseAddress = new Uri(baseAddress);
            }

            return client;
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? token = null)
        {
            var request = new HttpRequestMessage(method, WithMobileFlag(url));
            ApplyCommonHeaders(request, token);
            request.Headers.TryAddWithoutValidation("app", "new_mobile_app");
            return request;
        }

        private static HttpRequestMessage CreateCommunicationRequest(HttpMethod method, string url, string? token = null)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            request.Headers.TryAddWithoutValidation("x-app-name", "moj_asistent_parent");
            request.Headers.TryAddWithoutValidation("x-client-version", "100000");
            request.Headers.TryAddWithoutValidation("x-client-platform", "ios");
            var bearerToken = string.IsNullOrWhiteSpace(token) ? _communicationToken : token;
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearerToken}");
            }
            return request;
        }

        private static void ApplyCommonHeaders(HttpRequestMessage request, string? token)
        {
            request.Headers.TryAddWithoutValidation("x-app-name", "child");
            request.Headers.TryAddWithoutValidation("x-client-version", "11102");
            request.Headers.TryAddWithoutValidation("x-client-platform", "ios");

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
                if (AuthState.UserId > 0)
                {
                    request.Headers.TryAddWithoutValidation("X-Child-Id", $"child_uuid_{AuthState.UserId}");
                }
            }
        }

        private static string WithMobileFlag(string url)
        {
            if (url.Contains("wl=true", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            return url.Contains('?') ? $"{url}&wl=true" : $"{url}?wl=true";
        }

        public static async Task<LoginResponse> LoginAsync(string username, string password)
        {
            var request = CreateRequest(HttpMethod.Post, "/m/login");
            var body = JsonSerializer.Serialize(new
            {
                username,
                password,
                supported_user_types = new[] { "child", "parent" }
            });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Napaka pri prijavi ({(int)response.StatusCode}): {TryGetErrorMessage(json)}");
            }

            return JsonSerializer.Deserialize<LoginResponse>(json)
                ?? throw new Exception("Napaka pri razčlenjevanju odgovora.");
        }

        public static async Task<string> RefreshTokenAsync(string refreshToken)
        {
            var request = CreateRequest(HttpMethod.Post, "/m/refresh_token");
            var body = JsonSerializer.Serialize(new { refresh_token = refreshToken });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Napaka pri osveževanju žetona: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var at)
                && at.TryGetProperty("token", out var t))
            {
                // Also update the stored refresh token if the server issued a new one
                if (doc.RootElement.TryGetProperty("refresh_token", out var rt))
                {
                    var newRefreshToken = rt.GetString();
                    if (!string.IsNullOrWhiteSpace(newRefreshToken))
                    {
                        AuthState.RefreshToken = newRefreshToken;
                    }
                }
                return t.GetString() ?? string.Empty;
            }

            throw new Exception("Napaka pri razčlenjevanju odgovora za osvežitev žetona.");
        }

        public static string GetCommunicationToken() => _communicationToken;

        public static async Task<List<TimetableEvent>> GetTimetableAsync(string token, DateTime from, DateTime to)
        {
            var url = $"/m/timetable/events?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
            var json = await GetJsonWithRefreshAsync(token, url);
            var result = JsonSerializer.Deserialize<TimetableResponse>(json.GetRawText());
            return result?.Events ?? new List<TimetableEvent>();
        }

        private static async Task<JsonElement> GetJsonWithRefreshAsync(string token, string url)
        {
            var (response, json) = await SendGetAsync(token, url);

            if (response.StatusCode == HttpStatusCode.Unauthorized
                && !string.IsNullOrWhiteSpace(AuthState.RefreshToken))
            {
                var freshAccessToken = await RefreshTokenAsync(AuthState.RefreshToken);
                AuthState.AccessToken = freshAccessToken;
                token = freshAccessToken;
                (response, json) = await SendGetAsync(token, url);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Napaka ({(int)response.StatusCode}): {TryGetErrorMessage(json)}");
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        private static async Task<(HttpResponseMessage response, string json)> SendGetAsync(string token, string url)
        {
            var request = CreateRequest(HttpMethod.Get, url, token);
            var response = await _client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            return (response, json);
        }

        public static Task<JsonElement> GetGradesAsync(string token, DateTime from, DateTime to)
            => GetJsonWithRefreshAsync(token, $"/m/grades?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        public static Task<JsonElement> GetAbsencesAsync(string token, DateTime from, DateTime to)
            => GetJsonWithRefreshAsync(token, $"/m/absences?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        public static Task<JsonElement> GetHomeworkAsync(string token, DateTime from, DateTime to)
            => GetJsonWithRefreshAsync(token, $"/m/homework?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        public static Task<JsonElement> GetEvaluationsAsync(string token)
            => GetJsonWithRefreshAsync(token, "/m/evaluations?filter=future");

        public static async Task<JsonElement> GetSchoolCateringAsync(string token, DateTime from, DateTime to)
        {
            var endpoints = new[]
            {
                $"/m/meals/menus?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
                $"/m/meals?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
                $"/m/school-catering?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
                $"/m/school_catering?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"
            };

            Exception? lastException = null;
            foreach (var endpoint in endpoints)
            {
                try
                {
                    return await GetJsonWithRefreshAsync(token, endpoint);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            throw lastException ?? new Exception("Napaka pri nalaganju prehrane.");
        }

        public static async Task<JsonElement> SelectMealMenuAsync(string token, string type, DateTime date, int menuId)
        {
            var payloads = new object[]
            {
                new { type, date = date.ToString("yyyy-MM-dd"), menu = menuId },
                new { type, date = date.ToString("yyyy-MM-dd"), menu_id = menuId },
                new { meal_type = type, date = date.ToString("yyyy-MM-dd"), menu = menuId }
            };

            Exception? lastException = null;
            foreach (var payload in payloads)
            {
                var request = CreateRequest(HttpMethod.Post, "/m/meals/meal", token);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _client.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    lastException = new Exception($"Napaka pri prijavi na meni ({(int)response.StatusCode}): {TryGetErrorMessage(json)}");
                    continue;
                }

                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
                return doc.RootElement.Clone();
            }

            throw lastException ?? new Exception("Napaka pri prijavi na meni.");
        }

        public static async Task<JsonElement> GetCommunicationNewsAsync(string token)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, "/api/me/news", token));

        public static async Task<JsonElement> GetCommunicationMeAsync(string token)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, "/api/me?institution_id=0", token));

        public static async Task<JsonElement> GetCommunicationInstitutionsAsync(string token)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, "/api/me/institutions", token));

        public static async Task<JsonElement> GetMessageChannelsAsync(string token, int limit = 20, string? to = null)
        {
            var extra = string.IsNullOrWhiteSpace(to) ? string.Empty : $"&to={Uri.EscapeDataString(to)}";
            return await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/channels?type=message&limit={limit}&only_pinned=false&exclude_pinned=true{extra}", token));
        }

        public static async Task<JsonElement> GetChannelDetailsAsync(string token, string channelId)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/channels/{channelId}", token));

        public static async Task<JsonElement> GetChannelMessagesAsync(string token, string channelId, int limit = 25)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/channels/{channelId}/messages?limit={limit}&with=files,user,labels", token));

        public static async Task<JsonElement> SendChannelMessageAsync(string token, string channelId, string message)
        {
            var request = CreateCommunicationRequest(HttpMethod.Post, $"/api/channels/{channelId}/messages", token);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                channel_id = channelId,
                body = WrapHtml(message),
                page_url = $"https://komunikacija.easistent.com/inbox/messages/{channelId}"
            }, _jsonOptions), Encoding.UTF8, "application/json");
            return await SendCommunicationJsonAsync(request);
        }

        public static async Task<JsonElement> GetChannelsAsync(string token, string channelType = "message", int limit = 20, string? to = null)
        {
            var extra = string.IsNullOrWhiteSpace(to) ? string.Empty : $"&to={Uri.EscapeDataString(to)}";
            return await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/channels?type={channelType}&limit={limit}&only_pinned=false&exclude_pinned=true{extra}", token));
        }

        public static async Task<JsonElement> GetCommunicationUnseenAsync(string token)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, "/api/me/unseen", token));

        public static async Task<JsonElement> GetCommunicationChannelCountAsync(string token)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, "/api/channels/count", token));

        public static async Task<JsonElement> GetCommunicationContactGroupsAsync(string token)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, "/api/contacts/groups", token));

        public static async Task<JsonElement> GetCommunicationContactsForInstitutionAsync(string token, int institutionId)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/contacts/{institutionId}/0", token));

        public static async Task<JsonElement> GetChannelLabelsAsync(string token, string channelId)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/channels/{channelId}/labels", token));

        public static async Task<JsonElement> GetCommunicationLabelsAsync(string token)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, "/api/labels", token));

        public static async Task<JsonElement> GetChannelMeetingsAsync(string token, string channelId)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/channels/{channelId}/meetings", token));

        public static async Task<JsonElement> GetChannelDraftsAsync(string token, string channelId)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/channels/{channelId}/drafts", token));

        public static async Task<JsonElement> SaveChannelDraftAsync(string token, string channelId, string body)
        {
            var request = CreateCommunicationRequest(HttpMethod.Post, $"/api/channels/{channelId}/drafts", token);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                channel_id = channelId,
                body = WrapHtml(body)
            }, _jsonOptions), Encoding.UTF8, "application/json");
            return await SendCommunicationJsonAsync(request);
        }

        public static async Task<JsonElement> SearchCommunicationContactsAsync(string token, string query, int institutionId)
            => await GetCommunicationContactsForInstitutionAsync(token, institutionId);

        public static async Task<JsonElement> CreateNewMessageChannelAsync(string token, string title, string body, int institutionId, IEnumerable<JsonElement> participants)
        {
            var participantPayload = participants
                .Select(participant =>
                {
                    if (participant.ValueKind == JsonValueKind.Object
                        && participant.TryGetProperty("id", out var idProp)
                        && idProp.TryGetInt32(out var participantId))
                    {
                        return new { id = participantId } as object;
                    }
                    return JsonSerializer.Deserialize<object>(participant.GetRawText())!;
                })
                .ToArray();

            var request = CreateCommunicationRequest(HttpMethod.Post, "/api/channels?type=message", token);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                type = "message",
                title,
                body = WrapHtml(body),
                institutionId,
                participants = participantPayload,
                blockedSelectedUsers = Array.Empty<object>()
            }, _jsonOptions), Encoding.UTF8, "application/json");
            return await SendCommunicationJsonAsync(request);
        }

        private static async Task<JsonElement> SendCommunicationJsonAsync(HttpRequestMessage request)
        {
            // Proactively ensure we have a communication token before sending
            if (string.IsNullOrWhiteSpace(_communicationToken) && !string.IsNullOrWhiteSpace(AuthState.AccessToken))
            {
                await AcquireCommunicationTokenAsync(AuthState.AccessToken);
                // Rebuild authorization header with the newly acquired token
                request.Headers.Remove("Authorization");
                if (!string.IsNullOrWhiteSpace(_communicationToken))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_communicationToken}");
                }
            }

            var response = await _communicationClient.SendAsync(request);
            UpdateCommunicationToken(response);
            var json = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                (response, json) = await RetryCommunicationUnauthorizedAsync(request, response, json);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Napaka ({(int)response.StatusCode}): {TryGetErrorMessage(json)}");
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.Clone();
        }

        private static async Task<(HttpResponseMessage response, string json)> RetryCommunicationUnauthorizedAsync(HttpRequestMessage request, HttpResponseMessage response, string json)
        {
            if (!string.IsNullOrWhiteSpace(AuthState.RefreshToken))
            {
                try
                {
                    var freshAccessToken = await RefreshTokenAsync(AuthState.RefreshToken);
                    if (!string.IsNullOrWhiteSpace(freshAccessToken))
                    {
                        AuthState.AccessToken = freshAccessToken;
                    }
                }
                catch
                {
                }
            }

            _communicationToken = string.Empty;
            await AcquireCommunicationTokenAsync(AuthState.AccessToken);

            using var retryRequest = CloneRequest(request, null);
            response = await _communicationClient.SendAsync(retryRequest);
            UpdateCommunicationToken(response);
            json = await response.Content.ReadAsStringAsync();

            return (response, json);
        }

        private static async Task AcquireCommunicationTokenAsync(string eaToken)
        {
            try
            {
                // The official method: exchange the eA access token for a communication JWT
                var tokenRequest = CreateRequest(HttpMethod.Get, "/m/communication_login_get_token", eaToken);
                using var tokenResponse = await _client.SendAsync(tokenRequest);
                if (tokenResponse.IsSuccessStatusCode)
                {
                    var json = await tokenResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("token", out var t))
                    {
                        var tok = t.GetString();
                        if (!string.IsNullOrWhiteSpace(tok))
                        {
                            _communicationToken = tok;
                            return;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage source, string? token)
        {
            var request = CreateCommunicationRequest(source.Method, source.RequestUri?.ToString() ?? string.Empty, token);
            foreach (var header in source.Headers)
            {
                if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                request.Headers.Remove(header.Key);
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (source.Content != null)
            {
                var body = source.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var mediaType = source.Content.Headers.ContentType?.MediaType ?? "application/json";
                request.Content = new StringContent(body, Encoding.UTF8, mediaType);
            }
            return request;
        }

        private static void UpdateCommunicationToken(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("authorization", out var values))
            {
                var header = values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    _communicationToken = header[7..];
                }
            }
        }

        public static Task<JsonElement> GetUserAsync(string token)
            => GetJsonWithRefreshAsync(token, "/m/user");

        public static Task<JsonElement> GetChildInfoAsync(string token)
            => GetJsonWithRefreshAsync(token, "/m/me/child");

        public static Task<JsonElement> GetExamsAsync(string token)
            => GetJsonWithRefreshAsync(token, "/m/exams");

        public static Task<JsonElement> GetPraisesAsync(string token)
            => GetJsonWithRefreshAsync(token, "/m/praises_and_improvements");

        public static Task<JsonElement> GetConsentsAsync(string token)
            => GetJsonWithRefreshAsync(token, "/m/consents");

        public static Task<JsonElement> GetConsentAsync(string token, string uuid)
            => GetJsonWithRefreshAsync(token, $"/m/consent/{Uri.EscapeDataString(uuid)}");

        public static async Task<JsonElement> SubmitConsentAsync(string token, string uuid, object payload)
        {
            var request = CreateRequest(HttpMethod.Post, $"/m/submit_consent/{Uri.EscapeDataString(uuid)}", token);
            request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await _client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Napaka ({(int)response.StatusCode}): {TryGetErrorMessage(json)}");
            }
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.Clone();
        }

        public static Task<JsonElement> GetMealsStatusAsync(string token, DateTime from, DateTime to)
            => GetJsonWithRefreshAsync(token, $"/m/meals/status?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        public static Task<JsonElement> GetOverviewBasicAsync(string token)
            => GetJsonWithRefreshAsync(token, "/m/overview/basic");

        public static async Task LogoutAsync(string token, string deviceId = "")
        {
            var id = string.IsNullOrWhiteSpace(deviceId) ? "eaclient-desktop" : deviceId;
            var request = CreateRequest(HttpMethod.Delete, $"/m/logout?device_id={Uri.EscapeDataString(id)}", token);
            var response = await _client.SendAsync(request);
            _ = await response.Content.ReadAsStringAsync();
        }

        private static string WrapHtml(string text)
            => $"<p>{WebUtility.HtmlEncode(text).Replace("\n", "<br />")}</p>";

        private static string TryGetErrorMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                {
                    return msg.GetString() ?? json;
                }
                if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    return err.GetString() ?? json;
                }
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var first = doc.RootElement.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("message", out var nestedMsg))
                    {
                        return nestedMsg.GetString() ?? json;
                    }
                }
            }
            catch
            {
            }
            return json.Length > 200 ? json[..200] : json;
        }
    }
}
