using FunnelAnalysis.Analysis;
using FunnelAnalysis.IO;
using FunnelAnalysis.Models;
using FunnelAnalysis.Visualization;
using Spectre.Console;

// ── Banner ──────────────────────────────────────────────────────────────────
AnsiConsole.Write(new FigletText("Funnel EDA").Color(Color.SteelBlue1));
AnsiConsole.MarkupLine("[bold]Exploratory Data Analysis | E-Commerce Funnel Dataset[/]\n");

// ── Load data ────────────────────────────────────────────────────────────────
string csvPath = Path.Combine(AppContext.BaseDirectory, "Data", "sample_ecommerce.csv");
if (!File.Exists(csvPath))
{
    AnsiConsole.MarkupLine($"[red]CSV not found:[/] {csvPath}");
    return 1;
}

AnsiConsole.MarkupLine($"[grey]Loading data from:[/] {csvPath}");
List<DataRecord> records = CsvDataLoader.Load(csvPath);
AnsiConsole.MarkupLine($"[green]✓[/] Loaded [bold]{records.Count}[/] rows.\n");

// ── Numeric column extraction ────────────────────────────────────────────────
var numericColumnDefs = new Dictionary<string, Func<DataRecord, double?>>
{
    ["age"]                  = r => r.Age,
    ["session_duration_sec"] = r => r.SessionDurationSec,
    ["pages_viewed"]         = r => r.PagesViewed,
    ["added_to_cart"]        = r => r.AddedToCart,
    ["purchased"]            = r => r.Purchased,
    ["order_value"]          = r => r.OrderValue,
};

// ── 1. Descriptive Statistics ─────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[bold yellow]1. Descriptive Statistics[/]"));

var statsTable = new Table().Border(TableBorder.Rounded).Expand();
statsTable.AddColumns(
    "[bold]Column[/]", "[bold]Count[/]", "[bold]Missing[/]", "[bold]Min[/]",
    "[bold]Q1[/]", "[bold]Median[/]", "[bold]Mean[/]", "[bold]Q3[/]",
    "[bold]Max[/]", "[bold]StdDev[/]", "[bold]Skew[/]", "[bold]Outliers[/]");

var allStats = new List<ColumnStats>();
foreach (var (col, selector) in numericColumnDefs)
{
    var stats = DescriptiveStatistics.Compute(col, records.Select(selector));
    allStats.Add(stats);
    statsTable.AddRow(
        col,
        stats.Count.ToString(),
        stats.MissingCount == 0 ? "0" : $"[red]{stats.MissingCount}[/]",
        $"{stats.Min:F2}",
        $"{stats.Q1:F2}",
        $"{stats.Median:F2}",
        $"{stats.Mean:F2}",
        $"{stats.Q3:F2}",
        $"{stats.Max:F2}",
        $"{stats.StdDev:F2}",
        $"{stats.Skewness:F2}",
        stats.Outliers.Count == 0 ? "0" : $"[orange1]{stats.Outliers.Count}[/]");
}
AnsiConsole.Write(statsTable);

// ── 2. Missing Values ─────────────────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold yellow]2. Missing Values[/]"));
var missingCols = allStats.Where(s => s.MissingCount > 0).ToList();
if (missingCols.Count == 0)
{
    AnsiConsole.MarkupLine("[green]No missing values found.[/]");
}
else
{
    var missingData = missingCols.ToDictionary(
        s => s.Column,
        s => (double)s.MissingCount);
    ChartRenderer.RenderBarChart("Missing Values per Column", missingData, "Missing Count");

    foreach (var s in missingCols)
        AnsiConsole.MarkupLine($"  [cyan]{s.Column,-25}[/] {s.MissingCount} missing ({(double)s.MissingCount / s.Count * 100:F1}%)");
}

// ── 3. Outlier Detection ──────────────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold yellow]3. Outlier Detection (IQR Method)[/]"));
var outlierTable = new Table().Border(TableBorder.Rounded);
outlierTable.AddColumns("[bold]Column[/]", "[bold]Outlier Count[/]", "[bold]Outlier Values[/]");
foreach (var s in allStats)
{
    if (s.Outliers.Count > 0)
    {
        string vals = string.Join(", ", s.Outliers.Take(5).Select(v => v.ToString("F1")));
        if (s.Outliers.Count > 5) vals += " ...";
        outlierTable.AddRow(s.Column, $"[orange1]{s.Outliers.Count}[/]", vals);
    }
}
if (outlierTable.Rows.Count == 0)
    AnsiConsole.MarkupLine("[green]No outliers detected.[/]");
