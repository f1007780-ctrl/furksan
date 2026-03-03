using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace IProSensorPanel;

public sealed class SensorPanelViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // Üst yazılar
    private string _status = "Hazır";
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }

    private string _portInfo = "Port: -";
    public string PortInfo { get => _portInfo; private set { _portInfo = value; OnPropertyChanged(); } }

    // Port ayarları
    public ObservableCollection<string> AvailablePorts { get; } = new();

    private string? _selectedPort;
    public string? SelectedPort { get => _selectedPort; set { _selectedPort = value; OnPropertyChanged(); } }

    private bool _autoConnect = true;
    public bool AutoConnect { get => _autoConnect; set { _autoConnect = value; OnPropertyChanged(); } }

    private bool _isConnected;
    public string ConnectButtonText => _isConnected ? "Kes" : "Bağlan";

    // Sayfalar
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

    // Settings sayfasının butonu root’tan çağırıyor
    public ICommand RefreshPortsCommand { get; }
    public ICommand ToggleConnectCommand { get; }

    // Serial
    private readonly SerialPort _sp = new();
    private readonly System.Text.StringBuilder _buf = new();

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

        RefreshPorts();
        SyncSettingsPage();

        if (AutoConnect && !string.IsNullOrWhiteSpace(SelectedPort))
            Connect();
    }

    public void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var p in SerialPort.GetPortNames())
            AvailablePorts.Add(p);

        if (string.IsNullOrWhiteSpace(SelectedPort) && AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];

        Status = "Portlar yenilendi.";
        SyncSettingsPage();
    }

    public void ToggleConnect()
    {
        if (_isConnected) Disconnect();
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

            _sp.PortName = SelectedPort;
            _sp.BaudRate = 9600;
            _sp.DtrEnable = true;
            _sp.RtsEnable = true;
            _sp.DataReceived -= Sp_DataReceived;
            _sp.DataReceived += Sp_DataReceived;

            _sp.Open();

            _isConnected = true;
            PortInfo = $"Port: {SelectedPort} @ 9600";
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
            _sp.DataReceived -= Sp_DataReceived;
            if (_sp.IsOpen) _sp.Close();
        }
        catch { /* yut */ }

        _isConnected = false;
        PortInfo = "Port: -";
        Status = "Kesildi.";
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
                _buf.Append(all.Substring(idx + 1));

                if (line.Length > 0)
                    HandleLine(line);
            }
        }
        catch { /* yut */ }
    }

    // S1:24.1;S2:25.0;S3:23.8;S4:26.2;S5:18.7
    private void HandleLine(string line)
    {
        // UI thread’e taşı
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (!TryParse5(line, out var s1, out var s2, out var s3, out var s4, out var s5))
            {
                Status = "Veri geldi ama parse edilemedi.";
                return;
            }

            // Kalibrasyon offset uygula
            s1 += Calibration.GetOffset("S1");
            s2 += Calibration.GetOffset("S2");
            s3 += Calibration.GetOffset("S3");
            s4 += Calibration.GetOffset("S4");
            s5 += Calibration.GetOffset("S5");

            // Kartları besle
            Panel.S1.SetTemp(s1);
            Panel.S2.SetTemp(s2);
            Panel.S3.SetTemp(s3);
            Panel.S4.SetTemp(s4);
            Panel.S5.SetTemp(s5);

            // S1-S4 humidity yoksa -- kalsın; varsa burada set edebilirsin
            // Panel.S1.SetHum(h1) ... (Arduino gönderiyorsa ekleriz)

            // Veri sayfasına satır
            Data.Add(
                s1.ToString("0.0", CultureInfo.InvariantCulture),
                s2.ToString("0.0", CultureInfo.InvariantCulture),
                s3.ToString("0.0", CultureInfo.InvariantCulture),
                s4.ToString("0.0", CultureInfo.InvariantCulture),
                s5.ToString("0.0", CultureInfo.InvariantCulture)
            );

            Status = "Veri alınıyor…";
        });
    }

    private static bool TryParse5(string line, out double s1, out double s2, out double s3, out double s4, out double s5)
    {
        s1 = s2 = s3 = s4 = s5 = 0;

        double? a = null, b = null, c = null, d = null, e = null;

        var parts = line.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            var kv = p.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (kv.Length != 2) continue;

            var key = kv[0].ToUpperInvariant();
            var raw = kv[1].Replace(',', '.');

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                continue;

            if (key == "S1") a = val;
            else if (key == "S2") b = val;
            else if (key == "S3") c = val;
            else if (key == "S4") d = val;
            else if (key == "S5") e = val;
        }

        if (a is null || b is null || c is null || d is null || e is null)
            return false;

        s1 = a.Value; s2 = b.Value; s3 = c.Value; s4 = d.Value; s5 = e.Value;
        return true;
    }

    private void SyncSettingsPage()
    {
        Settings.SyncFromRoot();
        OnPropertyChanged(nameof(ConnectButtonText));
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}