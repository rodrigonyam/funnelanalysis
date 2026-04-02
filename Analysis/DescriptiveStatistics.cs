using MathNet.Numerics.Statistics;

namespace FunnelAnalysis.Analysis;

public record ColumnStats(
    string Column,
    int Count,
    int MissingCount,
    double Min,
    double Q1,
    double Median,
    double Mean,
    double Q3,
    double Max,
    double StdDev,
    double Skewness,
    List<double> Outliers
);

public static class DescriptiveStatistics
{
    public static ColumnStats Compute(string columnName, IEnumerable<double?> rawValues)
    {
        var allValues = rawValues.ToList();
        int total = allValues.Count;
        int missing = allValues.Count(v => v is null);
        var values = allValues.Where(v => v.HasValue).Select(v => v!.Value).ToArray();

        if (values.Length == 0)
        {
            return new ColumnStats(columnName, total, missing, 0, 0, 0, 0, 0, 0, 0, 0, []);
        }

        Array.Sort(values);
        double mean = values.Mean();
        double stdDev = values.Length > 1 ? values.StandardDeviation() : 0;
        double median = values.Median();
        double q1 = values.LowerQuartile();
        double q3 = values.UpperQuartile();
        double min = values.Min();
        double max = values.Max();
        double skewness = values.Length >= 3 ? values.Skewness() : 0;

        // IQR-based outlier detection
        double iqr = q3 - q1;
        double lowerFence = q1 - 1.5 * iqr;
        double upperFence = q3 + 1.5 * iqr;
        var outliers = values.Where(v => v < lowerFence || v > upperFence).ToList();

        return new ColumnStats(columnName, total, missing, min, q1, median, mean, q3, max, stdDev, skewness, outliers);
    }
}
