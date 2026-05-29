using DiscordStatus.Models;
using DiscordStatus.Services.Interfaces;
using DiscordStatus.Utils;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DiscordStatus.Services
{
    public class ConfigService : IConfigService
    {
        private readonly ILogger<ConfigService> _logger;

        public ConfigService(ILogger<ConfigService> logger)
        {
            _logger = logger;
        }

        public void UpdateConfig(Config configData, string configPath)
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var serializedConfigData = JsonSerializer.Serialize(configData, jsonOptions);

                var dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(configPath, serializedConfigData);

                Util.PrintLog("Updated config.json file");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating the config: {message}", ex.Message);
            }
        }
    }
}
