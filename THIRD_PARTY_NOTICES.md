# Third-party notices / Avisos de terceros

TufDood itself is licensed under the **GPL-3.0** (see [LICENSE](LICENSE)).
TufDood does not include RyzenAdj in its source repository; the user downloads it separately.
If a release bundles it, it is shipped unmodified, in its own `ryzenadj` folder, together with its own license files.

TufDood no incluye RyzenAdj en su repositorio de código fuente; el usuario lo descarga por separado.
Si un release lo incluye, va sin modificar, en su propia carpeta `ryzenadj` y junto con sus archivos de licencia.

| Component | Use | License |
|---|---|---|
| [RyzenAdj](https://github.com/FlyGoat/RyzenAdj) by FlyGoat | Executed as an external program (`ryzenadj\ryzenadj.exe`) to set the CPU temperature limit. Source code: see the link, use the tag of the version you downloaded. | LGPL-3.0 |
| WinRing0 (`WinRing0x64.sys`, part of the RyzenAdj Windows zip) | Driver used by RyzenAdj. Antivirus software may flag it. | See the RyzenAdj package |
| [System.Management](https://www.nuget.org/packages/System.Management) (NuGet, Microsoft) | Listens to the ASUS key event (WMI) | MIT |
| [.NET 8 / Windows Forms](https://github.com/dotnet/runtime) (Microsoft) | Runtime and UI | MIT |

## Acknowledgements / Agradecimientos

- [G-Helper](https://github.com/seerge/g-helper) (GPL-3.0) was the inspiration for the project. The ASUS ACPI interface used for the power mode and battery limit (device IDs and call format) follows what G-Helper and the Linux `asus-wmi` driver use; some of that code is structurally similar to G-Helper's `AsusACPI` class, which is why TufDood is released under the same license (GPL-3.0).
  G-Helper fue la inspiración del proyecto. La interfaz ACPI de ASUS (IDs de dispositivo y formato de llamada) sigue lo que usan G-Helper y el driver `asus-wmi` de Linux; parte de ese código se parece estructuralmente a la clase `AsusACPI` de G-Helper, por eso TufDood se publica con la misma licencia (GPL-3.0).

*ASUS* and *TUF* are trademarks of ASUSTeK Computer Inc. TufDood is not affiliated with ASUS.

Check each project's own license for its exact terms.
