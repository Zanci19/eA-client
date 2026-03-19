using System.Text.Json.Serialization;

namespace EAClient.Models
{
    public class GradeEntry
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("subject_id")]
        public int SubjectId { get; set; }

        [JsonPropertyName("grade")]
        public int Grade { get; set; }

        [JsonPropertyName("date_of_entry")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("teacher_name")]
        public string Teacher { get; set; } = string.Empty;

        [JsonPropertyName("comment")]
        public string Note { get; set; } = string.Empty;
    }
}
