using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

namespace IProSensorPanel;

public sealed class PanelPageVm
{
    public SensorCardVm S1 { get; } = new("S1");
    public SensorCardVm S2 { get; } = new("S2");
    public SensorCardVm S3 { get; } = new("S3");
    public SensorCardVm S4 { get; } = new("S4");
    public SensorCardVm S5 { get; } = new("S5");
}

public sealed class DataRowVm
{
    public string Timestamp { get; set; } = "";
    public string S1 { get; set; } = "";
    public string S2 { get; set; } = "";
    public string S3 { get; set; } = "";
    public string S4 { get; set; } = "";
    public string S5 { get; set; } = "";
}

public sealed class DataPageVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DataRowVm> Rows { get; } = new();

    private string _footer = "0 satır";
    public string Footer { get => _footer; set { _footer = value; OnPropertyChanged(); } }

    public ICommand ExportCsvCommand { get; }
    public ICommand ClearCommand { get; }

    private static string LogDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IProSensorPanel", "logs");

    public DataPageVm()
    {
        ExportCsvCommand = new RelayCommand(_ => ExportCsv());
        ClearCommand = new RelayCommand(_ => { Rows.Clear(); Footer = "0 satır"; });
    }

    public void Add(string s1, string s2, string s3, string s4, string s5)
    {
        Rows.Insert(0, new DataRowVm
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            S1 = s1,
            S2 = s2,
            S3 = s3,
            S4 = s4,
            S5 = s5
        });

        if (Rows.Count > 200) Rows.RemoveAt(Rows.Count - 1);
        Footer = $"{Rows.Count} satır (son 200)";
    }

    private void ExportCsv()
    {
        Directory.CreateDirectory(LogDir);
        var path = Path.Combine(LogDir, $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,S1,S2,S3,S4,S5");
        foreach (var r in Rows)
            sb.AppendLine($"{r.Timestamp},{r.S1},{r.S2},{r.S3},{r.S4},{r.S5}");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Footer = $"Export: {path}";
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class CalibrationItemVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; }

    private double _offset;
    public double Offset { get => _offset; set { _offset = value; OnPropertyChanged(); } }

    public CalibrationItemVm(string name, double offset = 0) { Name = name; _offset = offset; }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class CalibrationPageVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CalibrationItemVm> Items { get; } = new()
    {
        new("S1"), new("S2"), new("S3"), new("S4"), new("S5")
    };

    private string _infoText = "";
    public string InfoText { get => _infoText; set { _infoText = value; OnPropertyChanged(); } }

    public ICommand SaveCommand { get; }
    public ICommand LoadCommand { get; }

    private static string PathFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IProSensorPanel", "calibration.json");

    public CalibrationPageVm()
    {
        SaveCommand = new RelayCommand(_ => Save());
        LoadCommand = new RelayCommand(_ => Load());
        Load();
    }

    public double GetOffset(string name)
    {
        foreach (var it in Items)
            if (string.Equals(it.Name, name, StringComparison.OrdinalIgnoreCase))
                return it.Offset;
        return 0;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathFile)!);
        var dto = Items.Select(i => new CalDto(i.Name, i.Offset)).ToArray();
        File.WriteAllText(PathFile, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        InfoText = "Kaydedildi.";
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(PathFile)) { InfoText = "Kalibrasyon dosyası yok."; return; }
            var arr = JsonSerializer.Deserialize<CalDto[]>(File.ReadAllText(PathFile));
            if (arr is null) return;

            foreach (var dto in arr)
                foreach (var it in Items)
                    if (string.Equals(it.Name, dto.Name, StringComparison.OrdinalIgnoreCase))
                        it.Offset = dto.Offset;

            InfoText = "Yüklendi.";
        }
        catch (Exception ex)
        {
            InfoText = $"Yükleme hatası: {ex.Message}";
        }
    }

    private record CalDto(string Name, double Offset);

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}