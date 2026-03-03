using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

namespace IProSensorPanel;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> AvailablePorts { get; } = new();

    private string? _selectedPort;
    public string? SelectedPort { get => _selectedPort; set { _selectedPort = value; OnPropertyChanged(); } }

    private int _baudRate = 9600;
    public int BaudRate { get => _baudRate; set { _baudRate = value; OnPropertyChanged(); } }

    private bool _autoConnect = true;
    public bool AutoConnect { get => _autoConnect; set { _autoConnect = value; OnPropertyChanged(); } }

    private string _logFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IProSensorPanel",
        "logs");

    public string LogFolder { get => _logFolder; set { _logFolder = value; OnPropertyChanged(); } }

    private string _infoText = "";
    public string InfoText { get => _infoText; private set { _infoText = value; OnPropertyChanged(); } }

    public ICommand RefreshPortsCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand SetDefaultLogFolderCommand { get; }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IProSensorPanel",
        "settings.json");

    public SettingsViewModel()
    {
        RefreshPortsCommand = new RelayCommand(_ => RefreshPorts());
        SaveCommand = new RelayCommand(_ => Save());
        LoadCommand = new RelayCommand(_ => Load());
        SetDefaultLogFolderCommand = new RelayCommand(_ => SetDefaultLogFolder());

        RefreshPorts();
        InfoText = $"Ayar dosyası: {SettingsPath}";
    }

    public void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var p in SerialPort.GetPortNames())
            AvailablePorts.Add(p);

        if (SelectedPort is null && AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }

    public void SetDefaultLogFolder()
    {
        LogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IProSensorPanel",
            "logs");
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

        var dto = new SettingsDto
        {
            SelectedPort = SelectedPort,
            BaudRate = BaudRate,
            AutoConnect = AutoConnect,
            LogFolder = LogFolder
        };

        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        InfoText = $"Kaydedildi: {SettingsPath}";
    }

    public void Load()
    {
        try
        {
            RefreshPorts();

            if (!File.Exists(SettingsPath))
            {
                InfoText = "Ayar dosyası yok, varsayılanlar kullanılıyor.";
                return;
            }

            var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(SettingsPath));
            if (dto is null) return;

            BaudRate = dto.BaudRate <= 0 ? 9600 : dto.BaudRate;
            AutoConnect = dto.AutoConnect;
            LogFolder = string.IsNullOrWhiteSpace(dto.LogFolder) ? LogFolder : dto.LogFolder;

            if (!string.IsNullOrWhiteSpace(dto.SelectedPort))
                SelectedPort = dto.SelectedPort;

            InfoText = $"Yüklendi: {SettingsPath}";
        }
        catch (Exception ex)
        {
            InfoText = $"Yükleme hatası: {ex.Message}";
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private sealed class SettingsDto
    {
        public string? SelectedPort { get; set; }
        public int BaudRate { get; set; } = 9600;
        public bool AutoConnect { get; set; } = true;
        public string? LogFolder { get; set; }
    }
}