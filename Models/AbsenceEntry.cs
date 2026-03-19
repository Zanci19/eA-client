using System.Text.Json.Serialization;

namespace EAClient.Models
{
    public class AbsenceEntry
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonPropertyName("hours")]
        public int Hours { get; set; } = 1;
    }
}
