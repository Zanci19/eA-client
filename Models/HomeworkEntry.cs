using System.Text.Json.Serialization;

namespace EAClient.Models
{
    public class HomeworkEntry
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("teacher")]
        public string Teacher { get; set; } = string.Empty;
    }
}
