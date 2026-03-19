using System;
using System.Collections.Generic;
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

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? token = null)
        {
            var request = new HttpRequestMessage(method, WithMobileFlag(url));
            request.Headers.TryAddWithoutValidation("x-app-name", "child");
            request.Headers.TryAddWithoutValidation("x-client-version", "11101");
            request.Headers.TryAddWithoutValidation("x-client-platform", "android");
            request.Headers.TryAddWithoutValidation("app", "new_mobile_app");

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
                if (AuthState.UserId > 0)
                {
                    request.Headers.TryAddWithoutValidation("X-Child-Id", AuthState.UserId.ToString());
                }
            }

            return request;
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
