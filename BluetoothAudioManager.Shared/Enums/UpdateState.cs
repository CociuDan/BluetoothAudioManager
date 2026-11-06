namespace BluetoothAudioManager.Shared.Enums;

public enum UpdateState
{
    /// <summary>The installed version is the latest.</summary>
    None,

    /// <summary>A newer version is downloaded and checked, ready to install.</summary>
    Ready,

    /// <summary>The updater couldn't check, such as when GitHub can't be reached, or isn't there to ask.</summary>
    Failed,
}
