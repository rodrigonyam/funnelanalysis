using CsvHelper.Configuration.Attributes;

namespace FunnelAnalysis.Models;

public class DataRecord
{
    [Name("user_id")]
    public int UserId { get; set; }

    [Name("age")]
    public double? Age { get; set; }

    [Name("session_duration_sec")]
    public double SessionDurationSec { get; set; }

    [Name("pages_viewed")]
    public double PagesViewed { get; set; }

    [Name("added_to_cart")]
    public int AddedToCart { get; set; }

    [Name("purchased")]
    public int Purchased { get; set; }

    [Name("order_value")]
    public double? OrderValue { get; set; }

    [Name("region")]
    public string Region { get; set; } = string.Empty;

    [Name("device")]
    public string Device { get; set; } = string.Empty;
}
