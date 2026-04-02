using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FunnelAnalysis.Models;

namespace FunnelAnalysis.IO;

public static class CsvDataLoader
{
    public static List<DataRecord> Load(string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            HeaderValidated = null,
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        return csv.GetRecords<DataRecord>().ToList();
    }
}
