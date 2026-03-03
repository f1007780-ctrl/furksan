using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace IProSensorPanel;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsViewModel Settings { get; }
    public CalibrationViewModel Calibration { get; }
    public DataViewModel Data { get; }

    private readonly SerialService _serial = new();

    private string _status = "Hazır";
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }

    private string _portInfo = "Port: -";
    public string PortInfo { get => _portInfo; private set { _portInfo = value; OnPropertyChanged(); } }

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }

    public MainViewModel()
    {
        Settings = new SettingsViewModel();
        Calibration = new CalibrationViewModel();
        Data = new DataViewModel(Settings);

        ConnectCommand = new RelayCommand(_ => Connect());
        DisconnectCommand = new RelayCommand(_ => Disconnect());

        Settings.Load();

        if (Settings.AutoConnect)
            Connect();
    }

    private void Connect()
    {
        try
        {
            var port = Settings.SelectedPort;
            var baud = Settings.BaudRate;

            if (string.IsNullOrWhiteSpace(port))
            {
                Status = "Port seçili değil.";
                return;
            }

            _serial.OnLine -= OnLineReceived; // çift eklenmesin
            _serial.OnLine += OnLineReceived;

            _serial.Connect(port, baud);

            PortInfo = $"Port: {port} @ {baud}";
            Status = "Bağlandı.";
        }
        catch (Exception ex)
        {
            Status = $"Bağlantı hatası: {ex.Message}";
        }
    }

    private void Disconnect()
    {
        try
        {
            _serial.OnLine -= OnLineReceived;
            _serial.Disconnect();
            Status = "Bağlantı kesildi.";
            PortInfo = "Port: -";
        }
        catch (Exception ex)
        {
            Status = $"Kesme hatası: {ex.Message}";
        }
    }

    // Arduino satırı örnek:
    // S1:24.1;S2:25.0;S3:23.8;S4:26.2
    private void OnLineReceived(string line)
    {
        if (!SensorLineParser.TryParse(line, out var values))
        {
            Data.AddRaw(line);
            Status = "Veri alınıyor (parse edilemedi, loglandı).";
            return;
        }

        var calibrated = Calibration.Apply(values);
        Data.Add(calibrated, line);

        Status = "Veri alınıyor…";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        Disconnect();
        _serial.Dispose();
    }
}