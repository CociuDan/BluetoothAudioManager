namespace BluetoothAudioManager.Shared.Enums;

public enum UpdateState
{
    /// <summary>The installed version is the latest, or the newer one was dismissed.</summary>
    None,

    /// <summary>A newer version is downloaded, checked and unpacked, ready to install.</summary>
    Ready,

    /// <summary>
    /// A newer version is being downloaded, unpacked and got ready beside the app, so installing only switches folders.
    /// The app shows it as Preparing for install.
    /// </summary>
    Preparing,

    /// <summary>The updater couldn't check, such as when GitHub can't be reached, or isn't there to ask.</summary>
    Failed,
}
