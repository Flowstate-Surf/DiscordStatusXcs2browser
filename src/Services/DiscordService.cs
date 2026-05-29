using DiscordStatus.Helpers;
using DiscordStatus.Models;
using DiscordStatus.Models.Discord;
using DiscordStatus.Models.MessageInfo;
using DiscordStatus.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DiscordStatus.Services
{
    public class DiscordService : IDiscordService
    {
        private readonly ILogger<DiscordService> _logger;

        public DiscordService(ILogger<DiscordService> logger)
        {
            _logger = logger;
        }

        public async Task<string> CreateStatusMessageAsync(StatusMessageInfo messageInfo, WebhookMessage webhookMessage)
        {
            var serializeOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance
            };

            var serializedMessage = JsonSerializer.Serialize(webhookMessage, serializeOptions);

            /// No message exists, send first message
            var response = await PostJsonToWebhook(serializedMessage, messageInfo);

            if (response != null)
            {
                return await GetDiscordMessageId(response);
            }
            else
            {
                _logger.LogError("Something went wrong getting the response after posting the Discord message.");
                return string.Empty;
            }
        }

        public async Task UpdateStatusMessageAsync(StatusMessageInfo messageInfo, WebhookMessage webhookMessage)
        {
            var serializeOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance
            };

            /// Add message ID to Webhook URI
            var messageEditUri = messageInfo.WebhookUri + "/messages/" + messageInfo.MessageId;

            var serializedMessage = JsonSerializer.Serialize(webhookMessage, serializeOptions);

            await PatchJsonToWebhook(serializedMessage, messageEditUri);
        }

        public WebhookMessage CreateWebhookMessage(StatusMessageInfo statusMessageInfo, StatusData statusData)
        {
            return new WebhookMessage
            {
                Content = null,
                //ActionRowComponents = new List<ActionRowComponent>()
                //{
                //    new ActionRowComponent()
                //    {
                //        Type = 1,
                //        Components = new List<ButtonComponent>()
                //        {
                //            //CreateButtonComponent(statusMessageInfo)
                //        }
                //    }
                //},
                Embeds = new List<Embed>
                {
                    CreateEmbed(statusMessageInfo, statusData)
                }
            };
        }

        public WebhookMessage UpdateWebhookMessage(WebhookMessage webhookMessage, StatusData statusData)
        {
            if (webhookMessage.Embeds != null)
            {
                var statusEmbed = webhookMessage.Embeds.FirstOrDefault();

                statusEmbed = UpdateEmbed(statusEmbed, statusData);
            }

            return webhookMessage;
        }

        private async Task<HttpResponseMessage> PostJsonToWebhook(string serializedMessage, StatusMessageInfo messageInfo)
        {
            try
            {
                var webhookRequestUri = $"{messageInfo.WebhookUri}?wait=true";

                var content = new StringContent(serializedMessage, Encoding.UTF8, "application/json");

                using HttpClient httpClient = new HttpClient();

                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await httpClient.PostAsync(webhookRequestUri, content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Discord rejected POST ({status}).", (int)response.StatusCode);
                    return null;
                }
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send: {message}", ex.Message);
            }

            return null;
        }

        private async Task PatchJsonToWebhook(string serializedMessage, string webhookUri)
        {
            try
            {
                var content = new StringContent(serializedMessage, Encoding.UTF8, "application/json");

                using HttpClient httpClient = new HttpClient();

                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json-patch+json"));

                var response = await httpClient.PatchAsync($"{webhookUri}", content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Discord rejected PATCH ({status}).", (int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send: {message}", ex.Message);
            }
        }

        private async Task<string> GetDiscordMessageId(HttpResponseMessage response)
        {
            var deserializedResponse = JsonSerializer.Deserialize<WebhookResponse>(
                await response.Content.ReadAsStringAsync(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (deserializedResponse != null
                && !string.IsNullOrEmpty(deserializedResponse.Id))
            {
                return deserializedResponse.Id;
            }
            else
            {
                return string.Empty;
            }
        }

        private Embed CreateEmbed(StatusMessageInfo statusMessageInfo, StatusData statusData)
        {
            var connectUrl = statusData.ConnectUrl ?? "";
            var titleUrl = IsHttpUrl(connectUrl) ? connectUrl : null;

            var serverName = !string.IsNullOrWhiteSpace(statusData.ServerName) ? statusData.ServerName : "Server";
            var mapName = !string.IsNullOrWhiteSpace(statusData.MapName) ? statusData.MapName : "Unknown";
            var ipAddress = !string.IsNullOrWhiteSpace(statusData.IpAddress) ? statusData.IpAddress : "Not configured";
            var connectField = !string.IsNullOrWhiteSpace(connectUrl) ? $"[Click here]({connectUrl})" : "Not configured";
            var quickConnect = !string.IsNullOrWhiteSpace(statusData.IpAddress) ? $"`connect {statusData.IpAddress}`" : "`Not configured`";

            var imageUrl = !string.IsNullOrEmpty(statusData.BannerUrl)
                ? statusData.BannerUrl
                : (!string.IsNullOrWhiteSpace(statusData.MapName)
                    ? $"https://raw.githubusercontent.com/hazmat321/SurfMapPics/Maps-and-bonuses/csgo/{statusData.MapName}.jpg"
                    : null);

            var embed = new Embed()
            {
                Title = serverName,
                Description = "",
                Type = "rich",
                Url = titleUrl,
                Color = 16724530,
                Timestamp = DateTime.Now,
                Image = imageUrl != null ? new EmbedImage { Url = imageUrl } : null,
                Fields = new List<EmbedField>(){
                    new EmbedField(){
                        Name = "🖥️ Server",
                        Value = serverName,
                        Inline = true
                    },
                    new EmbedField(){
                        Name = "📶 Status",
                        Value = "Online 🟢",
                        Inline = true
                    },
                    new EmbedField()
                    {
                        Name = "👥 Players",
                        Value = $"{statusData.PlayerCount}/{statusData.MaxPlayers}",
                        Inline = true
                    },
                    new EmbedField()
                    {
                        Name = "🗺️ Map",
                        Value = mapName,
                        Inline = true
                    },
                    new EmbedField()
                    {
                        Name = "🌐 IP Address",
                        Value = ipAddress,
                        Inline = true
                    },
                    new EmbedField()
                    {
                        Name = "🔗 Connect Link",
                        Value = connectField,
                        Inline = true
                    },
                    new EmbedField()
                    {
                        Name = "⌨️ Quick Connect",
                        Value = quickConnect,
                        Inline = false
                    }
                }
            };

            return embed;
        }

        private Embed? UpdateEmbed(Embed? statusEmbed, StatusData statusData)
        {
            if (statusEmbed != null)
            {
                statusEmbed.Title = !string.IsNullOrWhiteSpace(statusData.ServerName) ? statusData.ServerName : "Server";
                statusEmbed.Timestamp = statusData.Timestamp;


                var connectUrl = statusData.ConnectUrl ?? "";
                statusEmbed.Url = IsHttpUrl(connectUrl) ? connectUrl : null;

                var mapNameField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "🗺️ Map");

                if (mapNameField != null)
                {
                    mapNameField.Value = !string.IsNullOrWhiteSpace(statusData.MapName) ? statusData.MapName : "Unknown";
                }

                var ipAddressField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "🌐 IP Address");

                if (ipAddressField != null)
                {
                    ipAddressField.Value = !string.IsNullOrWhiteSpace(statusData.IpAddress) ? statusData.IpAddress : "Not configured";
                }

                var serverOnlineStatusField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "📶 Status");

                if (serverOnlineStatusField != null)
                {
                    if (statusData.ServerOnline is false)
                    {
                        serverOnlineStatusField.Value = "Offline 🔴";
                    }
                    else
                    {
                        serverOnlineStatusField.Value = "Online 🟢";
                    }
                }

                var serverNameField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "🖥️ Server");

                if (serverNameField != null)
                {
                    serverNameField.Value = !string.IsNullOrWhiteSpace(statusData.ServerName) ? statusData.ServerName : "Server";
                }

                var connectLinkField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "🔗 Connect Link");

                if (connectLinkField != null)
                {
                    connectLinkField.Value = !string.IsNullOrWhiteSpace(connectUrl) ? $"[Click here]({connectUrl})" : "Not configured";
                }

                var quickConnectField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "⌨️ Quick Connect");

                if (quickConnectField != null)
                {
                    quickConnectField.Value = !string.IsNullOrWhiteSpace(statusData.IpAddress) ? $"`connect {statusData.IpAddress}`" : "`Not configured`";
                }

                var playersField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "👥 Players");

                if (playersField != null)
                {
                    playersField.Value = $"{statusData.PlayerCount}/{statusData.MaxPlayers}";
                }

                statusEmbed.Image = !string.IsNullOrEmpty(statusData.BannerUrl)
                    ? new EmbedImage { Url = statusData.BannerUrl }
                    : (!string.IsNullOrWhiteSpace(statusData.MapName)
                        ? new EmbedImage { Url = $"https://raw.githubusercontent.com/hazmat321/SurfMapPics/Maps-and-bonuses/csgo/{statusData.MapName}.jpg" }
                        : null);
            }

            return statusEmbed;
        }

        private static bool IsHttpUrl(string? url)
        {
            return !string.IsNullOrEmpty(url)
                && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        private ButtonComponent CreateButtonComponent(StatusMessageInfo statusMessageInfo)
        {
            var buttonComponent = new ButtonComponent()
            {
                Type = 2,
                Style = 5,
                Label = "Connect to server",
                Url = "",
                Disabled = false,
                Emoji = new Emoji()
                {
                    Id = null,
                    Name = "🔗"
                }
            };

            return buttonComponent;
        }
    }
}
