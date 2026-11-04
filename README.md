# BluetoothAudioManager

A Windows app that shows the battery of each AirPod and the case.

## How it works

The app listens for the Bluetooth LE messages AirPods broadcast, so it doesn't need a driver or a connection to the buds. Apple doesn't document this format, so a firmware update could break it.

- Readings come in 10% steps.
- The case usually reports only with a pod inside and the lid open. While the AirPods are connected, the app keeps showing the last level each part reported and how long ago that was.
- Broadcasts aren't tied to a paired device, so the app shows the strongest signal nearby. It could show someone else's AirPods if yours aren't broadcasting.
- AirPods Max aren't supported.

The Connected or Disconnected line comes from Windows' Bluetooth status for your paired AirPods, separately from the broadcasts. It finds them by name, so the device name must contain "AirPods". When they disconnect, the app clears all battery levels and ignores broadcasts until they reconnect.

The Connect button appears enabled while the AirPods are disconnected. It asks the Windows Bluetooth audio driver to connect them, then waits up to 15 seconds and shows "Connection failed" if they don't connect.

## Requirements

- Windows 10 version 2004 or later
- .NET 10 SDK
- A Bluetooth adapter that supports Bluetooth LE

## Run

```bash
dotnet run --project BluetoothAudioManager.App
```

In VS Code, open this folder and press F5. This needs the C# Dev Kit extension, which VS Code recommends when you open the folder.

## Test

```bash
dotnet test
```
