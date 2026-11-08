# TufDood — App de bandeja para domar una laptop ASUS TUF con CPU Ryzen 🌡️

**Versión 0.1** · [Read in English](README.md)

TufDood es una pequeña app de bandeja para Windows, inspirada en [G-Helper](https://github.com/seerge/g-helper), que te permite:

- **Limitar la temperatura del CPU** (50–95 °C) con RyzenAdj `--tctl-temp`. El límite se re-aplica automáticamente (intervalo configurable) y tras eventos que suelen reiniciarlo: reanudar de suspensión, conectar o desconectar el cargador, cambiar el plan de energía de Windows y cambiar el modo.
- **Cambiar el modo de energía** Silencioso / Equilibrado / Turbo, como Fn+F5 en G-Helper. TufDood escucha el evento de la tecla ASUS y además ofrece **Ctrl+Alt+F5** como atajo global.
- **Elegir el plan de energía de Windows** desde el menú de la bandeja.
- **Poner un límite de carga a la batería** (60 / 70 / 80 / 90 / 100 %).
- Ver un **aviso en pantalla** cuando cambia el modo, el plan o el límite de carga.
- **Iniciar con Windows** (tarea programada con permisos de administrador sin pedir UAC, también en batería).

Interfaz oscura, los textos siguen el idioma de Windows (inglés o español), menú de clic derecho en la ventana y en el ícono de la bandeja. Registro en `%AppData%\TufDood\tufdood.log` (clic derecho → *Ver registro*) y configuración en `%AppData%\TufDood\settings.json`.

> Solo se ha probado en una **ASUS TUF A15 FA506IU** (Ryzen 5 4600H). Úsalo bajo tu propio riesgo: TufDood modifica límites de potencia y temperatura del CPU y la carga de la batería mediante RyzenAdj y la interfaz del firmware ASUS.

## Requisitos

- Windows 10 / 11 x64 y el [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (o publicar autocontenido).
- Un APU Ryzen **Mobile** compatible con [RyzenAdj](https://github.com/FlyGoat/RyzenAdj). Los chips más nuevos no están soportados por RyzenAdj v0.19.0 (por ejemplo, un Ryzen 7 7445HS falla con *"Fam19h: unsupported model"*); TufDood no puede evitarlo.
- Para el modo, Fn+F5 y el límite de carga: una laptop ASUS con el driver **ASUS System Control Interface**. Solo se comprobaron en la FA506IU; otros modelos pueden no exponer el límite de batería.

## Compilar

```
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

Después descarga una versión de RyzenAdj para Windows desde <https://github.com/FlyGoat/RyzenAdj/releases> y extrae **todo** su contenido en una carpeta `ryzenadj` junto a `TufDood.exe`:

```
publish\
  TufDood.exe
  ryzenadj\
    ryzenadj.exe
    (el resto de los archivos del zip de RyzenAdj)
```

Notas:

- Windows Defender puede poner en cuarentena `WinRing0x64.sys` (el driver que usa RyzenAdj). Si TufDood muestra un error, revisa que el archivo siga ahí.
- En la FA506IU, RyzenAdj termina con `0xC0000005` después de aplicar el límite y no imprime nada. TufDood lo toma como aplicado. Para verificarlo, corre `.\ryzenadj.exe -i` en una consola y mira **THM LIMIT CORE**.
- El modo ASUS no se puede leer de vuelta en la FA506IU (el driver contesta siempre el mismo valor), por eso TufDood recuerda el último modo que aplicó.
- Si vienes de una versión llamada ATL: TufDood importa sus ajustes la primera vez y reemplaza la tarea de inicio "ATL".

## Créditos

Hecho por KyaaaDood con Claude AI. 🤖✨

Sigue mi trabajo en [GitHub](https://github.com/kyaaadood) y, si quieres apoyar el proyecto, puedes hacerlo en [Patreon](https://www.patreon.com/cw/KyaaaDood).

TufDood usa [RyzenAdj](https://github.com/FlyGoat/RyzenAdj) (LGPL-3.0, no incluido). Consulta [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
