using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using ImperfectServerStatus.Models;
using ImperfectServerStatus.Models.Discord;
using ImperfectServerStatus.Services.Interfaces;
using ImperfectServerStatus.Utils;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

namespace ImperfectServerStatus;

public partial class ImperfectServerStatus : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Imperfect-ServerStatus";
    public override string ModuleVersion => "1.4.0";
    public override string ModuleAuthor => "Imperfect Gamers - raz";
    public override string ModuleDescription => "A Discord server status plugin for Imperfect Gamers";

    public Config Config { get; set; } = new();
    public string ConfigPath;

    public StatusData _statusData = new();
    private WebhookMessage _webhookMessage;

    private readonly IConfigService _configService;
    private readonly IDiscordService _discordService;
    private readonly ILogger<ImperfectServerStatus> _logger;

    public ImperfectServerStatus(
        IConfigService configService,
        IDiscordService discordService,
        ILogger<ImperfectServerStatus> logger)
    {
        _configService = configService;
        _discordService = discordService;
        _logger = logger;
    }

    public void OnHostNameChanged(string hostName)
    {
        _statusData.ServerOnline = true;
        _statusData.ServerName = hostName;
        _statusData.MapName = Server.MapName;
        _statusData.PlayerCount = Utilities.GetPlayers().Count(p => !p.IsBot);
        _statusData.MaxPlayers = Server.MaxPlayers;

        if (string.IsNullOrEmpty(Config.StatusInfo.MessageId))
        {
            // Rebuild webhook message with current data before creating
            _webhookMessage = _discordService.CreateWebhookMessage(Config.StatusInfo, _statusData);
            CreateDiscordStatusMessage();
        }
        else
        {
            UpdateDiscordStatusMessage();
        }
    }

    public void OnMapStart(string mapName)
    {
        _statusData.ServerOnline = true;
        _statusData.MapName = mapName;
        _statusData.PlayerCount = 0;
        _statusData.MaxPlayers = Server.MaxPlayers;


        UpdateDiscordStatusMessage();
    }

    public override void Load(bool hotReload)
    {
        if (Config != null)
        {
            _statusData.Timestamp = DateTime.Now;
            _statusData.MapName = Server.MapName;
            _statusData.PlayerCount = Utilities.GetPlayers().Count(p => !p.IsBot);
            _statusData.MaxPlayers = Server.MaxPlayers;

            _webhookMessage = _discordService.CreateWebhookMessage(Config.StatusInfo, _statusData);

            RegisterListener<Listeners.OnHostNameChanged>(OnHostNameChanged);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);

            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

            if (string.IsNullOrEmpty(Config.StatusInfo.MessageId))
            {
                _logger.LogInformation("No MessageId found — sending initial Discord status message.");
                CreateDiscordStatusMessage();
            }
            else
            {
                _logger.LogInformation("MessageId found — updating existing Discord status message.");
                UpdateDiscordStatusMessage();
            }
        }
        else
        {
            _logger.LogInformation("The config file did not load correctly. Please check that there is a {ModuleName}.json file in the CounterStrikeSharp config directory.", ModuleName);
        };
    }

    public override void Unload(bool hotReload)
    {
        base.Unload(hotReload);
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        _statusData.PlayerCount = Utilities.GetPlayers().Count(p => !p.IsBot);
        UpdateDiscordStatusMessage();
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        _statusData.PlayerCount = Math.Max(0, Utilities.GetPlayers().Count(p => !p.IsBot) - 1);
        UpdateDiscordStatusMessage();
        return HookResult.Continue;
    }

    private void CreateDiscordStatusMessage()
    {
        // Send initial message
        Task.Run(async () =>
        {
            var messageId = await _discordService.CreateStatusMessageAsync(Config.StatusInfo, _webhookMessage);

            if (!string.IsNullOrEmpty(messageId))
            {
                Config.StatusInfo.MessageId = messageId;

                _configService.UpdateConfig(Config, ConfigPath);

                // Now that we have a MessageId, push the latest state
                await _discordService.UpdateStatusMessageAsync(Config.StatusInfo,
                    _discordService.UpdateWebhookMessage(_webhookMessage, _statusData));
            }
            else
            {
                _logger.LogError("Something went wrong getting a response when sending message.");
            }
        });
    }

    private void UpdateDiscordStatusMessage()
    {
        // Update the message
        Task.Run(async () =>
        {
            _statusData.Timestamp = DateTime.Now;

            WebhookMessage updatedWebhookMessage = _discordService.UpdateWebhookMessage(_webhookMessage, _statusData);

            await _discordService.UpdateStatusMessageAsync(Config.StatusInfo, updatedWebhookMessage);
        });
    }

    public void OnConfigParsed(Config config)
    {
        string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "";
        ConfigPath = $"{Server.GameDirectory}/csgo/addons/counterstrikesharp/configs/plugins/{AssemblyName}/{AssemblyName}.json";

        if (string.IsNullOrEmpty(AssemblyName))
        {
            _logger.LogWarning("There was an error getting the config path.");
        }

        if (config.Version < Config.Version)
        {
            _logger.LogWarning("The config version does not match current version: Expected: {0} | Current: {1}", Config.Version, config.Version);

            // TODO: Update config file to current version.
        }

        if (string.IsNullOrEmpty(config.ServerIp))
        {
            _logger.LogWarning("Server IP is missing from config. Set a value to create connection links and disply properly.");
        }
        else
        {
            _statusData.IpAddress = config.ServerIp;
        }

        if (!string.IsNullOrEmpty(config.ServerName))
        {
            _statusData.ServerName = config.ServerName;
        }

        Config = config;
    }
}