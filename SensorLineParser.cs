using System.Globalization;

namespace IProSensorPanel;

public static class SensorLineParser
{
    public static bool TryParse(string line, out SensorValues values)
    {
        values = default;
        if (string.IsNullOrWhiteSpace(line)) return false;

        double? s1 = null, s2 = null, s3 = null, s4 = null, s5 = null;

        var parts = line.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            var kv = p.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (kv.Length != 2) continue;

            var key = kv[0].ToUpperInvariant();
            var raw = kv[1].Replace(',', '.');

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                continue;

            if (key == "S1") s1 = val;
            else if (key == "S2") s2 = val;
            else if (key == "S3") s3 = val;
            else if (key == "S4") s4 = val;
            else if (key == "S5") s5 = val;
        }

        if (s1 is null || s2 is null || s3 is null || s4 is null || s5 is null)
            return false;

        values = new SensorValues(s1.Value, s2.Value, s3.Value, s4.Value, s5.Value);
        return true;
    }
}