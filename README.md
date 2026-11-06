# BluetoothAudioManager

A Windows app that shows the battery of each AirPod and the case, and connects or disconnects your paired Bluetooth headphones.

![The window with both devices disconnected](docs/images/1-disconnected.png)

![Battery badges in green, yellow and red, with a green charging bolt next to the right pod's badge](docs/images/2-colors-and-charging.png)

![AirPods settings open, showing ear detection and low battery notification](docs/images/3-settings.png)

![The window with only AirPods paired](docs/images/4-one-device.png)

![The message shown when no headphones are paired](docs/images/5-no-devices.png)

## How it works

### Battery levels

The app listens for the Bluetooth LE messages AirPods broadcast, so it doesn't need a driver. Apple doesn't document this format, so a firmware update could break it.

- Readings come in 10% steps. The badge is green above 20%, yellow from 10% to 20%, and red below 10%.
- A green bolt next to a badge shows that part is charging. AirPods and Jabra headsets report charging. Windows stores only a level for other headphones, so they never show the bolt.
- The case usually reports only with a pod inside and the lid open.
- A level clears after 1 minute without an update, and all levels clear when the AirPods disconnect. Broadcasts are ignored until they reconnect.
- Broadcasts aren't tied to a paired device, so the app shows the strongest signal nearby. It could show someone else's AirPods if yours aren't broadcasting.
- AirPods Max broadcasts aren't read. AirPods Max appear like other headphones, with their own picture.
- For other headphones, the app reads the level Windows stores, if there is one, using a property Microsoft doesn't document. A device with a level gets a battery badge and a settings button with its own low battery notification. A device without one, like the Marshall Major IV, shows neither.

### Connections

The app lists every paired Bluetooth audio device, connected or not, and checks for changes every 5 seconds. Devices keep their place when they connect or disconnect. To reorder them, hover over a device's name and drag the six-dot handle that appears to its left. Only the handle moves a device. The device lifts off the list and follows the pointer, the others slide aside to show where it will land, and it settles into place when you let go. The order is saved, and a device you unpair keeps its place for when you pair it again. A newly paired device goes last. AirPods are recognized by "AirPods" in their Windows name, so a renamed pair appears as generic headphones with no battery levels. If you've paired more than one set of AirPods, they share one row. AirPods Max are recognized by "AirPods Max" in their name or by their product ID, and get a row of their own. Each row is titled with the device's name in Windows. Every row is the same height, keeping space for a battery badge even on headphones that don't report one. The window shows two devices by default, then scrolls. Drag its top or bottom edge to fit anywhere from one device to all of them, and it remembers that height. With only one device, the window is that device's size.

The connection state comes from Windows' Bluetooth status for those devices.

The Connect button becomes Disconnect while a device is connected, and the Reconnect icon button next to it disconnects the device and connects it again. The settings button only works while the device is connected, and open settings close when it disconnects.

Jabra headsets that connect through a Jabra Link USB adapter aren't paired with Windows, so the app finds the adapter among the sound outputs instead, and lists it while it's plugged in. With Jabra's kit running, the row shows the headset as connected while it's linked to the adapter, and as disconnected when it's off or out of range. If the kit supports the adapter, the row also gets Connect, Disconnect and Reconnect, which ask the adapter to connect the headset last seen on it. Jabra lists this for the Link 380 and Link 390, so other adapters may not get these buttons. Without the kit, the row has only the system output button, and shows as connected whenever the adapter is plugged in. A Jabra Link 370 shows the Jabra Evolve 65 picture, as that's the headset it comes with.

The headset's name, battery level and charging come from Jabra's own kit, which needs a partner key. Jabra gives one free to anyone registered on [developer.jabra.com](https://developer.jabra.com/site/global/docs/partner-key.gsp). Add it to the settings file as `"JabraPartnerKey": "your-key"`, then restart the app. Without a key, or for a headset the kit can't read, the row shows the adapter's name and no battery. Charging from a USB cable makes the headset appear a second time, over USB, so the row uses whichever copy reports a level, and keeps the last level if neither does. With a level, the row also gets a settings button for its low battery notification. These buttons ask the Windows Bluetooth audio driver to make the change, wait up to 15 seconds for each step, and show a failure message if it doesn't happen.

### Ear detection

The AirPods settings have a toggle that pauses media when a pod comes out, off by default. Media resumes when the pods that were in are back, but only if the app paused it and the AirPods are the system output. It reads in-ear status from the same broadcasts as the battery levels, so someone else's AirPods nearby could trigger it. 

### Low battery notification

The AirPods settings also have a toggle and a slider, from 10% to 50%, for a Windows notification when the left pod, right pod or case reaches that level. It's off by default and set to 20%. Each part alerts once, then again only after it has charged back above the level, and a part that's charging doesn't alert.
Settings are saved in `%AppData%\BluetoothAudioManager\settings.json`, and device readings are never saved.

### System output

The headphones button to the left of Connect makes that device the system sound output, using its stereo output if it has one. It's highlighted on the device currently in use. Windows has no public API for this, so the app uses an undocumented interface that an update could change.

### System tray

The app runs from the system tray, where its icon is a Bluetooth symbol in front of a gray speaker. The window and the program file use the same icon, drawn in `Assets/TrayIcon.svg` and `Assets/AppIcon.svg`, which the `.ico` files are made from. Click the tray icon to show or hide the window above the bottom-right of the taskbar. Clicking elsewhere or pressing the window's X hides it, and the app keeps running. Right-click the icon for **Start with Windows**, **Check for updates** and the installed version, and **Exit**.

The gear next to the window's X shows the app's settings in place of the devices, and clicking it again goes back. **Start with Windows** there is the same setting as in the tray menu. **Check for updates automatically**, in the installed app only, is described under updates below. **Transparency** sets how see-through the window is, from solid to 100% in steps of 5%, and is 25% by default. It never goes further, so the window always stays a frosted panel behind its contents. It applies straight away, also to the card lifted while dragging a device, and is saved with the other settings.

Starting with Windows opens the app hidden in the tray. The setting stores the path of the build you turned it on from, so turn it on again if that build moves. The installed app keeps the same path through updates, and uninstalling it turns the setting off for everyone.

### Install and updates

Install the app by running `BluetoothAudioManager-Setup.exe` from the latest release. It's one file that needs nothing else installed, not even .NET. Windows asks for administrator permission once, as it opens. Its page asks where to install, with `C:\Program Files\BluetoothAudioManager` filled in, or the folder it's already in. Picking a folder with **Browse…** installs into a `BluetoothAudioManager` folder inside it. **Install** then shows its progress page while it copies the app, adds Start menu shortcuts for the app and its uninstaller, adds an entry in **Installed apps**, and sets up the updater. Then it opens the app, without administrator rights. It's installed for everyone on the PC. Running a newer installer over it replaces it. The installer isn't signed, so Windows SmartScreen may warn about it the first time: choose **More info**, then **Run anyway**.

The install folder holds `app`, the app itself, `updater`, the updater service, `Uninstall.exe`, a copy of the installer, and the hidden `.update` folder, where the updater keeps the latest version's installer. Only administrators and Windows can change any of it, which the installer makes sure of for a folder other than Program Files too. The installer is its own small app, in `BluetoothAudioManager.Installer`, and carries the app and the updater zipped inside it. A copy built anywhere but the pipeline has nothing inside to install, and says so.

**Updates.** Programs in Program Files can only be changed with administrator rights, so a Windows service, **Bluetooth Audio Manager Updater** in `BluetoothAudioManager.Updater`, does it. It starts with Windows and checks the latest release when it starts and every 4 hours. To change how often, set `CheckEvery`, as hours:minutes:seconds such as `"04:00:00"` and at least 10 seconds, in `C:\ProgramData\BluetoothAudioManager\updater.json`, which it reads before each wait. **Check for updates automatically** in the app's settings turns this checking off or on again, for everyone on the PC, as there's one updater for the whole PC. **Check now**, under it, checks straight away, like **Check for updates** in the tray menu, and says what it found. The app asks the updater to save it there as `CheckAutomatically`, and only the installed app may ask. Off, nothing is checked, downloaded or offered by itself, and turning it on checks straight away. **Check for updates** in the tray menu checks straight away, and says if there's nothing new or GitHub can't be reached. The updater downloads a new version's installer, the same `BluetoothAudioManager-Setup.exe`, to the install folder's hidden `.update` folder, and checks it against the SHA-256 checksum GitHub lists for the release file. There's only ever that one file, so each newer download replaces the one before. It writes what it does to `updater.log` beside `updater.json`.

The app asks the updater every minute whether a version is ready. Then a Windows notification offers **Update now** and **Later**, the tray menu gets **Update to *version*** at the top, and the window shows a bar with **Update now**. Updating closes the app, and the updater checks the downloaded installer once more and runs it in its update mode, with no pages. That stops the updater, swaps in the new app and updater, puts the old ones back if that fails, starts the updater again, and opens the app again for you within a few seconds, with the window showing if it was. A version that fails to install isn't offered again until a newer one comes out. If someone else signed in to the PC has the app open, the update waits until they close it. Only the app restarts: connected headphones stay connected, the sound output stays the same and playing audio carries on, as Windows holds those, not the app. If ear detection has paused media, the update waits until it resumes, so the pause isn't forgotten. After **Later**, the update installs the next time the app starts, before its window opens. If it can't reach GitHub, it shows nothing and tries again at the next check. The app only asks for the status, a check, or the install of what the updater downloaded and checked itself, and only the installed app can ask for an install, so nothing can use the updater to run its own files with administrator rights. Builds that weren't installed, like one run from the code, never ask.

**Uninstalling.** Uninstall from **Installed apps**, or with **Uninstall Bluetooth Audio Manager** in the Start menu. After Windows asks for permission, its page lists what goes, and **Uninstall** closes the app for everyone and removes:

- the app, the updater service, the downloaded installer, and the settings and log in `C:\ProgramData\BluetoothAudioManager`
- the shortcuts, the entry in Installed apps, and the app's keys in the registry
- for everyone on the PC, including people who aren't signed in: their settings in `%AppData%\BluetoothAudioManager`, their **Start with Windows** entry, and what Windows keeps for the app's notifications

Bluetooth pairings and the sound output stay, as they belong to Windows. Only the app's own files are removed from the install folder, then the folder if it's empty. The uninstaller runs from a temporary copy, which Windows deletes when it next restarts.

The repository is private, so the updater downloads releases with a read-only GitHub token built in by the pipeline. It's never in the code, and the app doesn't carry it. To set it up:

1. On GitHub, open **Settings**, **Developer settings**, **Personal access tokens**, **Fine-grained tokens**, then **Generate new token**. Give it access to only this repository, with **Contents** set to **Read-only**, and copy it.
2. In this repository, open **Settings**, **Secrets and variables**, **Actions**, then **New repository secret**. Name it `UPDATE_TOKEN` and paste the token.

The Build workflow stops if the secret is missing. Anyone with the installed app could take the token out of the updater and read this repository, though not change it. Before the token expires, make a new one, replace the secret and run Build: installed copies download that update with the old token and get the new one with it. A copy that stays offline until the old token expires has to be installed again by hand.

### Product images

The AirPods pictures in `Assets` come from Apple, the Marshall pictures (`MajorIV.png` and the other Major, Mid, Milton, Monitor, Minor, Mode and Motif files) from Marshall, and `Assets/JabraEvolve65.png` and `Assets/JabraEvolve65Mono.png` from Jabra, and those companies own them. The Minor II bud comes from a Marshall retailer's studio photo. The pod and Marshall images are edited cut-outs, and each right earbud is its left one mirrored. They're here for private use only, so remove them before publishing this repo or sharing the app.

The AirPods pictures are chosen by the product ID Windows stores for the paired AirPods. AirPods 3, AirPods 4 and AirPods Pro have their own pictures, and all AirPods Pro use the AirPods Pro 1 pictures. AirPods 1, AirPods 2 and models not recognized yet, such as AirPods 5 and AirPods Pro 3, show the AirPods 4 pictures.

Other headphones get the picture whose entry in `DevicePictures` matches part of their Windows name, such as "MAJOR IV", or a generic headphones placeholder if none does. Names match as whole words, ignoring capitals and dots, and the longest match wins, so "MID ANC" and "MID", or "MONITOR III" and "MONITOR II", each get their own picture. Every Marshall Bluetooth headphone and earbud model has one. Earbuds, such as the Marshall Minor, Mode and Motif, are shown like the AirPods: left bud, right bud and case, with one battery badge under the buds if Windows reports a level. To add a device, put its pictures in `Assets` and add a line to `BluetoothAudioManager.Core/Helpers/DevicePictures.cs`.

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

## Build on GitHub

The **Build** workflow in `.github/workflows/build.yml` builds the app in Release mode on a GitHub-hosted Windows machine, runs the tests, publishes the app and the updater self-contained so they run without .NET installed, and makes the installer with both inside it. Start it from the repository's **Actions** tab: choose **Build**, then **Run workflow**. Build output is never committed.

Each build that passes gets a version, which the app's files carry, and its commit is tagged with it, such as `v0.0.3`. Leave **Version** empty to add 1 to the last number of the highest version so far, starting at 0.0.1. To change it, enter a version such as `2.0.0` when you start the run, and later builds count up from there. A version can be used only once. A build that fails doesn't use up its version. Builds on your own PC are 1.0.0. `.github/scripts/Get-NextVersion.ps1` works the version out.

Each run is titled with its version in the Actions list, such as **Build 0.0.5**. GitHub sets a run's title as it starts, before anything inside it runs, and it can only read what you type and the repository's variables. So each run that passes saves the following version in the repository variable `NEXT_VERSION`, for the next run's title. GitHub's own token can't change variables, so this needs a second token:

1. On GitHub, create another fine-grained token as for `UPDATE_TOKEN`, with access to only this repository, and with **Variables** set to **Read and write** and nothing else. Don't give `UPDATE_TOKEN` this, as it's built into the updater on every PC.
2. Add it as a repository secret named `VARIABLES_TOKEN`.

The first run after that creates the variable. Without the secret, or once the token expires, releases are still made, and the run only warns that it couldn't save the next version. The next title then shows **(next version)**, or the last version it saved. The version itself always comes from the tags, so only the title can be off.

Each version also gets a release, shown in the **Releases** box on the repository's main page, with the newest marked **Latest**. Its page lists the commits since the previous release. Its one file is `BluetoothAudioManager-Setup.exe`, for installing and for the updater to download, kept until you delete the release. The updater only looks at the latest release, so deleting it makes the one before it the latest.
