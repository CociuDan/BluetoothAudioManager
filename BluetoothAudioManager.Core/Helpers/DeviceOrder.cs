namespace BluetoothAudioManager.Core.Helpers;

/// <summary>The order the user arranged their devices in, by a key for each device such as its Windows name.</summary>
public static class DeviceOrder
{
    /// <summary>
    /// Puts the devices in the saved order. Devices that aren't in it, such as newly paired ones,
    /// go after the rest and keep the order they came in.
    /// </summary>
    public static List<T> Sort<T>(IEnumerable<T> devices, Func<T, string> keyOf, IReadOnlyList<string> saved) =>
        devices
            .OrderBy(device => IndexIn(saved, keyOf(device)))
            .ToList();

    /// <summary>
    /// The order to save after the user moves a device: the order shown, with devices that aren't shown right now
    /// kept just after the device they followed before, so they return to the same place.
    /// </summary>
    public static List<string> Merge(IReadOnlyList<string> saved, IReadOnlyList<string> shown)
    {
        var merged = shown.ToList();
        var position = -1;

        foreach (var key in saved)
        {
            var index = merged.FindIndex(other => string.Equals(other, key, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                position = index;
            }
            else
            {
                merged.Insert(++position, key);
            }
        }

        return merged;
    }

    private static int IndexIn(IReadOnlyList<string> saved, string key)
    {
        for (var index = 0; index < saved.Count; index++)
        {
            if (string.Equals(saved[index], key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
