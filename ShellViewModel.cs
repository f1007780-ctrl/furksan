using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace IProSensorPanel;

public sealed class ShellViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsViewModel Settings { get; }
    public CalibrationViewModel Calibration { get; }
    public DataViewModel Data { get; }
    public DashboardViewModel Dashboard { get; }

    private readonly SerialService _serial = new();

    private string _status = "Hazır";
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }

    private string _portInfo = "Port: -";
    public string PortInfo { get => _portInfo; private set { _portInfo = value; OnPropertyChanged(); } }

    private object _currentView;
    public object CurrentView { get => _currentView; private set { _currentView = value; OnPropertyChanged(); } }

    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public ICommand ShowCalibrationCommand { get; }
    public ICommand ShowDataCommand { get; }

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand CloseCommand { get; }

    public ShellViewModel()
    {
        Settings = new SettingsViewModel();
        Calibration = new CalibrationViewModel();
        Data = new DataViewModel(Settings);
        Dashboard = new DashboardViewModel();

        _currentView = Dashboard;

        ShowDashboardCommand = new RelayCommand(_ => CurrentView = Dashboard);
        ShowSettingsCommand = new RelayCommand(_ => CurrentView = Settings);
        ShowCalibrationCommand = new RelayCommand(_ => CurrentView = Calibration);
        ShowDataCommand = new RelayCommand(_ => CurrentView = Data);

        ConnectCommand = new RelayCommand(_ => Connect());
        DisconnectCommand = new RelayCommand(_ => Disconnect());
        CloseCommand = new RelayCommand(_ => Application.Current.Shutdown());

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

            _serial.OnLine -= OnLineReceived;
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
            Status = "Kesildi.";
            PortInfo = "Port: -";
        }
        catch (Exception ex)
        {
            Status = $"Kesme hatası: {ex.Message}";
        }
    }

    private void OnLineReceived(string line)
    {
        if (!SensorLineParser.TryParse(line, out var values))
        {
            Data.AddRaw(line);
            Status = "Veri geliyor (parse yok, loglandı).";
            return;
        }

        var calibrated = Calibration.Apply(values);

        Dashboard.Update(calibrated);
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