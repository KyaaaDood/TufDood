<p align="center">
  <a href="README.md">English</a> · <b>Español</b>
</p>

<p align="center">
  <img src="icons/tufdood.png" width="128" alt="TufDood">
</p>

# 🌡️ TufDood — doma tu laptop ASUS TUF desde la bandeja

> **Versión 0.1** · 🌍 Sigue el idioma de Windows (español / inglés)

TufDood es una pequeña **app de bandeja para Windows** 🖥️ que mantiene tu laptop ASUS TUF con **Ryzen** más fresca y silenciosa: **limita la temperatura del CPU**, **cambia el modo de energía** y **limita la carga de la batería**, todo desde una ventanita oscura, **sin scripts ni archivos de configuración**. Inspirada en [G-Helper](https://github.com/seerge/g-helper).

🪶 Ligera: vive en la bandeja y solo habla con RyzenAdj o con el driver de ASUS cuando tiene algo que aplicar.
🧩 Hecha con C# (.NET 8, Windows Forms) + [RyzenAdj](https://github.com/FlyGoat/RyzenAdj) para el límite de temperatura.

> ⚠️ **Solo se ha probado en una ASUS TUF A15 FA506IU** (Ryzen 5 4600H · GTX 1660 Ti). *Probablemente* funcione en modelos parecidos (mira [¿En qué laptops debería funcionar?](#-en-qué-laptops-debería-funcionar)), pero nadie lo ha confirmado todavía. Úsalo bajo tu propio riesgo: TufDood modifica límites de potencia y temperatura del CPU y la carga de la batería mediante RyzenAdj y la interfaz del firmware ASUS.

## ✨ Qué hace

- 🌡️ **Límite de temperatura del CPU** (50–95 °C) con RyzenAdj `--tctl-temp`. Por defecto: 80 °C.
- 🔁 **Re-aplica el límite solo** (intervalo configurable, 1 hora por defecto) y justo después de los eventos que suelen reiniciarlo: 😴 reanudar de suspensión · 🔌 conectar o desconectar el cargador · 🔋 cambiar el plan de energía de Windows · ⚡ cambiar el modo de energía.
- 🚦 **Modo de energía** Silencioso · Equilibrado · Turbo, como **Fn+F5** en G-Helper. TufDood escucha el evento de la tecla ASUS y además ofrece **Ctrl+Alt+F5** como atajo global.
- 🔋 **Plan de energía de Windows** desde el menú de la bandeja.
- 🛡️ **Límite de carga de la batería** (60 / 70 / 80 / 90 / 100 %) para frenar el desgaste si casi siempre tienes la laptop conectada.
- 💬 **Aviso en pantalla (OSD)** cuando cambia el modo, el plan o el límite de carga.
- 🚀 **Iniciar con Windows**: tarea programada con permisos de administrador **sin pedir UAC**, también en batería.
- 🌙 **Interfaz oscura**, los textos siguen el idioma de Windows, menú de clic derecho en la ventana y en el ícono de la bandeja.
- 📜 **Registro** en `%AppData%\TufDood\tufdood.log` (clic derecho → *Ver registro*) para ver qué pasó realmente.

## 💻 ¿En qué laptops debería funcionar?

TufDood tiene **dos partes independientes**, y cada una tiene sus propios requisitos:

| Parte | Qué necesita | Funciona en |
|---|---|---|
| 🌡️ **Límite de temperatura** | Un APU **Ryzen Mobile** compatible con RyzenAdj | Laptops de **cualquier marca**, no solo ASUS |
| 🚦 **Modo de energía · Fn+F5 · 🔋 límite de carga** | El driver **ASUS System Control Interface** (`ATKACPI`) | Solo laptops ASUS |

Si una parte no está disponible, TufDood muestra *"No disponible en este equipo"* en esa función y todo lo demás sigue funcionando.

**Leyenda:** ✅ probado · 🟢 muy probable · 🟡 probable · 🟠 incierto · 🔴 no funcionará

> 🧪 Esta lista es **una estimación** basada en la generación del CPU y en que ASUS reutiliza la misma interfaz entre modelos. **No** es una lista de laptops probadas. Las terminaciones de los modelos cambian según la región y el año: fíjate en tu CPU, no solo en el nombre.

### 🎯 ASUS TUF Gaming A15 / A17 (AMD)

| Serie | Modelos | Generación del CPU | Probabilidad |
|---|---|---|---|
| **A15 (2020–21)** | **FA506IU** | Ryzen 4000H · Renoir (Zen 2) | ✅ **Probado** |
| | FA506IH · FA506II · FA506IV · FA506IC · FA506IE · FA506IHR | Ryzen 4000H · Renoir (Zen 2) | 🟢 Muy probable (misma plataforma que la FA506IU) |
| **A17 (2020–21)** | FA706IU · FA706IH · FA706II · FA706IC · FA706IE · FA706IHR | Ryzen 4000H · Renoir (Zen 2) | 🟢 Muy probable |
| **A15 / A17 (2021)** | FA506Q* · FA706Q* (p. ej. FA506QM, FA706QR) | Ryzen 5000H · Cezanne (Zen 3) | 🟡 Probable |
| **A15 / A17 (2022)** | FA507R* · FA707R* | Ryzen 6000H · Rembrandt (Zen 3+) | 🟡 Probable |
| **A15 / A16 / A17 (2023)** | FA506N* · FA507N* · FA707N* · FA617N* | Ryzen 7035HS · Rembrandt-R (Zen 3+) | 🟡 Probable |
| **A15 / A16 / A17 (2023–24)** | FA507X* · FA507U* · FA707X* · FA617X* | Ryzen 7040HS / 8040HS · Phoenix / Hawk Point (Zen 4) | 🟠 Depende de la versión de RyzenAdj |
| **A14 (2024)** | FA401U* | Ryzen 8040HS · Hawk Point | 🟠 Depende de la versión de RyzenAdj |
| **A14 (2024–25)** | FA401W* y más nuevas | Ryzen AI · Strix Point | 🔴 No soportadas por RyzenAdj v0.19.0 |

### 🧭 Otras laptops ASUS con CPU Ryzen Mobile

| Familia | Probabilidad |
|---|---|
| **ROG Zephyrus G14 / G15 / Strix G15 / Flow** de la época Ryzen 4000H–6000H (p. ej. GA401, GA502, GA503, G513) | 🟡 El **límite de temperatura** debería funcionar; el modo y el límite de carga *pueden* funcionar (G-Helper los soporta). |
| **Vivobook / Zenbook / ProArt** con Ryzen Mobile | 🟠 El **límite de temperatura** debería funcionar; las funciones ASUS dependen del modelo y del driver. |

### 🔴 No funcionará

- Laptops **Intel**, incluyendo todas las TUF Intel (**FX / F15 / F17 / Dash**): RyzenAdj solo habla con AMD.
- CPUs Ryzen de **escritorio** y chips **"HX" Dragon Range**: RyzenAdj no los cubre.
- Chips **más nuevos que tu versión de RyzenAdj**. Por ejemplo, con RyzenAdj v0.19.0 un **Ryzen 7 7445HS** falla con *"Fam19h: unsupported model"* y TufDood no puede evitarlo. Revisa los [procesadores soportados](https://github.com/FlyGoat/RyzenAdj#supported-processors) de RyzenAdj y actualízalo cuando una versión nueva agregue el tuyo.

> 💡 **¿Te funcionó (o no) en tu laptop?** Abre un [issue](../../issues) con tu modelo, tu CPU y qué funcionó, y se agregará a esta lista. 🙌

## 🚀 Instalación

No necesita instalador. Windows 10 / 11 x64.

1. 📦 Instala el [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (no hace falta si publicas autocontenido).
2. 🛠️ Compila TufDood con **`Build.bat`** (abajo) o descarga el `.exe` de un release si hay alguno.
3. ⬇️ Descarga una versión de **RyzenAdj** para Windows desde <https://github.com/FlyGoat/RyzenAdj/releases> y extrae **todo** su contenido en una carpeta `ryzenadj` junto a `TufDood.exe`:

```
TufDood.exe
ryzenadj\
  ryzenadj.exe
  (el resto de los archivos del zip de RyzenAdj)
```

4. 🖱️ Abre `TufDood.exe`. Te pedirá permisos de administrador (RyzenAdj los necesita).
5. 🚀 *(Opcional)* Activa **Iniciar con Windows** en la ventana o en el menú de la bandeja.

Para el **modo de energía, Fn+F5 y el límite de carga** necesitas además el driver **ASUS System Control Interface** (Armoury Crate lo instala; también puedes bajarlo solo desde el soporte de ASUS).

## 🏗️ Compilar

Lo más fácil: doble clic en **`Build.bat`**. Limpia compilaciones anteriores, corre `dotnet publish` y te avisa si falta `ryzenadj\ryzenadj.exe`. Necesitas el **SDK de .NET 8**.

O a mano:

```
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

El resultado es `publish\TufDood.exe`. Si existe una carpeta `ryzenadj` junto al proyecto, se copia a la salida en cada compilación.

<details>
<summary>⚡ <b>Arranque más rápido (ReadyToRun)</b></summary>

El proyecto se precompila con ReadyToRun, así la ventana abre más rápido. Solo aplica con `dotnet publish -r win-x64`. Si la compilación fallara (por ejemplo, sin internet para descargar el paquete crossgen), borra la línea `PublishReadyToRun` de `TufDood.csproj`: todo funciona igual, solo que la primera apertura es más lenta.
</details>

## 🪟 Cómo se usa

**Doble clic en el ícono de la bandeja** (o abre la app) para ver la ventana. **Clic derecho** en la ventana o en el ícono para el menú: *Abrir TufDood*, *Temperatura objetivo*, *Aplicar ahora*, *Re-aplicar automáticamente*, *Intervalo*, *Modo de energía* (con *Siguiente modo · Fn+F5*), *Límite de carga de la batería*, *Plan de energía de Windows*, *Atajo Ctrl+Alt+F5*, *Iniciar con Windows*, *Ver registro*, *Acerca de* y *Salir*.

| Ajuste | Qué hace | Por defecto |
|---|---|---|
| 🌡️ Temperatura objetivo | Límite del CPU en °C (50–95) | `80` |
| 🔁 Re-aplicar automáticamente | Vuelve a aplicar el límite cada cierto tiempo | Activado |
| ⏱️ Intervalo | Segundos entre cada re-aplicación | `3600` (1 hora) |
| 🚀 Iniciar con Windows | Tarea programada, administrador, sin UAC | Desactivado |
| ⌨️ Atajo | **Ctrl+Alt+F5** global cambia el modo de energía | Activado |
| 🔋 Límite de carga | 60 / 70 / 80 / 90 / 100 % | *Sin elegir* hasta que escojas uno |

<details>
<summary>📋 <b>Archivo de configuración (<code>settings.json</code>)</b></summary>

Normalmente **no lo necesitas**: todo se cambia desde la ventana. Está en `%AppData%\TufDood\settings.json`.

| Clave | Qué hace | Por defecto |
|---|---|---|
| `TargetTemp` | Límite de temperatura del CPU en °C | `80` |
| `AutoApply` | Re-aplicar el límite automáticamente | `true` |
| `IntervalSeconds` | Segundos entre re-aplicaciones | `3600` |
| `AutoStart` | Iniciar con Windows | `false` |
| `Hotkey` | Atajo global Ctrl+Alt+F5 | `true` |
| `LastMode` | Último modo de energía que aplicó TufDood (la laptop no permite consultarlo) | `-1` |
| `BatteryLimit` | Límite de carga elegido en %, `-1` = nunca se ha tocado | `-1` |
</details>

## 🧰 Es bueno saber

- 🛡️ Windows Defender puede **poner en cuarentena `WinRing0x64.sys`** (el driver que usa RyzenAdj). Si TufDood muestra un error, revisa que el archivo siga en la carpeta `ryzenadj`.
- 💥 En la FA506IU, RyzenAdj termina con `0xC0000005` después de aplicar el límite y no imprime nada. TufDood lo toma como **aplicado**. Para verificarlo, corre `.\ryzenadj.exe -i` en una consola y mira **THM LIMIT CORE**.
- 🔒 El modo de energía ASUS **no se puede leer de vuelta** en la FA506IU (el driver contesta siempre el mismo valor), por eso TufDood recuerda el último modo que aplicó. Si cambias el modo con otra herramienta, TufDood no se entera.
- 🔋 No todas las laptops exponen el límite de batería. Si no está, la ventana dice *No disponible*.
- 🔌 ¿Casi siempre tienes la laptop conectada? Un límite de **80 %** es buena opción.
- 🔁 **¿Vienes de ATL?** 🏷️ TufDood es el nuevo nombre. La primera vez que abre importa los ajustes anteriores (`%AppData%\ATL`) y reemplaza la tarea de inicio "ATL".
- 🧪 Cambiar los límites de potencia puede hacer que tu laptop se caliente más, haga más ruido o se vuelva inestable si te pasas. Usa valores razonables.

## 🆘 Solución de problemas

<details>
<summary>Abrir la lista</summary>

- **"RyzenAdj falló (0xC0000005)":** revisa que la carpeta `ryzenadj` esté completa (todo el contenido del zip) y que Defender no haya puesto en cuarentena `WinRing0x64.sys`. Los detalles están en el registro.
- **"Falta una DLL de RyzenAdj":** copia **todo** el contenido del zip de RyzenAdj, no solo `ryzenadj.exe`.
- **`Fam19h: unsupported model` (o parecido):** tu CPU es más nuevo que tu versión de RyzenAdj. Prueba con una versión más reciente; si todavía no existe, TufDood no puede hacer nada.
- **"No disponible en este equipo" en modo / límite de carga:** el driver ASUS System Control Interface no está instalado, o tu modelo no ofrece esa función.
- **"No se pudo cambiar el modo":** igual que arriba: instala o repara el driver **ASUS System Control Interface**.
- **El límite parece desaparecer:** el firmware lo reinicia al suspender o al cambiar la alimentación. TufDood lo re-aplica en esos eventos; revisa también que *Re-aplicar automáticamente* esté activado.
- **Ver el registro:** clic derecho → *Ver registro* (`%AppData%\TufDood\tufdood.log`).
</details>

## 📄 Licencia

TufDood es software libre bajo la **[GNU GPL v3.0](LICENSE)**. Puedes usarlo, estudiarlo, modificarlo y compartirlo; si lo distribuyes (o distribuyes una versión modificada), debe mantenerse con la misma licencia e ir acompañado de su código fuente.

> ℹ️ TufDood es un proyecto independiente y **no está afiliado, respaldado ni patrocinado por ASUS**. *ASUS* y *TUF* son marcas de ASUSTeK Computer Inc., usadas aquí solo para describir la compatibilidad.

## 🙏 Agradecimientos

- [G-Helper](https://github.com/seerge/g-helper) (GPL-3.0) fue la inspiración del proyecto. La interfaz ACPI de ASUS que se usa para el modo de energía y el límite de batería sigue lo que usan G-Helper y el driver `asus-wmi` de Linux.
- [RyzenAdj](https://github.com/FlyGoat/RyzenAdj) de FlyGoat hace el trabajo de verdad con el límite de temperatura. 💛

## 💛 Créditos

Hecho por **KyaaaDood** con Claude AI. 🤖✨

Sigue mi trabajo en [GitHub](https://github.com/kyaaadood) y, si quieres apoyar el proyecto, puedes hacerlo en [Patreon](https://www.patreon.com/cw/KyaaaDood). ☕

TufDood usa [RyzenAdj](https://github.com/FlyGoat/RyzenAdj) (LGPL-3.0, no incluido); consulta [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
