using System.Text.Json.Serialization;

namespace EAClient.Models
{
    public class AccessTokenInfo
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("expiration_date")]
        public string ExpirationDate { get; set; } = string.Empty;
    }

    public class LoginUserInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        [JsonPropertyName("access_token")]
        public AccessTokenInfo AccessToken { get; set; } = new();

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("user")]
        public LoginUserInfo User { get; set; } = new();
    }
}
