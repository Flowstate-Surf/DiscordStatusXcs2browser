using System.Text.Json.Serialization;

namespace DiscordStatus.Models.Discord
{
    public class ActionRowComponent
    {
        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("components")]
        public List<ButtonComponent> Components { get; set; }
    }
}
