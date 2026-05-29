using DiscordStatus.Models.MessageInfo;
using System.Text.Json.Serialization;

namespace DiscordStatus.Models
{
    public enum CS2BrowserBannerSize
    {
        Big,
        Small
    }

    public class Config
    {
        /// <summary>
        /// Config version number
        /// </summary>
        [JsonPropertyName("ConfigVersion")]
        public int Version { get; set; } = 2;

        /// <summary>
        /// The server IP to display and create connect links to
        /// </summary>
        public string ServerIp { get; set; } = "";

        /// <summary>
        /// Override the server name displayed in the embed (leave empty to use sv_hostname)
        /// </summary>
        public string ServerName { get; set; } = "";

        /// <summary>
        /// cs2browser.net server id (from the URL https://cs2browser.net/server/{id}).
        /// Accepts either the numeric id or the UUID-style id. Used for both the connect link
        /// and the embed banner image.
        /// </summary>
        public string CS2BrowserServerId { get; set; } = "";

        /// <summary>
        /// Which cs2browser banner to embed when CS2BrowserServerId is set. Big = 550x95, Small = 350x20.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CS2BrowserBannerSize CS2BrowserBannerSize { get; set; } = CS2BrowserBannerSize.Big;

        /// <summary>
        /// Server status message information
        /// </summary>
        public StatusMessageInfo StatusInfo { get; set; } = new();
    }
}