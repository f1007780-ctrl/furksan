using System.ComponentModel;

namespace IProSensorPanel;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public SensorCardVm S1 { get; } = new("S1");
    public SensorCardVm S2 { get; } = new("S2");
    public SensorCardVm S3 { get; } = new("S3");
    public SensorCardVm S4 { get; } = new("S4");
    public SensorCardVm S5 { get; } = new("S5");

    public void Update(SensorValues v)
    {
        S1.SetTemp(v.S1);
        S2.SetTemp(v.S2);
        S3.SetTemp(v.S3);
        S4.SetTemp(v.S4);
        S5.SetTemp(v.S5);
    }

    public void UpdateS5(double s5)
    {
        S5.SetTemp(s5);
    }
}
