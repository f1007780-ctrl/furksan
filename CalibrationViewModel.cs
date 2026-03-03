using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

namespace IProSensorPanel;

public sealed class CalibrationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CalibrationItem> Items { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand ApplyCommand { get; }

    private static string CalibrationPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IProSensorPanel",
        "calibration.json");

    public CalibrationViewModel()
    {
        Items.Add(new CalibrationItem("S1", 0.0, "Örn: termometreye göre farkı yaz"));
        Items.Add(new CalibrationItem("S2", 0.0, ""));
        Items.Add(new CalibrationItem("S3", 0.0, ""));
        Items.Add(new CalibrationItem("S4", 0.0, ""));

        SaveCommand = new RelayCommand(_ => Save());
        LoadCommand = new RelayCommand(_ => Load());
        ApplyCommand = new RelayCommand(_ => { /* UI butonu için */ });

        Load();
    }

    public SensorValues Apply(SensorValues v)
    {
        var o1 = GetOffset("S1");
        var o2 = GetOffset("S2");
        var o3 = GetOffset("S3");
        var o4 = GetOffset("S4");

        return v with
        {
            S1 = v.S1 + o1,
            S2 = v.S2 + o2,
            S3 = v.S3 + o3,
            S4 = v.S4 + o4
        };
    }

    private double GetOffset(string name)
    {
        foreach (var it in Items)
            if (string.Equals(it.SensorName, name, StringComparison.OrdinalIgnoreCase))
                return it.OffsetC;
        return 0.0;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CalibrationPath)!);
        File.WriteAllText(CalibrationPath, JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(CalibrationPath))
                return;

            var loaded = JsonSerializer.Deserialize<CalibrationItem[]>(File.ReadAllText(CalibrationPath));
            if (loaded is null) return;

            Items.Clear();
            foreach (var it in loaded)
                Items.Add(it);
        }
        catch
        {
            // bozuk dosya varsa sessiz geç
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class CalibrationItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string SensorName { get; }

    private double _offsetC;
    public double OffsetC { get => _offsetC; set { _offsetC = value; OnPropertyChanged(); } }

    private string _note;
    public string Note { get => _note; set { _note = value; OnPropertyChanged(); } }

    public CalibrationItem(string sensorName, double offsetC, string note)
    {
        SensorName = sensorName;
        _offsetC = offsetC;
        _note = note;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}