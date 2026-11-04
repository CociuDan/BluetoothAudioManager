namespace BluetoothAudioManager.Core;

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

    public void Reset() => _alerted.Clear();

    private void Check(string name, BatteryReading reading, int level, List<string> alerts)
    {
        if (reading.Percent is not { } percent)
        {
            return;
        }

        if (percent > level)
        {
            _alerted.Remove(name);
        }
        else if (!reading.IsCharging && _alerted.Add(name))
        {
            alerts.Add($"{name} is at {percent}%");
        }
    }
}
