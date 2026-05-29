namespace DiscordStatus.Models
{
    public class StatusData
    {
        public string ServerName { get; set; } = "";

        public string IpAddress { get; set; } = "";

        public bool ServerOnline { get; set; } = true;

        public string MapName { get; set; } = "";

        public int PlayerCount { get; set; } = 0;

        public int MaxPlayers { get; set; } = 0;

        public string ConnectUrl { get; set; } = "";

        public string? BannerUrl { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