else
    AnsiConsole.Write(outlierTable);

// ── 4. Correlation Analysis ───────────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold yellow]4. Correlation Analysis (Pearson r)[/]"));

// Build aligned double[] arrays (drop rows with any null across all columns)
var completeRecords = records
    .Where(r => numericColumnDefs.Values.All(sel => sel(r).HasValue))
    .ToList();

var numericArrays = numericColumnDefs.ToDictionary(
    kv => kv.Key,
    kv => completeRecords.Select(r => kv.Value(r)!.Value).ToArray());

var correlations = CorrelationAnalysis.Compute(numericArrays);
var corrTable = new Table().Border(TableBorder.Rounded);
corrTable.AddColumns("[bold]Column A[/]", "[bold]Column B[/]", "[bold]Pearson r[/]", "[bold]Strength[/]");
foreach (var c in correlations.Take(15))
{
    double abs = Math.Abs(c.PearsonR);
    string strength = abs switch
    {
        >= 0.7 => "[green]Strong[/]",
        >= 0.4 => "[yellow]Moderate[/]",
        >= 0.2 => "[grey]Weak[/]",
        _      => "[dim]Negligible[/]"
    };
    string rFormatted = c.PearsonR >= 0
        ? $"[green] {c.PearsonR:F4}[/]"
        : $"[red]{c.PearsonR:F4}[/]";
    corrTable.AddRow(c.ColumnA, c.ColumnB, rFormatted, strength);
}
AnsiConsole.Write(corrTable);

// ── 5. Distributions ──────────────────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold yellow]5. Distributions[/]"));
ChartRenderer.RenderHistogram("Age", records.Where(r => r.Age.HasValue).Select(r => r.Age!.Value));
ChartRenderer.RenderHistogram("Session Duration (sec)", records.Select(r => r.SessionDurationSec));
ChartRenderer.RenderHistogram("Order Value", records.Where(r => r.OrderValue.HasValue).Select(r => r.OrderValue!.Value));

// ── 6. Device & Region Breakdown ─────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold yellow]6. Categorical Breakdown[/]"));

var deviceCounts = records
    .GroupBy(r => r.Device)
    .ToDictionary(g => g.Key, g => (double)g.Count());
ChartRenderer.RenderBarChart("Users by Device", deviceCounts, "Count");

var regionCounts = records
    .GroupBy(r => r.Region)
    .ToDictionary(g => g.Key, g => (double)g.Count());
ChartRenderer.RenderBarChart("Users by Region", regionCounts, "Count");

// ── 7. Funnel Analysis ────────────────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold yellow]7. Conversion Funnel[/]"));
var funnel = new Dictionary<string, int>
{
    ["Visited"]         = records.Count,
    ["Viewed 3+ pages"] = records.Count(r => r.PagesViewed >= 3),
    ["Added to Cart"]   = records.Count(r => r.AddedToCart == 1),
    ["Purchased"]       = records.Count(r => r.Purchased == 1),
};
ChartRenderer.RenderFunnelChart("E-Commerce Conversion Funnel", funnel);

// ── 8. Average Order Value by Region ─────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold yellow]8. Average Order Value by Region[/]"));
var aovByRegion = records
    .Where(r => r.OrderValue.HasValue)
    .GroupBy(r => r.Region)
    .ToDictionary(g => g.Key, g => g.Average(r => r.OrderValue!.Value));
ChartRenderer.RenderBarChart("Average Order Value by Region ($)", aovByRegion, "AOV ($)");

// ── 9. Export Report ──────────────────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold yellow]9. Export JSON Report[/]"));
var report = new EDAReport
{
    RowCount = records.Count,
    ColumnStats = allStats.Select(ReportExporter.ToDto).ToList(),
    Correlations = correlations.Select(c => new CorrelationDto
    {
        ColumnA = c.ColumnA, ColumnB = c.ColumnB, PearsonR = c.PearsonR
    }).ToList(),
    Funnel = funnel
};

string reportPath = Path.Combine(AppContext.BaseDirectory, "eda_report.json");
ReportExporter.Export(report, reportPath);
AnsiConsole.MarkupLine($"[green]✓[/] Report saved to: [link]{reportPath}[/]");

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[bold green]EDA complete![/]");
return 0;

