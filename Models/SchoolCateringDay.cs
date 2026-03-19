using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EAClient.Models
{
    public class CateringMenuItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    public class SchoolCateringDay
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("menus")]
        public List<CateringMenuItem> Menus { get; set; } = new();
    }
}
