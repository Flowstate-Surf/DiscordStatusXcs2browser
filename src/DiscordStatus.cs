using DiscordStatus.Models;
using DiscordStatus.Models.Discord;
using DiscordStatus.Services;
using DiscordStatus.Services.Interfaces;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Plugins;
using System.Text.Json;

namespace DiscordStatus;

[PluginMetadata(
    Id = "DiscordStatus",
    Name = "Imperfect-ServerStatus",
    Version = "1.4.0",
    Author = "Imperfect Gamers - raz",
    Description = "A Discord server status plugin for Imperfect Gamers (SwiftlyS2 port).")]
public partial class DiscordStatus : BasePlugin
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<DiscordStatus> _logger;
    private readonly IConfigService _configService;
    private readonly IDiscordService _discordService;

    private Config _config = new();
    private string _configPath = string.Empty;

    private readonly StatusData _statusData = new();
    private WebhookMessage _webhookMessage = new();

    public DiscordStatus(ISwiftlyCore core) : base(core)
    {
        _core = core;
        _logger = core.LoggerFactory.CreateLogger<DiscordStatus>();
        _configService = new ConfigService(core.LoggerFactory.CreateLogger<ConfigService>());
        _discordService = new DiscordService(core.LoggerFactory.CreateLogger<DiscordService>());
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) { }

    public override void UseSharedInterface(IInterfaceManager interfaceManager) { }

    public override void Load(bool hotReload)
    {
        LoadConfig();

        _statusData.Timestamp = DateTime.Now;
        _statusData.IpAddress = _config.ServerIp;
        if (!string.IsNullOrEmpty(_config.ServerName))
        {
            _statusData.ServerName = _config.ServerName;
        }
        else
        {
            _statusData.ServerName = _core.ConVar.FindAsString("hostname")?.ValueAsString ?? string.Empty;
        }
        _statusData.MapName = _core.Engine.GlobalVars.MapName.Value ?? string.Empty;
        _statusData.ConnectUrl = BuildConnectUrl(_statusData.IpAddress, _config.CS2BrowserServerId);
        RefreshBannerUrl();
        RefreshLivePlayerData();

        _webhookMessage = _discordService.CreateWebhookMessage(_config.StatusInfo, _statusData);

        _core.Event.OnMapLoad += OnMapLoad;
        _core.GameEvent.HookPost<EventPlayerConnectFull>(OnPlayerConnectFull);
        _core.GameEvent.HookPost<EventPlayerDisconnect>(OnPlayerDisconnect);
        _core.GameEvent.HookPost<EventHostnameChanged>(OnHostnameChanged);

        if (!IsSnowflake(_config.StatusInfo.MessageId))
        {
            if (!string.IsNullOrEmpty(_config.StatusInfo.MessageId))
            {
                _logger.LogWarning("Stored MessageId '{id}' is not a Discord snowflake \u2014 clearing and re-creating.", _config.StatusInfo.MessageId);
                _config.StatusInfo.MessageId = string.Empty;
            }
            _logger.LogInformation("No MessageId found — sending initial Discord status message.");
            CreateDiscordStatusMessage();
        }
        else
        {
            _logger.LogInformation("MessageId found — updating existing Discord status message.");
            UpdateDiscordStatusMessage();
        }
    }

    public override void Unload()
    {
        _core.Event.OnMapLoad -= OnMapLoad;
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        _statusData.ServerOnline = true;
        _statusData.MapName = @event.MapName ?? string.Empty;
        RefreshLivePlayerData();
        UpdateDiscordStatusMessage();
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event)
    {
        RefreshLivePlayerData();
        UpdateDiscordStatusMessage();
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event)
    {
        // SwiftlyS2 fires PlayerDisconnect before PlayerCount reflects the change.
        _statusData.PlayerCount = Math.Max(0, GetHumanPlayerCount() - 1);
        _statusData.MaxPlayers = GetMaxPlayers();
        UpdateDiscordStatusMessage();
        return HookResult.Continue;
    }

    private HookResult OnHostnameChanged(EventHostnameChanged @event)
    {
        _statusData.ServerOnline = true;
        _statusData.ServerName = @event.Hostname ?? _statusData.ServerName;
        RefreshLivePlayerData();

        if (!IsSnowflake(_config.StatusInfo.MessageId))
        {
            _config.StatusInfo.MessageId = string.Empty;
            _webhookMessage = _discordService.CreateWebhookMessage(_config.StatusInfo, _statusData);
            CreateDiscordStatusMessage();
        }
        else
        {
            UpdateDiscordStatusMessage();
        }
        return HookResult.Continue;
    }

    private void RefreshLivePlayerData()
    {
        _statusData.PlayerCount = GetHumanPlayerCount();
        _statusData.MaxPlayers = GetMaxPlayers();
    }

    private int GetMaxPlayers()
    {
        try
        {
            var visible = _core.ConVar.Find<int>("sv_visiblemaxplayers")?.Value ?? -1;
            if (visible > 0) return visible;
        }
        catch { }
        try
        {
            var clients = _core.Engine.GlobalVars.MaxClients;
            if (clients > 0) return clients;
        }
        catch { }
        return _core.PlayerManager.PlayerCap;
    }

    private void RefreshBannerUrl()
    {
        // bucket cache-buster to 1-minute granularity so frequent updates reuse the same URL
        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var id = _config.CS2BrowserServerId?.Trim();
        if (!string.IsNullOrEmpty(id))
        {
            var size = _config.CS2BrowserBannerSize == CS2BrowserBannerSize.Small ? "small" : "big";
            _statusData.BannerUrl = $"https://cs2browser.net/banners/{Uri.EscapeDataString(id)}/{size}.png?t={bucket}";
        }
        else
        {
            _statusData.BannerUrl = null;
        }
        _logger.LogInformation("Banner URL: {url}", _statusData.BannerUrl ?? "<none>");
    }

    private int GetHumanPlayerCount()
    {
        try
        {
            var bots = _core.PlayerManager.GetBots()?.Count() ?? 0;
            return Math.Max(0, _core.PlayerManager.PlayerCount - bots);
        }
        catch
        {
            return _core.PlayerManager.PlayerCount;
        }
    }

    private static string BuildConnectUrl(string? ip, string? cs2BrowserServerId)
    {
        if (string.IsNullOrEmpty(ip)) return string.Empty;
        return !string.IsNullOrWhiteSpace(cs2BrowserServerId)
            ? $"https://cs2browser.net/server/{Uri.EscapeDataString(cs2BrowserServerId.Trim())}"
            : $"https://cs2browser.net/?search={Uri.EscapeDataString(ip)}";
    }

    private static bool IsSnowflake(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length < 17 || id.Length > 20) return false;
        foreach (var c in id) if (c < '0' || c > '9') return false;
        return true;
    }

    private void CreateDiscordStatusMessage()
    {
        Task.Run(async () =>
        {
            try
            {
                var messageId = await _discordService.CreateStatusMessageAsync(_config.StatusInfo, _webhookMessage);

                if (IsSnowflake(messageId))
                {
                    _config.StatusInfo.MessageId = messageId;
                    _core.Scheduler.NextTick(() => _configService.UpdateConfig(_config, _configPath));

                    await _discordService.UpdateStatusMessageAsync(
                        _config.StatusInfo,
                        _discordService.UpdateWebhookMessage(_webhookMessage, _statusData));
                }
                else
                {
                    _logger.LogError("Something went wrong getting a response when sending message.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed creating Discord status message.");
            }
        });
    }

    private void UpdateDiscordStatusMessage()
    {
        Task.Run(async () =>
        {
            try
            {
                _statusData.Timestamp = DateTime.Now;
                RefreshBannerUrl();
                var updated = _discordService.UpdateWebhookMessage(_webhookMessage, _statusData);
                await _discordService.UpdateStatusMessageAsync(_config.StatusInfo, updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed updating Discord status message.");
            }
        });
    }

    private void LoadConfig()
    {
        var dir = _core.Configuration.BasePath;
        Directory.CreateDirectory(dir);
        _configPath = _core.Configuration.GetConfigPath("DiscordStatus.json");

        if (!File.Exists(_configPath))
        {
            _config = new Config();
            _configService.UpdateConfig(_config, _configPath);
            _logger.LogInformation("Generated default config at {Path}", _configPath);
            return;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var parsed = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (parsed is null)
            {
                _logger.LogWarning("Config at {Path} parsed as null — using defaults.", _configPath);
                _config = new Config();
            }
            else
            {
                _config = parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read config at {Path} — using defaults.", _configPath);
            _config = new Config();
        }

        var defaults = new Config();
        if (_config.Version < defaults.Version)
        {
            _logger.LogWarning(
                "The config version does not match current version. Expected: {Expected} | Current: {Current}",
                defaults.Version,
                _config.Version);
        }

        if (string.IsNullOrEmpty(_config.ServerIp))
        {
            _logger.LogWarning("Server IP is missing from config. Set a value to create connection links and display properly.");
        }
    }
}
