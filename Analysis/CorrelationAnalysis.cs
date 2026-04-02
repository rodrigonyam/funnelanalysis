using MathNet.Numerics.Statistics;

namespace FunnelAnalysis.Analysis;

public record CorrelationPair(string ColumnA, string ColumnB, double PearsonR);

public static class CorrelationAnalysis
{
    public static List<CorrelationPair> Compute(Dictionary<string, double[]> numericColumns)
    {
        var keys = numericColumns.Keys.ToList();
        var pairs = new List<CorrelationPair>();

        for (int i = 0; i < keys.Count; i++)
        {
            for (int j = i + 1; j < keys.Count; j++)
            {
                var a = numericColumns[keys[i]];
                var b = numericColumns[keys[j]];

                // Only correlate rows where both values are present (both arrays same length, already filtered)
                if (a.Length < 2 || b.Length < 2) continue;

                double r = Correlation.Pearson(a, b);
                pairs.Add(new CorrelationPair(keys[i], keys[j], Math.Round(r, 4)));
            }
        }

        return [.. pairs.OrderByDescending(p => Math.Abs(p.PearsonR))];
    }
}
