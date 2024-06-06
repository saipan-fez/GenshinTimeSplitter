using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;
using OpenCvSharp;
using System;

namespace GenshinTimeSplitter.Proc;

public record struct AnalyzeResult(
    [JsonProperty] TimeSpan AnalyzeStartTimeSpan,
    [JsonProperty] TimeSpan AnalyzeEndTimeSpan,
    [JsonProperty] SectionInfo[] Sections);

// currently MapOpenedTimeSpan is not supported.
// so not output to csv file.
public readonly record struct SectionInfo()
{
    [JsonProperty]
    [Index(0)]
    [Name("section_start")]
    public TimeSpan SectionStartedTimeSpan { get; init; }

    [JsonProperty]
    [Ignore]
    [Name("map_open")]
    public TimeSpan? MapOpenedTimeSpan { get; init; }

    [JsonProperty]
    [Index(1)]
    [Name("load_start")]
    public TimeSpan? LoadScreenStartedTimeSpan { get; init; }

    [JsonProperty]
    [Ignore]
    [Name("seconds_from_section_start_to_map_open")]
    public readonly double SecondsFromSectionStartToMapOpened =>
        MapOpenedTimeSpan.HasValue ?
            (MapOpenedTimeSpan.Value- SectionStartedTimeSpan).TotalSeconds :
            0d;

    [JsonProperty]
    [Index(2)]
    [Name("seconds_from_section_start_to_load_start")]
    public readonly double SecondsFromSectionStartToLoadScreenStarted =>
        LoadScreenStartedTimeSpan.HasValue ?
            (LoadScreenStartedTimeSpan.Value - SectionStartedTimeSpan).TotalSeconds :
            0d;
}

[JsonObject]
public record struct AnalyzeConfig(
    [JsonProperty] Size TargetMovieResolution = new(),
    [JsonProperty] byte DiffThreashold = 3,
    [JsonProperty] Rect[] AnalyzeRegions = null,
    [JsonProperty] byte ParallelCount = 0)
{
    public static AnalyzeConfig GetDefault(Size s)
    {
        const int RegionWidth  = 150;
        const int RegionHeight = 150;

        var left_LeftRegion  = s.Width / 4 * 1 - RegionWidth / 2;
        var left_RightRegion = s.Width / 4 * 3 - RegionWidth / 2;
        var top_TopRegion    = s.Height / 4 * 1 - RegionHeight / 2;
        var top_BottomRegion = top_TopRegion + RegionHeight + 100;

        var regions = new Rect[]
        {
            new Rect(left_LeftRegion, top_TopRegion, RegionWidth, RegionHeight), // Left Top
            new Rect(left_RightRegion, top_TopRegion, RegionWidth, RegionHeight), // Right Top
            new Rect(left_LeftRegion, top_BottomRegion, RegionWidth, RegionHeight), // Left  Bottom
            new Rect(left_RightRegion, top_BottomRegion, RegionWidth, RegionHeight), // Right Bottom
        };

        return new AnalyzeConfig(
            TargetMovieResolution: s,
            AnalyzeRegions: regions);
    }
}
