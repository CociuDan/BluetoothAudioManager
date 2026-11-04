using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;

namespace BluetoothAudioManager.Core;

/// <summary>Listens for AirPods broadcasts. Events are raised on a background thread.</summary>
public sealed class AirPodsMonitor : IDisposable
{
    private const short MinimumRssi = -95;

    private readonly NearestDeviceSelector _selector = new(TimeSpan.FromSeconds(3));
    private readonly Lock _lock = new();
    private BluetoothLEAdvertisementWatcher? _watcher;

    public event Action<AirPodsStatus>? StatusReceived;
    public event Action<BluetoothError>? ScanFailed;

    /// <summary>Starts scanning, replacing any earlier watcher because one that failed can't be relied on to restart.</summary>
    public void Start()
    {
        StopWatcher();

        _watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };

        // Ask Windows for every broadcast it hears, rather than an occasional sample of them.
        _watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.Zero;
        _watcher.Received += OnReceived;
        _watcher.Stopped += OnStopped;
        _watcher.Start();
    }

    public void Dispose() => StopWatcher();

    private void StopWatcher()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.Received -= OnReceived;
        _watcher.Stopped -= OnStopped;
        _watcher.Stop();
        _watcher = null;
    }

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        if (args.RawSignalStrengthInDBm < MinimumRssi)
        {
            return;
        }

        foreach (var manufacturerData in args.Advertisement.ManufacturerData)
        {
            if (manufacturerData.CompanyId != AirPodsAdvertisementParser.AppleCompanyId)
            {
                continue;
            }

            var status = AirPodsAdvertisementParser.Parse(manufacturerData.Data.ToArray());
            if (status is null)
            {
                continue;
            }

            AirPodsStatus selected;
            lock (_lock)
            {
                selected = _selector.Select(args.BluetoothAddress, args.RawSignalStrengthInDBm, status, args.Timestamp);
            }

            StatusReceived?.Invoke(selected);
        }
    }

    private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        if (args.Error != BluetoothError.Success)
        {
            ScanFailed?.Invoke(args.Error);
        }
    }
}
