using BluetoothAudioManager.Core.Models;

namespace BluetoothAudioManager.Core.Services;

/// <summary>
/// Decides when to warn about a low battery: once when a part reaches the level,
/// then again only after it has charged back above it.
/// </summary>
public sealed class LowBatteryWatcher
{
    private readonly HashSet<string> _alerted = [];

    /// <returns>A message for each part that has just become low.</returns>
    public IReadOnlyList<string> Update(AirPodsStatus status, int level)
    {
        var alerts = new List<string>();
        Check("Left pod", status.Left, level, alerts);
        Check("Right pod", status.Right, level, alerts);
        Check("Case", status.Case, level, alerts);
        return alerts;
    }

    /// <returns>A message if this part has just become low.</returns>
    public string? UpdatePart(string name, BatteryReading reading, int level)
    {
        if (reading.Percent is not { } percent)
        {
            return null;
        }

        if (percent > level)
        {
            _alerted.Remove(name);
            return null;
        }

        return !reading.IsCharging && _alerted.Add(name) ? $"{name} is at {percent}%" : null;
    }

    public void Reset() => _alerted.Clear();

    private void Check(string name, BatteryReading reading, int level, List<string> alerts)
    {
        if (UpdatePart(name, reading, level) is { } alert)
        {
            alerts.Add(alert);
        }
    }
}
