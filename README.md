<p align="center">
  <b>English</b> · <a href="README.es.md">Español</a>
</p>

<p align="center">
  <img src="icons/tufdood.png" width="128" alt="TufDood">
</p>

# 🌡️ TufDood — tame your ASUS TUF laptop from the tray

> **Version 0.1** · 🌍 Follows the Windows language (English / Spanish)

TufDood is a small **Windows tray app** 🖥️ that keeps your **Ryzen** ASUS TUF laptop cooler and quieter: it **caps the CPU temperature**, **switches the power mode** and **limits the battery charge** — all from one dark little window, **no scripts and no config files**. Inspired by [G-Helper](https://github.com/seerge/g-helper).

🪶 Light: it sits in the tray and only talks to RyzenAdj / the ASUS driver when it has something to apply.
🧩 Built with C# (.NET 8, Windows Forms) + [RyzenAdj](https://github.com/FlyGoat/RyzenAdj) for the temperature limit.

> ⚠️ **Only tested on an ASUS TUF A15 FA506IU** (Ryzen 5 4600H · GTX 1660 Ti). It *probably* works on similar models — see [Which laptops should work?](#-which-laptops-should-work) — but nobody has confirmed it yet. Use it at your own risk: TufDood changes CPU power/thermal limits and battery charging through RyzenAdj and the ASUS firmware interface.

## ✨ What it does

- 🌡️ **CPU temperature cap** (50–95 °C) with RyzenAdj `--tctl-temp`. Default: 80 °C.
- 🔁 **Re-applies the limit by itself** (configurable interval, 1 hour by default) and right after the events that usually reset it: 😴 resume from sleep · 🔌 plugging / unplugging the charger · 🔋 changing the Windows power plan · ⚡ changing the power mode.
- 🚦 **Power mode** Silent · Balanced · Turbo, like **Fn+F5** in G-Helper. TufDood listens to the ASUS key event, and also offers **Ctrl+Alt+F5** as a global hotkey.
- 🔋 **Windows power plan** picker from the tray menu.
- 🛡️ **Battery charge limit** (60 / 70 / 80 / 90 / 100 %) to slow down battery wear when you keep the laptop plugged in.
- 💬 **On-screen message (OSD)** when the mode, plan or charge limit changes.
- 🚀 **Start with Windows**: a scheduled task that runs as administrator **without a UAC prompt**, also on battery.
- 🌙 **Dark interface**, texts follow the Windows language, right-click menu on both the window and the tray icon.
- 📜 **Log** at `%AppData%\TufDood\tufdood.log` (right-click → *View log*) to see what really happened.

## 💻 Which laptops should work?

TufDood has **two independent parts**, and each one has its own requirements:

| Part | What it needs | Works on |
|---|---|---|
| 🌡️ **Temperature cap** | A **Ryzen Mobile** APU supported by RyzenAdj | **Any brand** of laptop, not only ASUS |
| 🚦 **Power mode · Fn+F5 · 🔋 charge limit** | The **ASUS System Control Interface** driver (`ATKACPI`) | ASUS laptops only |

If one part isn't available, TufDood says *"Not available on this PC"* for that feature and the rest keeps working.

**Legend:** ✅ tested · 🟢 very likely · 🟡 probable · 🟠 uncertain · 🔴 will not work

> 🧪 This list is **an educated guess** based on the CPU generation and on the fact that ASUS reuses the same interface across models. It is **not** a list of tested laptops. Model suffixes vary by region and year: check your CPU, not only the name.

### 🎯 ASUS TUF Gaming A15 / A17 (AMD)

| Series | Models | CPU generation | Chance |
|---|---|---|---|
| **A15 (2020–21)** | **FA506IU** | Ryzen 4000H · Renoir (Zen 2) | ✅ **Tested** |
| | FA506IH · FA506II · FA506IV · FA506IC · FA506IE · FA506IHR | Ryzen 4000H · Renoir (Zen 2) | 🟢 Very likely (same platform as the FA506IU) |
| **A17 (2020–21)** | FA706IU · FA706IH · FA706II · FA706IC · FA706IE · FA706IHR | Ryzen 4000H · Renoir (Zen 2) | 🟢 Very likely |
| **A15 / A17 (2021)** | FA506Q* · FA706Q* (e.g. FA506QM, FA706QR) | Ryzen 5000H · Cezanne (Zen 3) | 🟡 Probable |
| **A15 / A17 (2022)** | FA507R* · FA707R* | Ryzen 6000H · Rembrandt (Zen 3+) | 🟡 Probable |
| **A15 / A16 / A17 (2023)** | FA506N* · FA507N* · FA707N* · FA617N* | Ryzen 7035HS · Rembrandt-R (Zen 3+) | 🟡 Probable |
| **A15 / A16 / A17 (2023–24)** | FA507X* · FA507U* · FA707X* · FA617X* | Ryzen 7040HS / 8040HS · Phoenix / Hawk Point (Zen 4) | 🟠 Depends on the RyzenAdj version |
| **A14 (2024)** | FA401U* | Ryzen 8040HS · Hawk Point | 🟠 Depends on the RyzenAdj version |
| **A14 (2024–25)** | FA401W* and newer | Ryzen AI · Strix Point | 🔴 Not supported by RyzenAdj v0.19.0 |

### 🧭 Other ASUS laptops with a Ryzen Mobile CPU

| Family | Chance |
|---|---|
| **ROG Zephyrus G14 / G15 / Strix G15 / Flow** from the Ryzen 4000H–6000H era (e.g. GA401, GA502, GA503, G513) | 🟡 The **temperature cap** should work; mode and charge limit *may* work (G-Helper supports them). |
| **Vivobook / Zenbook / ProArt** with Ryzen Mobile | 🟠 The **temperature cap** should work; the ASUS features depend on the model and on the driver. |

### 🔴 Won't work

- **Intel** laptops, including every Intel TUF (**FX / F15 / F17 / Dash**): RyzenAdj only talks to AMD.
- **Desktop** Ryzen CPUs and **Dragon Range "HX"** chips: not covered by RyzenAdj.
- Chips **newer than your RyzenAdj version**. For example, with RyzenAdj v0.19.0 a **Ryzen 7 7445HS** fails with *"Fam19h: unsupported model"*, and TufDood cannot work around that. Check RyzenAdj's [supported processors](https://github.com/FlyGoat/RyzenAdj#supported-processors) and update it when a new release adds yours.

> 💡 **Did it work (or not) on your laptop?** Open an [issue](../../issues) with your model, CPU and what worked, and it will be added to this list. 🙌

## 🚀 Install

No installer needed. Windows 10 / 11 x64.

1. 📦 Install the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (not needed if you publish self-contained).
2. 🛠️ Build TufDood with **`Build.bat`** (see below) or download a release `.exe` if one is available.
3. ⬇️ Download a Windows release of **RyzenAdj** from <https://github.com/FlyGoat/RyzenAdj/releases> and extract **all** of its contents into a `ryzenadj` folder next to `TufDood.exe`:

```
TufDood.exe
ryzenadj\
  ryzenadj.exe
  (the rest of the files from the RyzenAdj zip)
```

4. 🖱️ Open `TufDood.exe`. It asks for administrator rights (RyzenAdj needs them).
5. 🚀 *(Optional)* Turn on **Start with Windows** in the window or the tray menu.

For the **power mode, Fn+F5 and charge limit** you also need the **ASUS System Control Interface** driver (Armoury Crate installs it; you can also get it alone from ASUS support).

## 🏗️ Build

Easiest way: double-click **`Build.bat`**. It cleans old builds, runs `dotnet publish` and tells you if `ryzenadj\ryzenadj.exe` is missing. You need the **.NET 8 SDK**.

Or by hand:

```
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

The result is `publish\TufDood.exe`. If a `ryzenadj` folder exists next to the project, it is copied to the output on every build.

<details>
<summary>⚡ <b>Faster start (ReadyToRun)</b></summary>

The project is pre-compiled with ReadyToRun, so the window opens sooner. It only applies with `dotnet publish -r win-x64`. If the build fails (for example, no internet to download the crossgen package), delete the `PublishReadyToRun` line in `TufDood.csproj`: everything works the same, only the first open is slower.
</details>

## 🪟 Using it

**Double-click the tray icon** (or run the app) to open the window. **Right-click** the window or the icon for the menu: *Open TufDood*, *Target temperature*, *Apply now*, *Re-apply automatically*, *Interval*, *Power mode* (with *Next mode · Fn+F5*), *Battery charge limit*, *Windows power plan*, *Ctrl+Alt+F5 hotkey*, *Start with Windows*, *View log*, *About* and *Exit*.

| Setting | What it does | Default |
|---|---|---|
| 🌡️ Temperature target | CPU limit in °C (50–95) | `80` |
| 🔁 Auto-apply | Re-applies the limit every so often | On |
| ⏱️ Interval | Seconds between re-applies | `3600` (1 hour) |
| 🚀 Start with Windows | Scheduled task, admin, no UAC prompt | Off |
| ⌨️ Hotkey | Global **Ctrl+Alt+F5** cycles the power mode | On |
| 🔋 Charge limit | 60 / 70 / 80 / 90 / 100 % | *Not set* until you pick one |

<details>
<summary>📋 <b>Settings file (<code>settings.json</code>)</b></summary>

You normally **don't need it**: everything is changed from the window. It lives in `%AppData%\TufDood\settings.json`.

| Key | What it does | Default |
|---|---|---|
| `TargetTemp` | CPU temperature cap in °C | `80` |
| `AutoApply` | Re-apply the limit automatically | `true` |
| `IntervalSeconds` | Seconds between re-applies | `3600` |
| `AutoStart` | Start with Windows | `false` |
| `Hotkey` | Global Ctrl+Alt+F5 hotkey | `true` |
| `LastMode` | Last power mode TufDood applied (the laptop can't be asked) | `-1` |
| `BatteryLimit` | Chosen charge limit in %, `-1` = never touched | `-1` |
</details>

## 🧰 Good to know

- 🛡️ Windows Defender may **quarantine `WinRing0x64.sys`** (the driver RyzenAdj uses). If TufDood reports an error, check that the file is still in the `ryzenadj` folder.
- 💥 On the FA506IU, RyzenAdj exits with `0xC0000005` after applying the limit and prints nothing. TufDood treats that case as **applied**. To verify it, run `.\ryzenadj.exe -i` in a console and look at **THM LIMIT CORE**.
- 🔒 The ASUS power mode **can't be read back** on the FA506IU (the driver always answers the same value), so TufDood remembers the last mode it applied. If you change the mode with another tool, TufDood won't notice.
- 🔋 Not every laptop exposes the battery limit. If it's missing, the window says *Not available*.
- 🔌 Keep your laptop plugged in most of the time? An **80 %** limit is a good choice.
- 🔁 **Coming from ATL?** 🏷️ TufDood is the new name. The first time it opens it imports the old settings (`%AppData%\ATL`) and replaces the old "ATL" startup task.
- 🧪 Changing power limits can make your laptop hotter, noisier or unstable if you push it. Keep reasonable values.

## 🆘 Troubleshooting

<details>
<summary>Open the list</summary>

- **"RyzenAdj failed (0xC0000005)":** make sure the `ryzenadj` folder is complete (all the zip contents) and that Defender didn't quarantine `WinRing0x64.sys`. Details are in the log.
- **"A RyzenAdj DLL is missing":** copy the **whole** contents of the RyzenAdj zip, not just `ryzenadj.exe`.
- **`Fam19h: unsupported model` (or similar):** your CPU is newer than your RyzenAdj version. Try a newer RyzenAdj release; if there isn't one yet, TufDood can't do anything about it.
- **"Not available on this PC" for mode / charge limit:** the ASUS System Control Interface driver isn't installed, or your model doesn't expose that feature.
- **"Couldn't change the mode":** same as above — install or repair the **ASUS System Control Interface** driver.
- **The limit seems to disappear:** the firmware resets it after sleep or a power change. TufDood re-applies it on those events; also check that *Auto-apply* is on.
- **See the log:** right-click → *View log* (`%AppData%\TufDood\tufdood.log`).
</details>

## 📄 License

TufDood is free software under the **[GNU GPL v3.0](LICENSE)**. You can use, study, modify and share it; if you distribute it (or a modified version), it must stay under the same license and come with its source code.

> ℹ️ TufDood is an independent project and is **not affiliated with, endorsed by or sponsored by ASUS**. *ASUS* and *TUF* are trademarks of ASUSTeK Computer Inc., used here only to describe compatibility.

## 🙏 Acknowledgements

- [G-Helper](https://github.com/seerge/g-helper) (GPL-3.0) was the inspiration for the project. The ASUS ACPI interface used for the power mode and the battery limit follows what G-Helper and the Linux `asus-wmi` driver use.
- [RyzenAdj](https://github.com/FlyGoat/RyzenAdj) by FlyGoat does the real work of the temperature limit. 💛

## 💛 Credits

Made by **KyaaaDood** with Claude AI. 🤖✨

Follow my work on [GitHub](https://github.com/kyaaadood) and, if you want to support the project, you can do it on [Patreon](https://www.patreon.com/cw/KyaaaDood). ☕

TufDood uses [RyzenAdj](https://github.com/FlyGoat/RyzenAdj) (LGPL-3.0, not included); see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
