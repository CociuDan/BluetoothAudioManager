using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Updater.Interop;

/// <summary>Windows' STARTUPINFOW, for starting the app in a user's session.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct StartupInfo
{
    public int Size;
    public string? Reserved;
    public string? Desktop;
    public string? Title;
    public int X;
    public int Y;
    public int Width;
    public int Height;
    public int CharactersWide;
    public int CharactersHigh;
    public int FillAttribute;
    public int Flags;
    public short ShowWindow;
    public short Reserved2Size;
    public IntPtr Reserved2;
    public IntPtr StandardInput;
    public IntPtr StandardOutput;
    public IntPtr StandardError;
}
