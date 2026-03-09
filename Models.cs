namespace IProSensorPanel;

public readonly record struct SensorValues(double S1, double S2, double S3, double S4, double S5);

public sealed class DataRow
{
    public string Timestamp { get; set; } = "";
    public string S1 { get; set; } = "";
    public string S2 { get; set; } = "";
    public string S3 { get; set; } = "";
    public string S4 { get; set; } = "";
    public string S5 { get; set; } = "";
    public string Raw { get; set; } = "";
}
