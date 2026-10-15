# Intuime

Intuime is a Windows desktop app for building touchscreen experiences. You design scenes on a canvas, drop in text, pictures, video, buttons, and hotspots, then wire what happens when someone taps, waits, or presses a key. Press Play and the same file runs as a kiosk.

It follows the same workflow as [Intuiface](https://www.intuiface.com/): a visual composer and a player. Intuime is its own app. It is not affiliated with Intuiface and does not open Intuiface project files.

The gallery sample opens on first launch. Tap through it with Preview, or press Play for full screen.

## What you can build

- Scenes at any resolution, including 1920×1080 and portrait 1080×1920
- Text, images, rectangles, ellipses, buttons, invisible hotspots, clocks, web links, video, audio, data lists, and counters
- Drag, resize, snap, zoom, layers, lock, hide, align, undo, copy, and paste
- Triggers: tap, double-tap, press, release, scene enter, scene leave, timer, and key
- Actions: change scene, show or hide, set text, move and resize with a duration, set variables, play media, open a web page, wait, restart, or exit
- `{{variables}}` in text, including `{{time}}` and `{{date}}`
- A data list that writes the tapped row into a variable, and a counter with a min and max
- Save a portable `.intuime` project. Images and media are copied into an `assets` folder beside the file

## Install on Windows

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) if `dotnet` is not already on your PATH. The script below can install it with winget.
2. In this folder, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-windows.ps1
```

3. Start `publish\win-x64\Intuime.exe`.

That publish is self-contained, so the machine that runs the exe does not need the SDK. Video and audio are included through LibVLC (LGPL). See `THIRD-PARTY-NOTICES.txt`.

To work on the source instead of publishing:

```powershell
dotnet run --project src/Intuime
```

## Player keys

| Key | Action |
| --- | --- |
| Esc | Leave the player and return to the composer |
| F11 | Full screen |
| Left / Right | Previous / next scene in the gallery sample |

In the composer, F5 previews in a window and Shift+F5 presents full screen. Ctrl+S saves, Ctrl+Z undoes, and Ctrl+mouse wheel zooms.

## Project file

A project is a JSON file with the extension `.intuime`. Keep the `assets` folder next to it. Open it from File, or pass the folder around on a USB stick.

## Tests

```powershell
dotnet test Intuime.sln
dotnet run --project src/Intuime -- --self-check
```
