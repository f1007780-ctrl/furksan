using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace IProSensorPanel;

public sealed class SensorCardVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    private string _tText = "--";
    public string TText { get => _tText; private set { _tText = value; OnPropertyChanged(); } }

    private string _hText = "--";
    public string HText { get => _hText; private set { _hText = value; OnPropertyChanged(); } }

    private Brush _tempBrush = Brushes.LightGray;
    public Brush TempBrush { get => _tempBrush; private set { _tempBrush = value; OnPropertyChanged(); } }

    public SensorCardVm(string name) => Name = name;

    public void SetTemp(double t)
    {
        TText = t.ToString("0.0", CultureInfo.InvariantCulture);
        TempBrush = TempColor(t);
    }

    public void SetHum(double h)
    {
        HText = h.ToString("0", CultureInfo.InvariantCulture);
    }

    private static Brush TempColor(double t)
    {
        // basit renk map (istersen eski mantığına göre değiştiririz)
        if (t < 10) return new SolidColorBrush(Color.FromRgb(47, 99, 199));   // mavi
        if (t < 25) return new SolidColorBrush(Color.FromRgb(46, 168, 101));  // yeşil
        if (t < 35) return new SolidColorBrush(Color.FromRgb(255, 170, 0));   // turuncu
        return new SolidColorBrush(Color.FromRgb(233, 74, 74));               // kırmızı
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}