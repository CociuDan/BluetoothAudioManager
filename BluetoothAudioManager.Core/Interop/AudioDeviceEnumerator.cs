namespace BluetoothAudioManager.Core.Interop;

internal static class AudioDeviceEnumerator
{
    // Created by ID, because declaring this class in two places makes the runtime confuse the two.
    private static readonly Guid ClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    public static IMMDeviceEnumerator Create() =>
        (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(ClassId)!)!;
}
