using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace IProSensorPanel;

public sealed class DataViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DataRow> Rows { get; } = new();

    private string _footer = "0 satır";
    public string Footer { get => _footer; private set { _footer = value; OnPropertyChanged(); } }

    public ICommand ExportCsvCommand { get; }
    public ICommand ClearCommand { get; }

    private readonly SettingsViewModel _settings;

    public DataViewModel(SettingsViewModel settings)
    {
        _settings = settings;
        ExportCsvCommand = new RelayCommand(_ => ExportCsv());
        ClearCommand = new RelayCommand(_ => Clear());
        UpdateFooter();
    }

    public void Add(SensorValues values, string raw)
    {
        var row = new DataRow
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            S1 = values.S1.ToString("0.0", CultureInfo.InvariantCulture),
            S2 = values.S2.ToString("0.0", CultureInfo.InvariantCulture),
            S3 = values.S3.ToString("0.0", CultureInfo.InvariantCulture),
            S4 = values.S4.ToString("0.0", CultureInfo.InvariantCulture),
            Raw = raw
        };

        Rows.Insert(0, row);
        if (Rows.Count > 500) Rows.RemoveAt(Rows.Count - 1);

        AppendLogLine(row);
        UpdateFooter();
    }

    public void AddRaw(string raw)
    {
        var row = new DataRow
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            S1 = "-",
            S2 = "-",
            S3 = "-",
            S4 = "-",
            Raw = raw
        };

        Rows.Insert(0, row);
        if (Rows.Count > 500) Rows.RemoveAt(Rows.Count - 1);

        AppendLogLine(row);
        UpdateFooter();
    }

    private void AppendLogLine(DataRow row)
    {
        try
        {
            Directory.CreateDirectory(_settings.LogFolder);
            var path = Path.Combine(_settings.LogFolder, $"ipro_log_{DateTime.Now:yyyyMMdd}.csv");

            var isNew = !File.Exists(path);
            using var sw = new StreamWriter(path, append: true, Encoding.UTF8);

            if (isNew)
                sw.WriteLine("Timestamp,S1,S2,S3,S4,Raw");

            sw.WriteLine($"{Esc(row.Timestamp)},{Esc(row.S1)},{Esc(row.S2)},{Esc(row.S3)},{Esc(row.S4)},{Esc(row.Raw)}");
        }
        catch
        {
            // log patlarsa UI çalışmaya devam etsin
        }

        static string Esc(string? s)
        {
            s ??= "";
            if (s.Contains(",") || s.Contains("\""))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }

    public void ExportCsv()
    {
        Directory.CreateDirectory(_settings.LogFolder);
        var path = Path.Combine(_settings.LogFolder, $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.WriteLine("Timestamp,S1,S2,S3,S4,Raw");
        foreach (var r in Rows)
            sw.WriteLine($"{r.Timestamp},{r.S1},{r.S2},{r.S3},{r.S4},{(r.Raw ?? "").Replace("\n", " ").Replace("\r", " ")}");

        Footer = $"Dışa aktarıldı: {path}";
    }

    public void Clear()
    {
        Rows.Clear();
        UpdateFooter();
    }

    private void UpdateFooter() => Footer = $"{Rows.Count} satır (son 500 tutulur)";

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}