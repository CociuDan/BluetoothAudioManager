using BluetoothAudioManager.Core.Interop;

namespace BluetoothAudioManager.Core.Services;

/// <summary>The sound outputs Windows can currently play through, of any kind.</summary>
public static class AudioOutputs
{
    private const uint RenderFlow = 0;
    private const uint ActiveOnly = 0x1;

    /// <returns>The outputs' names as Windows shows them, such as "Speakers (Jabra Link 370)".</returns>
    public static IReadOnlyList<string> FindActiveNames()
    {
        var enumerator = AudioDeviceEnumerator.Create();
        enumerator.EnumAudioEndpoints(RenderFlow, ActiveOnly, out var devices);
        devices.GetCount(out var count);

        var names = new List<string>();
        for (uint i = 0; i < count; i++)
        {
            devices.Item(i, out var device);

            try
            {
                if (BluetoothAudioEndpoint.GetFriendlyName(device) is { } name)
                {
                    names.Add(name);
                }
            }
            catch (Exception)
            {
                // An output that can't be read is skipped, as elsewhere.
            }
        }

        return names;
    }
}
