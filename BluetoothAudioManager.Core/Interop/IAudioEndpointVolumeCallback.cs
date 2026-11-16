using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Core.Interop;

// Public because Windows can only call back into interfaces visible to COM.
[ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioEndpointVolumeCallback
{
    [PreserveSig] int OnNotify(IntPtr notificationData);
}
