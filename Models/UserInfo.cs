using System.Text.Json.Serialization;

namespace EAClient.Models
{
    public class UserInfo
    {
        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("gender")]
        public string Gender { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("plus_enabled")]
        public bool PlusEnabled { get; set; }

        [JsonPropertyName("short_name")]
        public string ShortName { get; set; } = string.Empty;

        [JsonPropertyName("student_id")]
        public int StudentId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }
}
