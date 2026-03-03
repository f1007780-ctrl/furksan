using System.IO.Ports;
using System.Text;

namespace IProSensorPanel;

public sealed class SerialService : IDisposable
{
    private SerialPort? _sp;
    private readonly StringBuilder _buffer = new();

    public event Action<string>? OnLine;

    public void Connect(string portName, int baudRate)
    {
        Disconnect();

        _sp = new SerialPort(portName, baudRate)
        {
            NewLine = "\n",
            Encoding = Encoding.ASCII,
            DtrEnable = true,
            RtsEnable = true,
            ReadTimeout = 500,
            WriteTimeout = 500
        };

        _sp.DataReceived += SpOnDataReceived;
        _sp.Open();
    }

    private void SpOnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_sp is null) return;

            var data = _sp.ReadExisting();
            if (string.IsNullOrEmpty(data)) return;

            _buffer.Append(data);

            while (true)
            {
                var s = _buffer.ToString();
                var idx = s.IndexOf('\n');
                if (idx < 0) break;

                var line = s.Substring(0, idx).Trim('\r', '\n', ' ');
                _buffer.Clear();
                _buffer.Append(s.Substring(idx + 1));

                if (line.Length > 0)
                    OnLine?.Invoke(line);
            }
        }
        catch
        {
            // DataReceived thread patlamasın
        }
    }

    public void Disconnect()
    {
        if (_sp is null) return;

        try { _sp.DataReceived -= SpOnDataReceived; } catch { }
        try { if (_sp.IsOpen) _sp.Close(); } catch { }
        try { _sp.Dispose(); } catch { }

        _sp = null;
        _buffer.Clear();
    }

    public void Dispose() => Disconnect();
}