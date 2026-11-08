# TufDood — Tray app to tame an ASUS TUF laptop with a Ryzen CPU 🌡️

**Version 0.1** · [Leer en español](README.es.md)

TufDood is a small Windows tray app, inspired by [G-Helper](https://github.com/seerge/g-helper), that lets you:

- **Cap the CPU temperature** (50–95 °C) with RyzenAdj `--tctl-temp`. The limit is re-applied automatically (configurable interval) and after events that usually reset it: resume from sleep, plugging or unplugging the charger, changing the Windows power plan and changing the power mode.
- **Switch the power mode** Silent / Balanced / Turbo, like Fn+F5 in G-Helper. TufDood listens to the ASUS key event, and also offers **Ctrl+Alt+F5** as a global hotkey.
- **Pick the Windows power plan** from the tray menu.
- **Set a battery charge limit** (60 / 70 / 80 / 90 / 100 %).
- See an **on-screen message** when the mode, plan or charge limit changes.
- **Start with Windows** (scheduled task that runs as administrator without a UAC prompt, also on battery).

Dark interface, texts follow the Windows language (English or Spanish), right-click menu on the window and on the tray icon. Log at `%AppData%\TufDood\tufdood.log` (right-click → *View log*) and settings at `%AppData%\TufDood\settings.json`.

> Only tested on an **ASUS TUF A15 FA506IU** (Ryzen 5 4600H). Use it at your own risk: TufDood changes CPU power/thermal limits and battery charging through RyzenAdj and the ASUS firmware interface.

## Requirements

- Windows 10 / 11 x64 and the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or publish self-contained).
- A Ryzen **Mobile** APU supported by [RyzenAdj](https://github.com/FlyGoat/RyzenAdj). Newer chips are not supported by RyzenAdj v0.19.0 (for example a Ryzen 7 7445HS fails with *"Fam19h: unsupported model"*); TufDood cannot work around that.
- For power mode, Fn+F5 and charge limit: an ASUS laptop with the **ASUS System Control Interface** driver. These were only checked on the FA506IU; other models may not expose the battery limit.

## Build

```
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

Then download a Windows release of RyzenAdj from <https://github.com/FlyGoat/RyzenAdj/releases> and extract **all** of its contents into a `ryzenadj` folder next to `TufDood.exe`:

```
publish\
  TufDood.exe
  ryzenadj\
    ryzenadj.exe
    (the rest of the files from the RyzenAdj zip)
```

Notes:

- Windows Defender may quarantine `WinRing0x64.sys` (the driver RyzenAdj uses). If TufDood reports an error, check that the file is still there.
- On the FA506IU, RyzenAdj exits with `0xC0000005` after applying the limit and prints nothing. TufDood treats that case as applied. To verify, run `.\ryzenadj.exe -i` in a console and look at **THM LIMIT CORE**.
- The ASUS mode cannot be read back on the FA506IU (the driver always answers the same value), so TufDood remembers the last mode it applied.
- Coming from a version called ATL: TufDood imports its settings the first time and replaces the old "ATL" startup task.

## Credits

Made by KyaaaDood with Claude AI. 🤖✨

Follow my work on [GitHub](https://github.com/kyaaadood) and, if you want to support the project, you can do it on [Patreon](https://www.patreon.com/cw/KyaaaDood).

TufDood uses [RyzenAdj](https://github.com/FlyGoat/RyzenAdj) (LGPL-3.0, not included). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
