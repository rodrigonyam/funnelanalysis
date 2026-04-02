using System.Text.Json;
using System.Text.Json.Serialization;
using FunnelAnalysis.Analysis;

namespace FunnelAnalysis.IO;

public class EDAReport
{
    [JsonPropertyName("generated_at")]
    public string GeneratedAt { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }

    [JsonPropertyName("column_stats")]
    public List<ColumnStatsDto> ColumnStats { get; set; } = [];

    [JsonPropertyName("correlations")]
    public List<CorrelationDto> Correlations { get; set; } = [];

    [JsonPropertyName("funnel")]
    public Dictionary<string, int> Funnel { get; set; } = [];
}

public class ColumnStatsDto
{
    [JsonPropertyName("column")] public string Column { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("missing")] public int Missing { get; set; }
    [JsonPropertyName("missing_pct")] public double MissingPct { get; set; }
    [JsonPropertyName("min")] public double Min { get; set; }
    [JsonPropertyName("q1")] public double Q1 { get; set; }
    [JsonPropertyName("median")] public double Median { get; set; }
    [JsonPropertyName("mean")] public double Mean { get; set; }
    [JsonPropertyName("q3")] public double Q3 { get; set; }
    [JsonPropertyName("max")] public double Max { get; set; }
    [JsonPropertyName("std_dev")] public double StdDev { get; set; }
    [JsonPropertyName("skewness")] public double Skewness { get; set; }
    [JsonPropertyName("outlier_count")] public int OutlierCount { get; set; }
}

public class CorrelationDto
{
    [JsonPropertyName("column_a")] public string ColumnA { get; set; } = "";
    [JsonPropertyName("column_b")] public string ColumnB { get; set; } = "";
    [JsonPropertyName("pearson_r")] public double PearsonR { get; set; }
}

public static class ReportExporter
{
    public static void Export(EDAReport report, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        string json = JsonSerializer.Serialize(report, options);
        File.WriteAllText(outputPath, json);
    }

    public static ColumnStatsDto ToDto(ColumnStats stats) => new()
    {
        Column = stats.Column,
        Count = stats.Count,
        Missing = stats.MissingCount,
        MissingPct = stats.Count > 0 ? Math.Round((double)stats.MissingCount / stats.Count * 100, 2) : 0,
        Min = Math.Round(stats.Min, 4),
        Q1 = Math.Round(stats.Q1, 4),
        Median = Math.Round(stats.Median, 4),
        Mean = Math.Round(stats.Mean, 4),
        Q3 = Math.Round(stats.Q3, 4),
        Max = Math.Round(stats.Max, 4),
        StdDev = Math.Round(stats.StdDev, 4),
        Skewness = Math.Round(stats.Skewness, 4),
        OutlierCount = stats.Outliers.Count
    };
}
