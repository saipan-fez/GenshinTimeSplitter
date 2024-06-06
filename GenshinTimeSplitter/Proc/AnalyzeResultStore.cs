using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GenshinTimeSplitter.Proc;

public class AnalyzeResultStore
{
    private readonly ILogger<AnalyzeResultStore> _logger;

    public AnalyzeResultStore(
        ILogger<AnalyzeResultStore> logger)
    {
        _logger = logger;
    }

    public bool Exists(string movieFile)
    {
        var xsp = GetXspPath(movieFile);
        var json = GetJsonPath(movieFile);
        var csv = GetCsvPath(movieFile);

        _logger.LogDebug("xsp file path:{xsp}", xsp);
        _logger.LogDebug("json file path:{json}", json);
        _logger.LogDebug("csv file path:{csv}", csv);

        return File.Exists(xsp) || File.Exists(json) || File.Exists(csv);
    }

    public async Task SaveAsync(string movieFile, AnalyzeResult analyzeResult)
    {
        await Task.WhenAll(new[]
        {
            SaveXspfPlaylistAsync(movieFile, analyzeResult),
            SaveCsvAsync(movieFile, analyzeResult),
            SaveJsonAsync(movieFile, analyzeResult),
        });
    }

    private async Task SaveCsvAsync(string movieFile, AnalyzeResult analyzeResult)
    {
        var csv = GetCsvPath(movieFile);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        };
        using var sw = new StreamWriter(csv);
        using var cw = new CsvWriter(sw, config);
        await cw.WriteRecordsAsync(analyzeResult.Sections);

        _logger.LogDebug("csv saved. path:{csv}", csv);
    }

    private async Task SaveJsonAsync(string movieFile, AnalyzeResult analyzeResult)
    {
        var json = GetJsonPath(movieFile);

        var jsonStr = JsonConvert.SerializeObject(analyzeResult, Formatting.Indented);

        using var sw = new StreamWriter(json);
        await sw.WriteLineAsync(jsonStr);

        _logger.LogDebug("json saved. path:{json}", json);
    }

    private async Task SaveXspfPlaylistAsync(string movieFile, AnalyzeResult analyzeResult)
    {
        var movieFileUri = new Uri(movieFile);
        var tracks = analyzeResult.Sections
            .Select(x => x.SectionStartedTimeSpan)
            .Select((t, i) =>
                $"""
                <track>
                  <title>{t:hh\:mm\:ss}</title>
                  <location>{movieFileUri.AbsoluteUri}</location>
                  <extension application="http://www.videolan.org/vlc/playlist/0">
                    <vlc:id>{i}</vlc:id>
                    <vlc:option>start-time={(int)t.TotalSeconds}</vlc:option>
                  </extension>
                </track>
                """);

        var xml =
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <playlist xmlns="http://xspf.org/ns/0/" xmlns:vlc="http://www.videolan.org/vlc/playlist/ns/0/" version="1">
              <title>Example Playlist</title>
              <trackList>
                {string.Join("", tracks.Select(x => "    " + x))}
              </trackList>
            </playlist>
            """;

        var xsp = GetXspPath(movieFile);
        using var sw = new StreamWriter(xsp);
        await sw.WriteLineAsync(xml);

        _logger.LogDebug("xsp saved. path:{xsp}", xsp);
    }

    private static string GetXspPath(string movieFilePath)
    {
        var movieDir      = Path.GetDirectoryName(movieFilePath);
        var movieFileName = Path.GetFileNameWithoutExtension(movieFilePath);
        var xspfFilePath  = Path.Combine(movieDir, movieFileName) + ".xspf";
        return xspfFilePath;
    }

    private static string GetJsonPath(string movieFilePath)
    {
        var movieDir      = Path.GetDirectoryName(movieFilePath);
        var movieFileName = Path.GetFileNameWithoutExtension(movieFilePath);
        var xspfFilePath  = Path.Combine(movieDir, movieFileName) + ".json";
        return xspfFilePath;
    }

    private static string GetCsvPath(string movieFilePath)
    {
        var movieDir      = Path.GetDirectoryName(movieFilePath);
        var movieFileName = Path.GetFileNameWithoutExtension(movieFilePath);
        var xspfFilePath  = Path.Combine(movieDir, movieFileName) + ".csv";
        return xspfFilePath;
    }
}
