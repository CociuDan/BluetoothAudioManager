using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Core.Interop;

// Windows' volume for one sound output. Only the methods up to GetMute are declared, in Windows' order, as that's all the app uses.
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
    void UnregisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
    void GetChannelCount(out uint count);
    void SetMasterVolumeLevel(float levelDecibels, ref Guid eventContext);
    void SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
    void GetMasterVolumeLevel(out float levelDecibels);
    void GetMasterVolumeLevelScalar(out float level);
    void SetChannelVolumeLevel(uint channel, float levelDecibels, ref Guid eventContext);
    void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
    void GetChannelVolumeLevel(uint channel, out float levelDecibels);
    void GetChannelVolumeLevelScalar(uint channel, out float level);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
}
