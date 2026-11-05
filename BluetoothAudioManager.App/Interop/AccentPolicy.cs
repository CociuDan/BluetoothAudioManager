using System.Runtime.InteropServices;

namespace BluetoothAudioManager.App.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct AccentPolicy
{
    public int AccentState;
    public int AccentFlags;
    public int GradientColor;
    public int AnimationId;
}
