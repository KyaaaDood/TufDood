@echo off
setlocal
cd /d "%~dp0"
title TufDood - Build

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] No se encontro dotnet. Instala el SDK de .NET 8:
    echo         https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo.
echo === Limpiando compilaciones anteriores ===
if exist publish rmdir /s /q publish
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo.
echo === Compilando TufDood ===
dotnet publish TufDood.csproj -c Release -r win-x64 --self-contained false -o publish
if errorlevel 1 (
    echo.
    echo [ERROR] La compilacion fallo. Revisa los mensajes de arriba.
    pause
    exit /b 1
)

echo.
if exist "publish\ryzenadj\ryzenadj.exe" (
    echo RyzenAdj incluido.
) else (
    echo [AVISO] Falta publish\ryzenadj\ryzenadj.exe
    echo         Descarga RyzenAdj y extrae TODO su contenido en una carpeta "ryzenadj"
    echo         junto a este script ^(se copia sola en cada build^) o dentro de publish.
)

echo.
echo === Listo: publish\TufDood.exe ===
pause
