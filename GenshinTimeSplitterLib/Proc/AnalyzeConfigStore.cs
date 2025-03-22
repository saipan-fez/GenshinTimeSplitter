using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GenshinTimeSplitter.Proc;

public class AnalyzeConfigStore
{
    private const string ConfigFilePath = "./config.json";

    private readonly ILogger<AnalyzeConfigStore> _logger;

    public AnalyzeConfigStore(
        ILogger<AnalyzeConfigStore> logger)
    {
        _logger = logger;
    }

    public bool TryLoad(Size movieResolution, out AnalyzeConfig result)
    {
        return TryLoad(movieResolution, ConfigFilePath, out result);
    }

    public bool TryLoad(Size movieResolution, string filePath, out AnalyzeConfig result)
    {
        result = new();

        if (!File.Exists(filePath))
        {
            _logger.LogDebug("config file not found.");
            return false;
        }

        try
        {
            var fileContent = File.ReadAllText(filePath);
            var config = JsonConvert.DeserializeObject<AnalyzeConfig>(fileContent);
            _logger.LogInformation("config file loaded.");

            if (config.TargetMovieResolution == movieResolution)
            {
                result = config;
                return true;
            }
            else
            {
                _logger.LogError("Resolution is different. config:{config} movie:{movie}", config.TargetMovieResolution, movieResolution);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to load config file.");
            return false;
        }
    }

    public async Task SaveAsync(AnalyzeConfig analyzeConfig)
    {
        var jsonStr = JsonConvert.SerializeObject(analyzeConfig, Formatting.Indented);
        using var sw = new StreamWriter(ConfigFilePath);
        await sw.WriteLineAsync(jsonStr);

        _logger.LogInformation("config file saved.");
    }
}
