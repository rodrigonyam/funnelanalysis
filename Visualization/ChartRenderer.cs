using Spectre.Console;

namespace FunnelAnalysis.Visualization;

public static class ChartRenderer
{
    public static void RenderBarChart(string title, Dictionary<string, double> data, string valueLabel = "Value")
    {
        AnsiConsole.WriteLine();
        var chart = new BarChart()
            .Width(70)
            .Label($"[bold yellow]{title}[/]")
            .CenterLabel();

        double max = data.Values.Max();
        int colorIndex = 0;
        Color[] palette = [Color.SteelBlue1, Color.Green3, Color.Orange1, Color.Red3, Color.Violet];

        foreach (var (label, value) in data)
        {
            chart.AddItem(label, Math.Round(value, 2), palette[colorIndex % palette.Length]);
            colorIndex++;
        }

        AnsiConsole.Write(chart);
    }

    public static void RenderHistogram(string title, IEnumerable<double> values, int bins = 10)
    {
        var arr = values.ToArray();
        if (arr.Length == 0) return;

        double min = arr.Min();
        double max = arr.Max();
        double binWidth = (max - min) / bins;
        if (binWidth == 0) binWidth = 1;

        var counts = new int[bins];
        foreach (var v in arr)
        {
            int idx = (int)((v - min) / binWidth);
            if (idx >= bins) idx = bins - 1;
            counts[idx]++;
        }

        var data = new Dictionary<string, double>();
        for (int i = 0; i < bins; i++)
        {
            double lower = min + i * binWidth;
            double upper = lower + binWidth;
            data[$"{lower:F1}-{upper:F1}"] = counts[i];
        }

        RenderBarChart($"{title} – Histogram", data, "Count");
    }

    public static void RenderFunnelChart(string title, Dictionary<string, int> steps)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold yellow]{title}[/]");
        AnsiConsole.WriteLine(new string('─', 60));

        int maxWidth = 50;
        int firstValue = steps.Values.First();

        foreach (var (step, count) in steps)
        {
            double ratio = firstValue > 0 ? (double)count / firstValue : 0;
            int barLen = (int)(ratio * maxWidth);
            string bar = new string('█', barLen);
            string pct = $"{ratio * 100:F1}%";
            AnsiConsole.MarkupLine(
                $"[cyan]{step,-20}[/] [green]{bar,-52}[/] [white]{count,5}[/] [grey]({pct})[/]");
        }

        AnsiConsole.WriteLine(new string('─', 60));
    }
}
