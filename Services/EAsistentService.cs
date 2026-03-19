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
        private static readonly HttpClient _client = new HttpClient
        {
            BaseAddress = new Uri("https://www.easistent.com"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly HttpClient _communicationClient = new HttpClient
        {
            BaseAddress = new Uri("https://komunikacija.easistent.com"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static string _communicationToken = string.Empty;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? token = null)
        {
            var request = new HttpRequestMessage(method, WithMobileFlag(url));
            ApplyCommonHeaders(request, token);
            request.Headers.TryAddWithoutValidation("app", "new_mobile_app");
            return request;
        }

        private static HttpRequestMessage CreateCommunicationRequest(HttpMethod method, string url, string? token = null, string contentType = "application/json")
        {
            var request = new HttpRequestMessage(method, WithMobileFlag(url));
            ApplyCommonHeaders(request, string.IsNullOrWhiteSpace(token) ? _communicationToken : token);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                request.Headers.TryAddWithoutValidation("Content-Type", contentType);
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
                return t.GetString() ?? string.Empty;
            }

            throw new Exception("Napaka pri razčlenjevanju odgovora za osvežitev žetona.");
        }

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
            var request = CreateRequest(HttpMethod.Post, "/m/meals/meal", token);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                type,
                date = date.ToString("yyyy-MM-dd"),
                menu = menuId
            }), Encoding.UTF8, "application/json");

            var response = await _client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Napaka pri prijavi na meni ({(int)response.StatusCode}): {TryGetErrorMessage(json)}");
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.Clone();
        }

        public static async Task<JsonElement> GetCommunicationNewsAsync(string token)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, "/api/me/news", token));

        public static async Task<JsonElement> GetCommunicationMeAsync(string token)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, "/api/me", token));

        public static async Task<JsonElement> GetMessageChannelsAsync(string token, int limit = 20, string? to = null)
        {
            var extra = string.IsNullOrWhiteSpace(to) ? string.Empty : $"&to={Uri.EscapeDataString(to)}";
            return await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/channels?type=message&limit={limit}{extra}", token));
        }

        public static async Task<JsonElement> GetChannelDetailsAsync(string token, string channelId)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/channels/{channelId}", token));

        public static async Task<JsonElement> GetChannelMessagesAsync(string token, string channelId, int limit = 25)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/channels/{channelId}/messages?with=files&limit={limit}", token));

        public static async Task<JsonElement> SendChannelMessageAsync(string token, string channelId, string message)
        {
            var request = CreateCommunicationRequest(HttpMethod.Post, $"/api/channels/{channelId}/messages", token);
            request.Content = new StringContent(JsonSerializer.Serialize(new { message }, _jsonOptions), Encoding.UTF8, "application/json");
            return await SendCommunicationJsonAsync(request);
        }

        public static async Task<JsonElement> SearchCommunicationContactsAsync(string token, string query, int institutionId)
            => await SendCommunicationJsonAsync(CreateCommunicationRequest(HttpMethod.Get, $"/api/search/contacts/{institutionId}/{Uri.EscapeDataString(query)}", token));

        public static async Task<JsonElement> CreateNewMessageChannelAsync(string token, string title, string body, int institutionId, IEnumerable<JsonElement> participants)
        {
            var participantPayload = participants
                .Select(participant => JsonSerializer.Deserialize<object>(participant.GetRawText())!)
                .ToArray();

            var request = CreateCommunicationRequest(HttpMethod.Post, "/api/channels?type=message", token);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                type = "message",
                title,
                body,
                participants = participantPayload,
                blockedSelectedUsers = Array.Empty<object>(),
                institutionId
            }, _jsonOptions), Encoding.UTF8, "application/json");
            return await SendCommunicationJsonAsync(request);
        }

        private static async Task<JsonElement> SendCommunicationJsonAsync(HttpRequestMessage request)
        {
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
                        using var refreshedRequest = CloneRequest(request, freshAccessToken);
                        response = await _communicationClient.SendAsync(refreshedRequest);
                        UpdateCommunicationToken(response);
                        json = await response.Content.ReadAsStringAsync();
                        if (response.IsSuccessStatusCode)
                        {
                            return (response, json);
                        }
                    }
                }
                catch
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(AuthState.AccessToken) && request.Headers.Authorization == null)
            {
                using var retryRequest = CloneRequest(request, AuthState.AccessToken);
                response = await _communicationClient.SendAsync(retryRequest);
                UpdateCommunicationToken(response);
                json = await response.Content.ReadAsStringAsync();
            }

            return (response, json);
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage source, string token)
        {
            var request = CreateCommunicationRequest(source.Method, source.RequestUri?.PathAndQuery ?? string.Empty, token);
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
            }
            catch
            {
            }
            return json.Length > 200 ? json[..200] : json;
        }
    }
}
