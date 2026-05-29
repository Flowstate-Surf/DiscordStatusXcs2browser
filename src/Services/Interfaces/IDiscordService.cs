using DiscordStatus.Models;
using DiscordStatus.Models.Discord;
using DiscordStatus.Models.MessageInfo;

namespace DiscordStatus.Services.Interfaces
{
    public interface IDiscordService
    {
        Task<string> CreateStatusMessageAsync(StatusMessageInfo messageInfo, WebhookMessage webhookMessage);

        Task UpdateStatusMessageAsync(StatusMessageInfo messageInfo, WebhookMessage webhookMessage);

        WebhookMessage CreateWebhookMessage(StatusMessageInfo statusMessageInfo, StatusData statusData);

        WebhookMessage UpdateWebhookMessage(WebhookMessage webhookMessage, StatusData statusData);
    }
}
