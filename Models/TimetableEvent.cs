using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EAClient.Models
{
    public class TimetableEvent
    {
        [JsonPropertyName("classroom")]
        public string Classroom { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#4b4bbf";

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("homework")]
        public List<object> Homework { get; set; } = new();

        [JsonPropertyName("lesson")]
        public string Lesson { get; set; } = string.Empty;

        [JsonPropertyName("teachers")]
        public List<string> Teachers { get; set; } = new();

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("title_short")]
        public string TitleShort { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    public class TimetableResponse
    {
        [JsonPropertyName("events")]
        public List<TimetableEvent> Events { get; set; } = new();
    }
}
