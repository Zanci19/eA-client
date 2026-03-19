using System;
using System.Collections.Generic;
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
            var request = new HttpRequestMessage(method, url);
            request.Headers.TryAddWithoutValidation("x-app-name", "child");
            request.Headers.TryAddWithoutValidation("x-client-version", "11101");
            request.Headers.TryAddWithoutValidation("x-client-platform", "android");
            request.Headers.TryAddWithoutValidation("app", "new_mobile_app");
            if (!string.IsNullOrEmpty(token))
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
            return request;
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
                throw new Exception($"Napaka pri prijavi ({(int)response.StatusCode}): {TryGetErrorMessage(json)}");

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
                throw new Exception($"Napaka pri osveževanju žetona: {response.StatusCode}");

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var at)
                && at.TryGetProperty("token", out var t))
                return t.GetString() ?? string.Empty;

            throw new Exception("Napaka pri razčlenjevanju odgovora za osvežitev žetona.");
        }

        public static async Task<List<TimetableEvent>> GetTimetableAsync(string token, DateTime from, DateTime to)
        {
            var url = $"/m/timetable/events?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
            var request = CreateRequest(HttpMethod.Get, url, token);
            var response = await _client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Napaka pri nalaganju urnika ({(int)response.StatusCode}).");

            var result = JsonSerializer.Deserialize<TimetableResponse>(json);
            return result?.Events ?? new List<TimetableEvent>();
        }

        private static async Task<JsonElement> GetJsonAsync(string token, string url)
        {
            var request = CreateRequest(HttpMethod.Get, url, token);
            var response = await _client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Napaka ({(int)response.StatusCode}): {TryGetErrorMessage(json)}");

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        public static Task<JsonElement> GetGradesAsync(string token, DateTime from, DateTime to)
            => GetJsonAsync(token, $"/m/grades?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        public static Task<JsonElement> GetAbsencesAsync(string token, DateTime from, DateTime to)
            => GetJsonAsync(token, $"/m/absences?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        public static Task<JsonElement> GetHomeworkAsync(string token, DateTime from, DateTime to)
            => GetJsonAsync(token, $"/m/homework?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        public static Task<JsonElement> GetEvaluationsAsync(string token)
            => GetJsonAsync(token, "/m/evaluations?filter=future");

        public static async Task<JsonElement> GetSchoolCateringAsync(string token, DateTime from, DateTime to)
        {
            try
            {
                return await GetJsonAsync(token, $"/m/school-catering?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            }
            catch (HttpRequestException)
            {
                return await GetJsonAsync(token, $"/m/school_catering?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            }
        }

        public static Task<JsonElement> GetUserAsync(string token)
            => GetJsonAsync(token, "/m/user");

        public static Task<JsonElement> GetChildInfoAsync(string token)
            => GetJsonAsync(token, "/m/me/child");

        private static string TryGetErrorMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return msg.GetString() ?? json;
                if (doc.RootElement.TryGetProperty("error", out var err))
                    return err.GetString() ?? json;
            }
            catch { }
            return json.Length > 200 ? json[..200] : json;
        }
    }
}
