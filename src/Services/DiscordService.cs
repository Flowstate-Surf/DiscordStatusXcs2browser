using ImperfectServerStatus.Helpers;
using ImperfectServerStatus.Models;
using ImperfectServerStatus.Models.Discord;
using ImperfectServerStatus.Models.MessageInfo;
using ImperfectServerStatus.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ImperfectServerStatus.Services
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

                return (await httpClient.PostAsync(webhookRequestUri, content)).EnsureSuccessStatusCode();
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

                HttpResponseMessage response = (await httpClient.PatchAsync($"{webhookUri}", content)).EnsureSuccessStatusCode();
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
            var connectUrl = "";
            if (statusData.IpAddress != null)
            {
                connectUrl = "https://cs2serverlist.com/server/" + statusData.IpAddress;
            }

            var mapImageUrl = !string.IsNullOrEmpty(statusData.MapName)
                ? $"https://raw.githubusercontent.com/hazmat321/SurfMapPics/Maps-and-bonuses/csgo/{statusData.MapName}.jpg"
                : null;

            var embed = new Embed()
            {
                Title = statusData.ServerName ?? "Server Name",
                Description = "",
                Type = "rich",
                Url = connectUrl,
                Color = 16724530,
                Timestamp = DateTime.Now,
                Image = mapImageUrl != null ? new EmbedImage { Url = mapImageUrl } : null,
                Fields = new List<EmbedField>(){
                    new EmbedField(){
                        Name = "🖥️ Server",
                        Value = statusData.ServerName ?? "Server Name",
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
                        Value = statusData.MapName ?? "",
                        Inline = true
                    },
                    new EmbedField()
                    {
                        Name = "🌐 IP Address",
                        Value = statusData.IpAddress ?? "",
                        Inline = true
                    },
                    new EmbedField()
                    {
                        Name = "🔗 Connect Link",
                        Value = $"[Click here]({connectUrl})",
                        Inline = true
                    },
                    new EmbedField()
                    {
                        Name = "⌨️ Quick Connect",
                        Value = $"`connect {statusData.IpAddress}`",
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
                statusEmbed.Title = statusData.ServerName;
                statusEmbed.Timestamp = statusData.Timestamp;


                var connectUrl = "";
                if (statusData.IpAddress != null)
                {
                    connectUrl = "https://cs2serverlist.com/server/" + statusData.IpAddress;
                }
                statusEmbed.Url = connectUrl;

                var mapNameField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "🗺️ Map");

                if (mapNameField != null)
                {
                    mapNameField.Value = statusData.MapName ?? "";
                }

                var ipAddressField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "🌐 IP Address");

                if (ipAddressField != null)
                {
                    ipAddressField.Value = statusData.IpAddress ?? "";
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
                    serverNameField.Value = statusData.ServerName ?? "";
                }

                var connectLinkField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "🔗 Connect Link");

                if (connectLinkField != null)
                {
                    connectLinkField.Value = $"[Click here]({connectUrl})";
                }

                var quickConnectField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "⌨️ Quick Connect");

                if (quickConnectField != null)
                {
                    quickConnectField.Value = $"`connect {statusData.IpAddress}`";
                }

                var playersField = statusEmbed.Fields.FirstOrDefault(f => f.Name == "👥 Players");

                if (playersField != null)
                {
                    playersField.Value = $"{statusData.PlayerCount}/{statusData.MaxPlayers}";
                }

                statusEmbed.Image = !string.IsNullOrEmpty(statusData.MapName)
                    ? new EmbedImage { Url = $"https://raw.githubusercontent.com/hazmat321/SurfMapPics/Maps-and-bonuses/csgo/{statusData.MapName}.jpg" }
                    : null;
            }

            return statusEmbed;
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
