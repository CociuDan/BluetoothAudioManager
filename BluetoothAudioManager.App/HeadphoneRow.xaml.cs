using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.App;

/// <summary>A paired audio device with no special support: its picture and connection buttons.</summary>
public partial class HeadphoneRow : UserControl, IDisposable
{
    private readonly DeviceConnectionMonitor _connection;

    public HeadphoneRow(string deviceName, string title, ImageSource picture)
    {
        InitializeComponent();

        DeviceName = deviceName;
        _connection = new DeviceConnectionMonitor(deviceName);
        Header.Title = title;
        Picture.Source = picture;
        Header.Follow(_connection);
    }

    public string DeviceName { get; }

    public bool IsConnected => Header.State == DeviceConnectionState.Connected;

    public bool ShowDivider
    {
        set => Divider.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public void RefreshOutputState() => Header.RefreshOutputState();

    public void Dispose()
    {
        Header.Stop();
        _connection.Dispose();
    }
}
