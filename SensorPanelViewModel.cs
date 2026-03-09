using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;

namespace IProSensorPanel;

public sealed class SensorPanelViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _status = "Hazır";
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); Settings.SyncFromRoot(); } }

    private string _portInfo = "Port: -";
    public string PortInfo { get => _portInfo; private set { _portInfo = value; OnPropertyChanged(); } }

    public ObservableCollection<string> AvailablePorts { get; } = new();

    private string? _selectedPort;
    public string? SelectedPort { get => _selectedPort; set { _selectedPort = value; OnPropertyChanged(); } }

    private int _baudRate = 9600;
    public int BaudRate { get => _baudRate; set { _baudRate = value <= 0 ? 9600 : value; OnPropertyChanged(); } }

    private bool _autoConnect = true;
    public bool AutoConnect { get => _autoConnect; set { _autoConnect = value; OnPropertyChanged(); } }

    private bool _autoReconnect = true;
    public bool AutoReconnect { get => _autoReconnect; set { _autoReconnect = value; OnPropertyChanged(); } }

    private bool _enableLogging = true;
    public bool EnableLogging { get => _enableLogging; set { _enableLogging = value; OnPropertyChanged(); } }

    private bool _isConnected;
    public string ConnectButtonText => _isConnected ? "Kes" : "Bağlan";

    public PanelPageVm Panel { get; } = new();
    public SettingsPageVm Settings { get; }
    public CalibrationPageVm Calibration { get; } = new();
    public DataPageVm Data { get; } = new();

    private object _currentPage;
    public object CurrentPage { get => _currentPage; private set { _currentPage = value; OnPropertyChanged(); } }

    public ICommand ShowPanelCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public ICommand ShowCalibrationCommand { get; }
    public ICommand ShowDataCommand { get; }

    public ICommand RefreshPortsCommand { get; }
    public ICommand ToggleConnectCommand { get; }

    private readonly SerialPort _sp = new();
    private readonly StringBuilder _buf = new();
    private readonly DispatcherTimer _reconnectTimer;

    private static string LogDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IProSensorPanel",
        "logs");

    public SensorPanelViewModel()
    {
        Settings = new SettingsPageVm(this);

        _currentPage = Panel;

        ShowPanelCommand = new RelayCommand(_ => CurrentPage = Panel);
        ShowSettingsCommand = new RelayCommand(_ => { SyncSettingsPage(); CurrentPage = Settings; });
        ShowCalibrationCommand = new RelayCommand(_ => CurrentPage = Calibration);
        ShowDataCommand = new RelayCommand(_ => CurrentPage = Data);

        RefreshPortsCommand = new RelayCommand(_ => RefreshPorts());
        ToggleConnectCommand = new RelayCommand(_ => ToggleConnect());

        _reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _reconnectTimer.Tick += (_, _) =>
        {
            if (!_isConnected && AutoReconnect)
            {
                TryAutoSelectPort();
                if (!string.IsNullOrWhiteSpace(SelectedPort))
                    Connect();
            }
        };

        RefreshPorts();
        SyncSettingsPage();

        if (AutoConnect)
        {
            TryAutoSelectPort();
            if (!string.IsNullOrWhiteSpace(SelectedPort))
                Connect();
        }
    }

    public void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var p in SerialPort.GetPortNames().OrderBy(p => p))
            AvailablePorts.Add(p);

        TryAutoSelectPort();
        Status = "Portlar yenilendi.";
        SyncSettingsPage();
    }

    public void ToggleConnect()
    {
        if (_isConnected) Disconnect("Kullanıcı tarafından bağlantı kesildi.");
        else Connect();

        OnPropertyChanged(nameof(ConnectButtonText));
        SyncSettingsPage();
    }

    private void Connect()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedPort))
            {
                Status = "Port seçili değil.";
                return;
            }

            if (_sp.IsOpen && string.Equals(_sp.PortName, SelectedPort, StringComparison.OrdinalIgnoreCase) && _sp.BaudRate == BaudRate)
            {
                Status = "Bağlantı zaten aktif.";
                return;
            }

            if (_sp.IsOpen)
                _sp.Close();

            _sp.PortName = SelectedPort;
            _sp.BaudRate = BaudRate;
            _sp.DtrEnable = false;
            _sp.RtsEnable = false;
            _sp.DataReceived -= Sp_DataReceived;
            _sp.DataReceived += Sp_DataReceived;
            _sp.Open();

            _isConnected = true;
            _reconnectTimer.Stop();
            PortInfo = $"Port: {SelectedPort} @ {BaudRate}";
            Status = "Bağlandı.";
            LogEvent($"CONNECTED {SelectedPort} {BaudRate}");
        }
        catch (Exception ex)
        {
            _isConnected = false;
            Status = $"Bağlantı hatası: {ex.Message}";
            LogEvent($"CONNECT_ERROR {ex.Message}");
            StartReconnectIfNeeded();
        }
    }

    private void Disconnect(string reason = "Bağlantı kesildi.")
    {
        try
        {
            _sp.DataReceived -= Sp_DataReceived;
            if (_sp.IsOpen) _sp.Close();
        }
        catch { }

        _isConnected = false;
        PortInfo = "Port: -";
        Status = reason;
        LogEvent($"DISCONNECTED {reason}");
        StartReconnectIfNeeded();
    }

    private void Sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var s = _sp.ReadExisting();
            if (string.IsNullOrEmpty(s)) return;

            _buf.Append(s);

            while (true)
            {
                var all = _buf.ToString();
                var idx = all.IndexOf('\n');
                if (idx < 0) break;

                var line = all.Substring(0, idx).Trim('\r', '\n', ' ');
                _buf.Clear();
                _buf.Append(all[(idx + 1)..]);

                if (line.Length > 0)
                    HandleLine(line);
            }
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Disconnect($"Bağlantı hatası: {ex.Message}");
            });
        }
    }

    private void HandleLine(string line)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            LogLine(line);

            if (!TryParse5(line, out var temps, out var hums))
            {
                Data.AddRaw(line);
                Status = "Veri geldi ama parse edilemedi.";
                return;
            }

            for (var i = 0; i < 5; i++)
                temps[i] += Calibration.GetOffset($"S{i + 1}");

            Panel.S1.SetTemp(temps[0]);
            Panel.S2.SetTemp(temps[1]);
            Panel.S3.SetTemp(temps[2]);
            Panel.S4.SetTemp(temps[3]);
            Panel.S5.SetTemp(temps[4]);

            if (hums[0].HasValue) Panel.S1.SetHum(hums[0]!.Value);
            if (hums[1].HasValue) Panel.S2.SetHum(hums[1]!.Value);
            if (hums[2].HasValue) Panel.S3.SetHum(hums[2]!.Value);
            if (hums[3].HasValue) Panel.S4.SetHum(hums[3]!.Value);

            Data.Add(
                temps[0].ToString("0.0", CultureInfo.InvariantCulture),
                temps[1].ToString("0.0", CultureInfo.InvariantCulture),
                temps[2].ToString("0.0", CultureInfo.InvariantCulture),
                temps[3].ToString("0.0", CultureInfo.InvariantCulture),
                temps[4].ToString("0.0", CultureInfo.InvariantCulture),
                line
            );

            Status = "Veri alınıyor…";
        });
    }

    private static bool TryParse5(string line, out double[] temps, out double?[] hums)
    {
        temps = new double[5];
        hums = new double?[4];

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

            switch (key)
            {
                case "S1":
                case "S1T": s1 = val; break;
                case "S2":
                case "S2T": s2 = val; break;
                case "S3":
                case "S3T": s3 = val; break;
                case "S4":
                case "S4T": s4 = val; break;
                case "S5":
                case "S5T": s5 = val; break;
                case "H1":
                case "S1H": hums[0] = val; break;
                case "H2":
                case "S2H": hums[1] = val; break;
                case "H3":
                case "S3H": hums[2] = val; break;
                case "H4":
                case "S4H": hums[3] = val; break;
            }
        }

        if (s1 is null || s2 is null || s3 is null || s4 is null || s5 is null)
            return false;

        temps[0] = s1.Value;
        temps[1] = s2.Value;
        temps[2] = s3.Value;
        temps[3] = s4.Value;
        temps[4] = s5.Value;

        return true;
    }

    private void TryAutoSelectPort()
    {
        if (!string.IsNullOrWhiteSpace(SelectedPort) && AvailablePorts.Contains(SelectedPort))
            return;

        SelectedPort = AvailablePorts.FirstOrDefault();
    }

    private void StartReconnectIfNeeded()
    {
        if (AutoReconnect && !_reconnectTimer.IsEnabled)
            _reconnectTimer.Start();
    }

    private void LogLine(string line)
    {
        if (!EnableLogging) return;
        WriteLog($"DATA {line}");
    }

    private void LogEvent(string text)
    {
        if (!EnableLogging) return;
        WriteLog($"EVENT {text}");
    }

    private static void WriteLog(string text)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var path = Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {text}{Environment.NewLine}");
        }
        catch
        {
            // log hatası uygulamayı durdurmamalı
        }
    }

    private void SyncSettingsPage()
    {
        Settings.SyncFromRoot();
        OnPropertyChanged(nameof(ConnectButtonText));
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public void Dispose()
    {
        _reconnectTimer.Stop();
        Disconnect("Uygulama kapanıyor.");
        _sp.Dispose();
    }
}
