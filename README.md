# BluetoothAudioManager

A Windows app that shows the battery of each AirPod and the case, and connects or disconnects your paired Bluetooth headphones.

## How it works

### Battery levels

The app listens for the Bluetooth LE messages AirPods broadcast, so it doesn't need a driver. Apple doesn't document this format, so a firmware update could break it.

- Readings come in 10% steps. The badge is green above 20%, yellow from 10% to 20%, and red below 10%.
- The case usually reports only with a pod inside and the lid open.
- A level clears after 1 minute without an update, and all levels clear when the AirPods disconnect. Broadcasts are ignored until they reconnect.
- Broadcasts aren't tied to a paired device, so the app shows the strongest signal nearby. It could show someone else's AirPods if yours aren't broadcasting.
- AirPods Max aren't supported.
- The Marshall Major IV has no battery level, because Windows doesn't report one for it.

### Connections

The app lists every paired Bluetooth audio device, connected or not, with connected devices first, and checks for changes every 5 seconds. AirPods are recognized by "AirPods" in their Windows name, so a renamed pair appears as generic headphones with no battery levels. If you've paired more than one set of AirPods, they share one row. The window fits two devices, then scrolls.

The connection state comes from Windows' Bluetooth status for those devices.

The Connect button becomes Disconnect while a device is connected, and the Reconnect icon button next to it disconnects the device and connects it again. These buttons ask the Windows Bluetooth audio driver to make the change, wait up to 15 seconds for each step, and show a failure message if it doesn't happen.

### Ear detection

The AirPods settings have a toggle that pauses media when a pod comes out, off by default. Media resumes when the pods that were in are back, but only if the app paused it and the AirPods are the system output. It reads in-ear status from the same broadcasts as the battery levels, so someone else's AirPods nearby could trigger it. 

### Low battery notification

The AirPods settings also have a toggle and a slider, from 10% to 50%, for a Windows notification when the left pod, right pod or case reaches that level. It's off by default and set to 20%. Each part alerts once, then again only after it has charged back above the level, and a part that's charging doesn't alert. The Marshall Major IV can't alert, because its level can't be read.

Settings are saved in `%AppData%\BluetoothAudioManager\settings.json`, and device readings are never saved.

### System output

The headphones button to the left of Connect makes that device the system sound output, using its stereo output if it has one. It's highlighted on the device currently in use. Windows has no public API for this, so the app uses an undocumented interface that an update could change.

### System tray

The app runs from the system tray. Click its icon to show or hide the window above the bottom-right of the taskbar. Clicking elsewhere or pressing the window's X hides it, and the app keeps running. Right-click the icon for **Start with Windows** and **Exit**.

Starting with Windows opens the app hidden in the tray. The setting stores the path of the build you turned it on from, so turn it on again if that build moves.

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
