using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;

namespace TufDood;

// ===================================================================
//  Idioma: español si Windows está en español, inglés en cualquier otro caso
// ===================================================================
static class L
{
    public static readonly bool Es =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase);

    public static string T(string es, string en) => Es ? es : en;
}

// ===================================================================
//  Datos de créditos / enlaces (edítalos aquí)
// ===================================================================
static class AppInfo
{
    public const string Name = "TufDood";
    public const string GitHubUrl = "https://github.com/kyaaadood";
    public const string PatreonUrl = "https://www.patreon.com/cw/KyaaaDood";
    public const string RyzenAdjUrl = "https://github.com/FlyGoat/RyzenAdj";

    public static string Credits =>
        L.T("Hecho por KyaaaDood con Claude AI 🤖✨", "Made by KyaaaDood with Claude AI 🤖✨");

    public static string Subtitle =>
        L.T("Límite de temperatura para CPU Ryzen", "Temperature limiter for Ryzen CPUs");

    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.1.0";

    public static Icon LoadIcon(int size)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("tufdood.ico")!;
        return new Icon(stream, new Size(size, size));
    }

    public static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }
}

// ===================================================================
//  Registro (para ver el motivo real de un error): %AppData%\TufDood\tufdood.log
// ===================================================================
static class Log
{
    static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TufDood");
    public static string FilePath => Path.Combine(Dir, "tufdood.log");

    // El registro se escribe desde un hilo propio: así ninguna llamada a Log.Write puede
    // bloquear la interfaz esperando al disco o al antivirus.
    static readonly System.Collections.Concurrent.BlockingCollection<string> queue = new();
    static readonly object startLock = new();
    static Thread? worker;
    static int pending;
    static bool dirReady;

    public static void Write(string line)
    {
        try
        {
            string text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {line}{Environment.NewLine}";
            EnsureWorker();
            Interlocked.Increment(ref pending);
            queue.Add(text);
        }
        catch { }
    }

    static void EnsureWorker()
    {
        if (worker != null) return;
        lock (startLock)
        {
            if (worker != null) return;
            var t = new Thread(Pump) { IsBackground = true, Name = "TufDood-Log" };
            t.Start();
            worker = t;
        }
    }

    static void Pump()
    {
        var sb = new StringBuilder();
        foreach (var first in queue.GetConsumingEnumerable())
        {
            sb.Clear();
            sb.Append(first);
            int n = 1;
            while (queue.TryTake(out var more)) { sb.Append(more); n++; }   // agrupa varias líneas en una sola escritura
            try
            {
                if (!dirReady) { Directory.CreateDirectory(Dir); dirReady = true; }
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > 200_000) File.WriteAllText(FilePath, "");
                File.AppendAllText(FilePath, sb.ToString());
            }
            catch { dirReady = false; }
            Interlocked.Add(ref pending, -n);
        }
    }

    /// <summary>Espera (máx. timeoutMs) a que se escriban las líneas pendientes.</summary>
    public static void Flush(int timeoutMs = 1500)
    {
        var sw = Stopwatch.StartNew();
        while (Volatile.Read(ref pending) > 0 && sw.ElapsedMilliseconds < timeoutMs) Thread.Sleep(10);
    }

    public static void Open()
    {
        Flush(500);   // que el archivo ya tenga todo lo registrado hasta ahora
        try
        {
            Directory.CreateDirectory(Dir);
            if (!File.Exists(FilePath)) File.WriteAllText(FilePath, "");
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{FilePath}\"") { UseShellExecute = true });
        }
        catch { }
    }
}

public class Settings
{
    public int TargetTemp { get; set; } = 80;
    public bool AutoApply { get; set; } = true;
    public int IntervalSeconds { get; set; } = 3600;
    public bool AutoStart { get; set; } = false;
    public bool Hotkey { get; set; } = true;   // atajo global Ctrl+Alt+F5 para ciclar el modo
    public int LastMode { get; set; } = -1;    // último modo que TufDood aplicó (el equipo no permite leerlo)
    public int BatteryLimit { get; set; } = -1; // límite de carga elegido (%), -1 = nunca se ha tocado

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TufDood", "settings.json");

    // Ajustes de las versiones anteriores, cuando el programa se llamaba ATL (se importan una sola vez)
    static string LegacyFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ATL", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            if (File.Exists(LegacyFilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(LegacyFilePath)) ?? new Settings();
        }
        catch { }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

// ===================================================================
//  RyzenAdj
// ===================================================================
record ApplyResult(bool Ok, string Msg, string Detail);

static class RyzenAdj
{
    public static string ExePath => Path.Combine(AppContext.BaseDirectory, "ryzenadj", "ryzenadj.exe");

    static readonly string[] FailWords = { "fail", "unable", "error", "not supported", "cannot", "can't" };

    // Lista los archivos de la carpeta ryzenadj (nombre y tamaño) para el registro
    static string ListFiles()
    {
        try
        {
            return string.Join(", ", Directory.GetFiles(Path.GetDirectoryName(ExePath)!)
                .Select(f => $"{Path.GetFileName(f)} ({new FileInfo(f).Length})"));
        }
        catch (Exception ex) { return "?" + ex.Message; }
    }

    public static ApplyResult SetTctlTemp(int temp)
    {
        if (!File.Exists(ExePath))
        {
            string m = L.T("No se encontró ryzenadj\\ryzenadj.exe junto a TufDood",
                           "ryzenadj\\ryzenadj.exe not found next to TufDood");
            return new ApplyResult(false, m, "missing: " + ExePath);
        }

        try
        {
            var psi = new ProcessStartInfo(ExePath, $"--tctl-temp={temp}")
            {
                WorkingDirectory = Path.GetDirectoryName(ExePath)!,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi)!;
            try { p.StandardInput.Close(); } catch { } // entrada estándar válida (cerrada) por precaución
            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();

            if (!p.WaitForExit(8000))
            {
                try { p.Kill(true); } catch { }
                return new ApplyResult(false,
                    L.T("RyzenAdj tardó demasiado en responder", "RyzenAdj took too long to respond"), "timeout");
            }
            Task.WaitAll(new Task[] { outTask, errTask }, 2000);

            string output = ((outTask.IsCompletedSuccessfully ? outTask.Result : "") + " " +
                             (errTask.IsCompletedSuccessfully ? errTask.Result : "")).Trim();
            int code = p.ExitCode;
            string codeText = $"{code} (0x{unchecked((uint)code):X8})";
            string detail = $"exit={codeText} output=\"{output}\"";

            bool saidOk = output.Contains("Successfully", StringComparison.OrdinalIgnoreCase);
            bool failed = FailWords.Any(w => output.Contains(w, StringComparison.OrdinalIgnoreCase))
                          || (code != 0 && !saidOk);

            // En este equipo RyzenAdj aplica el límite y luego se cierra con una violación de acceso
            // (0xC0000005) al terminar. Como su salida va por una tubería (con buffer), el mensaje
            // "Successfully..." se pierde. Solo en ese caso exacto (0xC0000005 y sin ninguna salida)
            // se da por aplicado; cualquier otro error se sigue mostrando.
            if (code == unchecked((int)0xC0000005) && output.Length == 0)
                return new ApplyResult(true, L.T($"Límite de {temp} °C aplicado", $"{temp} °C limit applied"),
                    detail + " (RyzenAdj terminó con 0xC0000005 sin mensaje; se asume aplicado)");

            if (!failed)
                return new ApplyResult(true, L.T($"Límite de {temp} °C aplicado", $"{temp} °C limit applied"), detail);

            detail += " files=[" + ListFiles() + "]";

            string msg;
            if (code == unchecked((int)0xC0000135))
                msg = L.T("Falta una DLL de RyzenAdj en la carpeta ryzenadj (copia todo el contenido del zip)",
                          "A RyzenAdj DLL is missing from the ryzenadj folder (copy the whole zip contents)");
            else if (code == unchecked((int)0xC0000005))
                msg = L.T("RyzenAdj falló (0xC0000005): revisa que la carpeta ryzenadj esté completa; Defender suele poner en cuarentena WinRing0x64.sys (ver registro)",
                          "RyzenAdj crashed (0xC0000005): make sure the ryzenadj folder is complete; Defender often quarantines WinRing0x64.sys (see log)");
            else
                msg = L.T("Error de RyzenAdj", "RyzenAdj error") + $" (exit {codeText})" +
                      (output.Length > 0 ? ": " + output : L.T(" — sin mensaje", " — no message"));
            return new ApplyResult(false, msg, detail);
        }
        catch (Exception ex)
        {
            return new ApplyResult(false, L.T("Error: ", "Error: ") + ex.Message, ex.ToString());
        }
    }
}

public static class AutoStartTask
{
    const string TaskName = "TufDood";
    // Nombres de versiones anteriores: se borran para que no arranquen dos copias al iniciar Windows
    static readonly string[] OldTaskNames = { "ATL", "TempLimiter" };

    // Borrar las tareas viejas solo hace falta una vez: se deja una marca y no se vuelve a llamar a schtasks
    static string MarkerPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TufDood", "oldtasks.done");

    static void DeleteOldTasks()
    {
        try { if (File.Exists(MarkerPath)) return; } catch { }
        bool allRan = true;
        foreach (var name in OldTaskNames)
            if (RunSchtasksOut($"/Delete /TN \"{name}\" /F").Code < 0) allRan = false;   // -1 = schtasks no pudo ejecutarse
        if (allRan)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
                File.WriteAllText(MarkerPath, "");
            }
            catch { }
        }
    }

    /// <summary>
    /// Para el arranque: si la tarea ya apunta a este mismo TufDood.exe no hace falta recrearla (evita varias
    /// llamadas a schtasks en cada inicio). Si no se puede comprobar o difiere, se recrea como siempre.
    /// </summary>
    public static bool EnsureCurrent()
    {
        try
        {
            string exe = Environment.ProcessPath ?? "";
            if (exe.Length > 0)
            {
                var (code, xml) = RunSchtasksOut($"/Query /TN \"{TaskName}\" /XML");
                if (code == 0
                    && xml.Contains(SecurityElement.Escape(exe), StringComparison.OrdinalIgnoreCase)
                    && xml.Contains(SecurityElement.Escape(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase)
                    && xml.Contains("--minimized", StringComparison.Ordinal))
                    return true;
            }
        }
        catch { }
        return Enable();
    }

    // Tarea programada con XML para que:
    //  - corra con privilegios de administrador sin pedir UAC
    //  - arranque también con la laptop en batería
    public static bool Enable()
    {
        DeleteOldTasks(); // limpia las tareas viejas si existen

        string exe = Environment.ProcessPath!;
        string xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>PT15S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{SecurityElement.Escape(exe)}</Command>
      <Arguments>--minimized</Arguments>
      <WorkingDirectory>{SecurityElement.Escape(AppContext.BaseDirectory)}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
        string tmp = Path.Combine(Path.GetTempPath(), "tufdood_task.xml");
        File.WriteAllText(tmp, xml, Encoding.Unicode);
        bool ok = RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{tmp}\" /F", logFail: true);
        try { File.Delete(tmp); } catch { }
        return ok;
    }

    public static bool Disable()
    {
        DeleteOldTasks();
        return RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
    }

    static bool RunSchtasks(string args, bool logFail = false) => RunSchtasksOut(args, logFail).Code == 0;

    // Devuelve el código de salida y la salida estándar; Code = -1 si schtasks no se pudo ejecutar
    static (int Code, string Out) RunSchtasksOut(string args, bool logFail = false)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi)!;
            string outp = p.StandardOutput.ReadToEnd();
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit(10000);
            if (p.ExitCode != 0 && logFail)
                Log.Write($"schtasks falló (código {p.ExitCode}): {err.Trim()} {outp.Trim()}");
            return (p.ExitCode, outp);
        }
        catch (Exception ex)
        {
            if (logFail) Log.Write("schtasks no se pudo ejecutar: " + ex.Message);
            return (-1, "");
        }
    }
}

// ===================================================================
//  Tema visual y controles personalizados
// ===================================================================
static class Theme
{
    public static readonly Color Bg = Color.FromArgb(22, 22, 27);
    public static readonly Color Card = Color.FromArgb(33, 33, 41);
    public static readonly Color CardBorder = Color.FromArgb(52, 52, 63);
    public static readonly Color Text = Color.FromArgb(240, 240, 245);
    public static readonly Color Muted = Color.FromArgb(150, 150, 164);
    public static readonly Color Track = Color.FromArgb(58, 58, 70);
    public static readonly Color Red = Color.FromArgb(255, 61, 61);
    public static readonly Color Ok = Color.FromArgb(80, 200, 120);
    public static readonly Color Hot = Color.FromArgb(255, 90, 90);

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2f);
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

// ===================================================================
//  Modo de la laptop ASUS (Silencioso / Equilibrado / Turbo), igual que Fn+F5 en G-Helper.
//  Se comunica con el driver "ASUS System Control Interface" (\\.\ATKACPI).
// ===================================================================
static class AsusMode
{
    public const int Balanced = 0, Turbo = 1, Silent = 2;

    static readonly uint[] DeviceIds = { 0x00120075, 0x00110019 };
    const uint DSTS = 0x53545344, DEVS = 0x53564544, IoctlAcpi = 0x0022240C;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(IntPtr h, uint code, byte[] inBuf, int inSize,
                                       byte[] outBuf, int outSize, out int ret, IntPtr ov);

    static readonly IntPtr Invalid = new(-1);
    static IntPtr handle = Invalid;
    static uint devId;
    static bool loggedRaw;

    public static bool Available { get; private set; }

    static bool Open()
    {
        if (handle != Invalid) return true;
        handle = CreateFile(@"\\.\ATKACPI", 0xC0000000, 3, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
        if (handle == Invalid)
        {
            Log.Write($"ATKACPI no se pudo abrir (Win32 {Marshal.GetLastWin32Error()}). ¿Está instalado ASUS System Control Interface?");
            return false;
        }
        return true;
    }

    static readonly object gate = new();

    static byte[]? Call(uint method, byte[] args)
    {
        lock (gate) return CallLocked(method, args);   // una sola llamada al driver a la vez
    }

    static byte[]? CallLocked(uint method, byte[] args)
    {
        if (!Open()) return null;
        var buf = new byte[8 + args.Length];
        BitConverter.GetBytes(method).CopyTo(buf, 0);
        BitConverter.GetBytes((uint)args.Length).CopyTo(buf, 4);
        Array.Copy(args, 0, buf, 8, args.Length);
        var output = new byte[16];
        if (!DeviceIoControl(handle, IoctlAcpi, buf, buf.Length, output, output.Length, out _, IntPtr.Zero))
        {
            Log.Write($"ATKACPI DeviceIoControl falló (Win32 {Marshal.GetLastWin32Error()})");
            return null;
        }
        return output;
    }

    static int DeviceGet(uint id)
    {
        var a = new byte[8];
        BitConverter.GetBytes(id).CopyTo(a, 0);
        var r = Call(DSTS, a);
        return r == null ? int.MinValue : BitConverter.ToInt32(r, 0) - 65536;
    }

    static int DeviceSet(uint id, uint value)
    {
        var a = new byte[8];
        BitConverter.GetBytes(id).CopyTo(a, 0);
        BitConverter.GetBytes(value).CopyTo(a, 4);
        var r = Call(DEVS, a);
        return r == null ? int.MinValue : BitConverter.ToInt32(r, 0);
    }

    /// <summary>Modo actual (0/1/2) o -1 si esta laptop no lo soporta.</summary>
    public static int Get()
    {
        int result = -1;
        foreach (var id in devId != 0 ? new[] { devId } : DeviceIds)
        {
            int v = DeviceGet(id);
            if (!loggedRaw) Log.Write($"ATKACPI DSTS 0x{id:X8} -> {v} (crudo 0x{v + 65536:X8})");
            if (result < 0 && v is >= 0 and < 256) { devId = id; result = v; }   // bit "presente" activo
        }
        loggedRaw = true;
        Available = result >= 0;
        return result;
    }

    public static bool Set(int mode)
    {
        if (devId == 0 && Get() < 0) return false;
        int r = DeviceSet(devId, (uint)mode);
        Log.Write($"ATKACPI DEVS 0x{devId:X8} = {mode} -> {r}");
        // DSTS no devuelve el modo actual en este equipo (siempre 3), así que la confirmación es la respuesta de DEVS (1 = aceptado)
        return r == 1;
    }

    // ----- Límite de carga de la batería (ASUS_WMI_DEVID_RSOC); aquí DSTS sí devuelve el porcentaje -----
    const uint BatteryId = 0x00120057;
    static bool loggedBat;

    static int DeviceGetRaw(uint id)
    {
        var a = new byte[8];
        BitConverter.GetBytes(id).CopyTo(a, 0);
        var r = Call(DSTS, a);
        return r == null ? int.MinValue : BitConverter.ToInt32(r, 0);
    }

    /// <summary>
    /// Estado del límite de carga: si el equipo lo expone (bit 0x10000 "presente") y el valor que reporta
    /// en el byte bajo (0 = el equipo no informa un porcentaje).
    /// </summary>
    public static (bool Present, int Value) GetChargeInfo()
    {
        int raw = DeviceGetRaw(BatteryId);
        bool present = raw != int.MinValue && (raw & 0x10000) != 0;
        int value = raw == int.MinValue ? 0 : raw & 0xFF;
        if (!loggedBat)
        {
            Log.Write($"ATKACPI DSTS 0x{BatteryId:X8} (límite de carga) -> crudo 0x{raw:X8}, presente={present}, valor={value}");
            loggedBat = true;
        }
        return (present, value);
    }

    /// <summary>Aplica el límite de carga. Devuelve si el driver lo aceptó y el valor que el equipo reporta después (0 = no informa).</summary>
    public static (bool Ok, int Back) SetChargeLimit(int percent)
    {
        int r = DeviceSet(BatteryId, (uint)percent);
        Thread.Sleep(150);
        int raw = DeviceGetRaw(BatteryId);
        int back = raw == int.MinValue ? 0 : raw & 0xFF;
        Log.Write($"ATKACPI DEVS 0x{BatteryId:X8} = {percent} -> {r}; lectura posterior crudo 0x{raw:X8} (valor {back})");
        return (r == 1, back);
    }

    // Mismo orden que G-Helper: Equilibrado -> Turbo -> Silencioso -> Equilibrado...
    public static int Next(int cur) => cur switch { Balanced => Turbo, Turbo => Silent, _ => Balanced };

    public static string Name(int m) => m switch
    {
        Turbo => "Turbo",
        Silent => L.T("Silencioso", "Silent"),
        Balanced => L.T("Equilibrado", "Balanced"),
        _ => $"#{m}"
    };
}

// ===================================================================
//  Planes de energía de Windows
// ===================================================================
static class WinPlans
{
    [DllImport("powrprof.dll")]
    static extern uint PowerEnumerate(IntPtr root, IntPtr sub, IntPtr schemeSub, uint access, uint index,
                                      ref Guid buffer, ref uint size);

    [DllImport("powrprof.dll")]
    static extern uint PowerReadFriendlyName(IntPtr root, ref Guid scheme, IntPtr sub, IntPtr setting,
                                             byte[]? buffer, ref uint size);

    [DllImport("powrprof.dll")]
    static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);

    [DllImport("powrprof.dll")]
    static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr h);

    public static List<(Guid Id, string Name)> List()
    {
        var list = new List<(Guid, string)>();
        for (uint i = 0; i < 64; i++)
        {
            Guid g = default;
            uint size = 16;
            if (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 16, i, ref g, ref size) != 0) break;
            list.Add((g, NameOf(g)));
        }
        return list;
    }

    static string NameOf(Guid g)
    {
        uint size = 0;
        PowerReadFriendlyName(IntPtr.Zero, ref g, IntPtr.Zero, IntPtr.Zero, null, ref size);
        if (size == 0) return g.ToString();
        var b = new byte[size];
        if (PowerReadFriendlyName(IntPtr.Zero, ref g, IntPtr.Zero, IntPtr.Zero, b, ref size) != 0) return g.ToString();
        return Encoding.Unicode.GetString(b).TrimEnd('\0');
    }

    public static Guid? Active()
    {
        if (PowerGetActiveScheme(IntPtr.Zero, out var p) != 0 || p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStructure<Guid>(p); }
        finally { LocalFree(p); }
    }

    public static bool Set(Guid g) => PowerSetActiveScheme(IntPtr.Zero, ref g) == 0;
}

static class Native
{
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // Barra de título oscura (Windows 10 2004+ / Windows 11)
    public static void DarkTitleBar(IntPtr handle)
    {
        int on = 1;
        DwmSetWindowAttribute(handle, 20, ref on, sizeof(int));
        DwmSetWindowAttribute(handle, 19, ref on, sizeof(int));
    }
}

class Card : Panel
{
    public Card()
    {
        DoubleBuffered = true;
        BackColor = Theme.Bg;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.Round(r, 14);
        using var fill = new SolidBrush(Theme.Card);
        using var pen = new Pen(Theme.CardBorder);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(pen, path);
    }
}

class GradientLine : Control
{
    public GradientLine()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Theme.Bg;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        var rect = new Rectangle(0, 0, Math.Max(Width, 1), Math.Max(Height, 1));
        using var lg = new LinearGradientBrush(rect, Color.White, Theme.Red, LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(lg, rect);
    }
}

// Número grande con degradado blanco -> rojo (más rojo mientras más alta la temperatura)
class TempDisplay : Control
{
    int _v = 80;
    readonly Font big = new("Segoe UI Semibold", 36F, FontStyle.Regular, GraphicsUnit.Point);
    readonly Font unit = new("Segoe UI", 15F, FontStyle.Regular, GraphicsUnit.Point);

    public int Value
    {
        get => _v;
        set { if (_v == value) return; _v = value; Invalidate(); }
    }

    public TempDisplay()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(BackColor);

        string num = _v.ToString();
        using var fmt = (StringFormat)StringFormat.GenericTypographic.Clone();
        fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        SizeF sz = g.MeasureString(num, big, PointF.Empty, fmt);
        float y = (Height - sz.Height) / 2f;
        float heat = (_v - 50) / 45f;
        Color end = Theme.Lerp(Color.FromArgb(255, 200, 200), Theme.Red, heat);
        var rect = new RectangleF(0, y, Math.Max(sz.Width, 1f), Math.Max(sz.Height, 1f));

        using (var br = new LinearGradientBrush(rect, Color.White, end, 90f))
            g.DrawString(num, big, br, rect.X, rect.Y, fmt);
        using (var ub = new SolidBrush(Theme.Muted))
            g.DrawString("°C", unit, ub, sz.Width + 4, y + 10, fmt);
    }
}

class GradientSlider : Control
{
    const int Pad = 14;
    int _v = 80;
    bool drag;

    public int Minimum { get; set; } = 50;
    public int Maximum { get; set; } = 95;
    public event EventHandler? ValueChanged;
    public event EventHandler? Committed; // al soltar el mouse o usar el teclado

    public int Value
    {
        get => _v;
        set
        {
            value = Math.Clamp(value, Minimum, Maximum);
            if (value == _v) return;
            _v = value;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public GradientSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        BackColor = Theme.Card;
    }

    int ValueAt(int x)
    {
        float t = Math.Clamp((x - Pad) / (float)Math.Max(Width - 2 * Pad, 1), 0f, 1f);
        return (int)Math.Round(Minimum + (Maximum - Minimum) * t);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Left)
        {
            drag = true;
            Value = ValueAt(e.X);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (drag) Value = ValueAt(e.X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (drag)
        {
            drag = false;
            Committed?.Invoke(this, EventArgs.Empty);
        }
        base.OnMouseUp(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int d = e.KeyCode switch
        {
            Keys.Left or Keys.Down => -1,
            Keys.Right or Keys.Up => 1,
            _ => 0
        };
        if (d != 0)
        {
            Value += d;
            Committed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        float cy = Height / 2f, th = 8f;
        var track = new RectangleF(Pad, cy - th / 2, Math.Max(Width - 2 * Pad, 1), th);

        using (var p = Theme.Round(track, th / 2))
        using (var b = new SolidBrush(Theme.Track))
            g.FillPath(b, p);

        float t = (_v - Minimum) / (float)(Maximum - Minimum);
        float x = Pad + track.Width * t;
        var fill = new RectangleF(Pad, track.Y, Math.Max(x - Pad, th), th);
        using (var p = Theme.Round(fill, th / 2))
        using (var lg = new LinearGradientBrush(track, Color.White, Theme.Red, LinearGradientMode.Horizontal))
            g.FillPath(lg, p);

        // Perilla
        float r = 11f;
        Color c = Theme.Lerp(Color.White, Theme.Red, t);
        using (var sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            g.FillEllipse(sh, x - r, cy - r + 2, 2 * r, 2 * r);
        using (var tb = new SolidBrush(Color.White))
            g.FillEllipse(tb, x - r, cy - r, 2 * r, 2 * r);
        using (var pen = new Pen(c, 3f))
            g.DrawEllipse(pen, x - r + 1.5f, cy - r + 1.5f, 2 * r - 3, 2 * r - 3);

        if (Focused && ShowFocusCues)
        {
            using var fp = new Pen(Color.FromArgb(130, Theme.Red), 1.5f);
            g.DrawEllipse(fp, x - r - 3, cy - r - 3, 2 * r + 6, 2 * r + 6);
        }
    }
}

class ToggleSwitch : Control
{
    bool _c;
    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _c;
        set
        {
            if (_c == value) return;
            _c = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Size = new Size(46, 26);
        BackColor = Theme.Card;
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Theme.Round(r, r.Height / 2))
        {
            if (Checked)
            {
                using var lg = new LinearGradientBrush(r, Color.FromArgb(255, 135, 135), Theme.Red, LinearGradientMode.Horizontal);
                g.FillPath(lg, path);
            }
            else
            {
                using var b = new SolidBrush(Theme.Track);
                g.FillPath(b, path);
            }
            if (Focused && ShowFocusCues)
            {
                using var fp = new Pen(Color.FromArgb(130, Theme.Red), 1.5f);
                g.DrawPath(fp, path);
            }
        }

        float d = Height - 7f;
        float x = Checked ? Width - d - 3.5f : 3.5f;
        using var kb = new SolidBrush(Color.White);
        g.FillEllipse(kb, x, 3.5f, d, d);
    }
}

class RoundButton : Button
{
    bool hover, down;
    public bool Primary { get; set; }

    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI Semibold", 10F);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Bg);

        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.Round(r, 10);
        Color text;

        if (!Enabled)
        {
            using var b = new SolidBrush(Color.FromArgb(40, 40, 48));
            g.FillPath(b, path);
            text = Theme.Muted;
        }
        else if (Primary)
        {
            Color top = down ? Color.FromArgb(225, 60, 60) : hover ? Color.FromArgb(255, 125, 125) : Color.FromArgb(255, 100, 100);
            Color bot = down ? Color.FromArgb(190, 25, 25) : hover ? Color.FromArgb(240, 50, 50) : Color.FromArgb(225, 38, 38);
            using var lg = new LinearGradientBrush(r, top, bot, LinearGradientMode.Vertical);
            g.FillPath(lg, path);
            text = Color.White;
        }
        else
        {
            Color fill = down ? Color.FromArgb(36, 36, 44) : hover ? Color.FromArgb(60, 60, 73) : Color.FromArgb(46, 46, 57);
            using var b = new SolidBrush(fill);
            using var pen = new Pen(Theme.CardBorder);
            g.FillPath(b, path);
            g.DrawPath(pen, path);
            text = Theme.Text;
        }

        TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
        {
            using var fp = new Pen(Color.FromArgb(130, Theme.Red), 1.5f);
            using var fpath = Theme.Round(new RectangleF(2.5f, 2.5f, Width - 5.5f, Height - 5.5f), 8);
            g.DrawPath(fp, fpath);
        }
    }
}

// ----- Menú contextual oscuro -----
class DarkColors : ProfessionalColorTable
{
    public static readonly Color MenuBg = Color.FromArgb(30, 30, 37);
    static readonly Color Sel = Color.FromArgb(56, 56, 69);

    public override Color ToolStripDropDownBackground => MenuBg;
    public override Color ImageMarginGradientBegin => MenuBg;
    public override Color ImageMarginGradientMiddle => MenuBg;
    public override Color ImageMarginGradientEnd => MenuBg;
    public override Color MenuBorder => Theme.CardBorder;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => Sel;
    public override Color MenuItemSelectedGradientBegin => Sel;
    public override Color MenuItemSelectedGradientEnd => Sel;
    public override Color MenuItemPressedGradientBegin => Sel;
    public override Color MenuItemPressedGradientMiddle => Sel;
    public override Color MenuItemPressedGradientEnd => Sel;
    public override Color SeparatorDark => Theme.CardBorder;
    public override Color SeparatorLight => Theme.CardBorder;
}

class DarkRenderer : ToolStripProfessionalRenderer
{
    public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Text;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var b = new SolidBrush(DarkColors.MenuBg);
        e.Graphics.FillRectangle(b, e.AffectedBounds);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var ir = e.ImageRectangle;
        var r = new RectangleF(ir.X + 1, ir.Y + 1, ir.Width - 2, ir.Height - 2);
        using (var path = Theme.Round(r, 4))
        using (var b = new SolidBrush(Theme.Red))
            g.FillPath(b, path);
        using var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, new[]
        {
            new PointF(r.Left + r.Width * 0.24f, r.Top + r.Height * 0.52f),
            new PointF(r.Left + r.Width * 0.43f, r.Top + r.Height * 0.72f),
            new PointF(r.Left + r.Width * 0.77f, r.Top + r.Height * 0.30f)
        });
    }
}

// ===================================================================
//  Ventana "Acerca de"
// ===================================================================
public class AboutForm : Form
{
    public AboutForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = L.T($"Acerca de {AppInfo.Name}", $"About {AppInfo.Name}");
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9F);
        ClientSize = new Size(360, 344);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Icon = AppInfo.LoadIcon(32);

        var pic = new PictureBox
        {
            Image = AppInfo.LoadIcon(128).ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(96, 96),
            Location = new Point(132, 20),
            BackColor = Theme.Bg
        };
        var lblName = Centered($"{AppInfo.Name}  v{AppInfo.Version}", 124, 34, 18F, FontStyle.Bold, Theme.Text);
        var line = new GradientLine { Location = new Point(140, 162), Size = new Size(80, 3) };
        var lblSub = Centered(AppInfo.Subtitle, 172, 22, 9.5F, FontStyle.Regular, Theme.Muted);
        var lblCredits = Centered(AppInfo.Credits, 200, 26, 10F, FontStyle.Regular, Theme.Text);
        lblCredits.Font = new Font("Segoe UI Emoji", 10F); // para que se vean los emojis

        var lnkGit = Link("GitHub", AppInfo.GitHubUrl, 232);
        var lnkPat = Link("Patreon", AppInfo.PatreonUrl, 254);
        var lnkAdj = Link(L.T("Usa RyzenAdj (FlyGoat)", "Powered by RyzenAdj (FlyGoat)"), AppInfo.RyzenAdjUrl, 276);

        var btnClose = new RoundButton
        {
            Text = L.T("Cerrar", "Close"),
            Location = new Point(130, 302),
            Size = new Size(100, 34),
            DialogResult = DialogResult.Cancel
        };
        CancelButton = btnClose;

        Controls.AddRange(new Control[] { pic, lblName, line, lblSub, lblCredits, lnkGit, lnkPat, lnkAdj, btnClose });
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.DarkTitleBar(Handle);
    }

    Label Centered(string text, int y, int h, float size, FontStyle style, Color color) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", size, style),
        ForeColor = color,
        BackColor = Theme.Bg,
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleCenter,
        Size = new Size(360, h),
        Location = new Point(0, y)
    };

    LinkLabel Link(string text, string url, int y)
    {
        var l = new LinkLabel
        {
            Text = text,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(360, 22),
            Location = new Point(0, y),
            BackColor = Theme.Bg,
            LinkColor = Color.FromArgb(255, 120, 120),
            ActiveLinkColor = Color.White,
            VisitedLinkColor = Color.FromArgb(255, 120, 120),
            LinkBehavior = LinkBehavior.HoverUnderline,
            Font = new Font("Segoe UI", 9.5F)
        };
        l.LinkClicked += (_, _) => AppInfo.OpenUrl(url);
        return l;
    }
}

// ===================================================================
//  Ventana principal
// ===================================================================
// ===================================================================
//  Aviso en pantalla (OSD): aparece abajo al centro, no roba el foco ni bloquea clics
// ===================================================================
class Osd : Form
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 30 };
    string main = "", sub = "";
    Color accent = Theme.Red;
    DateTime until;

    public Osd()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Card;
        DoubleBuffered = true;
        timer.Tick += (_, _) =>
        {
            if (DateTime.UtcNow < until) return;
            Opacity = Math.Max(0, Opacity - 0.12);
            if (Opacity <= 0.02) { timer.Stop(); Hide(); }
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020;   // NOACTIVATE | TOOLWINDOW | TRANSPARENT (clics pasan)
            return cp;
        }
    }

    public void Pop(string mainText, string subText, Color color, int ms = 1800)
    {
        main = mainText; sub = subText; accent = color;
        float sc = DeviceDpi / 96f;
        Size = new Size((int)(340 * sc), (int)(84 * sc));
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - (int)(64 * sc));
        using (var path = Theme.Round(new RectangleF(0, 0, Width, Height), 18 * sc))
            Region = new Region(path);
        until = DateTime.UtcNow.AddMilliseconds(ms);
        Opacity = 1;
        if (!Visible) Show();
        Invalidate();
        timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float sc = DeviceDpi / 96f;
        g.Clear(Theme.Card);

        using (var pen = new Pen(Color.FromArgb(120, accent), 2f * sc))
        using (var path = Theme.Round(new RectangleF(1, 1, Width - 2, Height - 2), 17 * sc))
            g.DrawPath(pen, path);

        float d = 16 * sc;
        var dot = new RectangleF(22 * sc, (Height - d) / 2, d, d);
        using (var glow = new SolidBrush(Color.FromArgb(50, accent)))
            g.FillEllipse(glow, dot.X - 6 * sc, dot.Y - 6 * sc, d + 12 * sc, d + 12 * sc);
        using (var b = new SolidBrush(accent))
            g.FillEllipse(b, dot);

        const TextFormatFlags fl = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                   TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        int x = (int)(56 * sc), w = Width - (int)(72 * sc);
        using var f1 = new Font("Segoe UI Semibold", 16F);
        using var f2 = new Font("Segoe UI", 9F);
        TextRenderer.DrawText(g, sub, f2, new Rectangle(x, (int)(14 * sc), w, (int)(20 * sc)), Theme.Muted, fl);
        TextRenderer.DrawText(g, main, f1, new Rectangle(x, (int)(34 * sc), w, (int)(36 * sc)), Theme.Text, fl);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}

public class MainForm : Form
{
    readonly Osd osd = new();

    static Color ModeColor(int m) => m switch
    {
        AsusMode.Silent => Color.FromArgb(96, 165, 250),
        AsusMode.Turbo => Theme.Red,
        AsusMode.Balanced => Theme.Ok,
        _ => Theme.Muted
    };

    static readonly int[] Presets = { 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600 };
    const int MinTemp = 50, MaxTemp = 95;

    readonly Settings s = Settings.Load();
    readonly TempDisplay tempDisplay = new();
    readonly GradientSlider slider = new();
    readonly ToggleSwitch tglAuto = new();
    readonly ToggleSwitch tglStart = new();
    readonly Label lblInterval = new();
    readonly RoundButton btnMinus = new();
    readonly RoundButton btnPlus = new();
    readonly RoundButton btnApply = new() { Primary = true };
    readonly RoundButton btnAbout = new();
    readonly Label lblStatus = new();
    readonly ToolTip tip = new() { AutoPopDelay = 20000 };
    readonly System.Windows.Forms.Timer timer = new();
    readonly NotifyIcon tray = new();
    readonly ContextMenuStrip menu = new();
    readonly List<ToolStripMenuItem> tempItems = new();
    readonly List<ToolStripMenuItem> intervalItems = new();
    ToolStripMenuItem miHeader = null!, miAuto = null!, miStart = null!, miPlans = null!, miHotkey = null!;
    readonly SemaphoreSlim autoStartGate = new(1, 1);              // schtasks de a uno a la vez
    List<(Guid Id, string Name)>? planCache;                       // lista de planes de energía (se refresca en segundo plano)

    // Modo de energía (orden en pantalla: Silencioso, Equilibrado, Turbo)
    static readonly int[] ModeValues = { AsusMode.Silent, AsusMode.Balanced, AsusMode.Turbo };
    readonly RoundButton[] btnMode = { new(), new(), new() };
    readonly Label lblModeInfo = new();
    readonly List<ToolStripMenuItem> modeItems = new();
    System.Management.ManagementEventWatcher? keyWatcher;
    DateTime lastKey = DateTime.MinValue;
    int curMode = -1;
    bool changingMode;
    readonly System.Windows.Forms.Timer reapplyTimer = new() { Interval = 2500 };
    readonly System.Windows.Forms.Timer planWatch = new() { Interval = 30 * 60 * 1000 };   // detecta cambios del plan de Windows (cada 30 min)
    Guid? lastPlan;

    // Límite de carga de la batería
    static readonly int[] BatValues = { 60, 70, 80, 90, 100 };
    readonly RoundButton[] btnBat = { new(), new(), new(), new(), new() };
    readonly Label lblBatInfo = new();
    readonly List<ToolStripMenuItem> batItems = new();
    int curBat = -1;
    bool batSupported, batVerified, changingBat;
    const int HotkeyId = 0xA71;

    bool startHidden, reallyExit, applying, pending, aboutOpen;
    bool? lastOk;

    public MainForm(bool minimized)
    {
        startHidden = minimized;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = AppInfo.Name;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 680);
        Icon = AppInfo.LoadIcon(32);
        DoubleBuffered = true;

        // ----- Encabezado -----
        var pic = new PictureBox
        {
            Image = AppInfo.LoadIcon(48).ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(40, 40),
            Location = new Point(20, 16),
            BackColor = Theme.Bg
        };
        var lblTitle = Lbl(AppInfo.Name, 17F, FontStyle.Bold, Theme.Text, Theme.Bg, new Point(68, 12), new Size(200, 30));
        var lblSub = Lbl(AppInfo.Subtitle, 9F, FontStyle.Regular, Theme.Muted, Theme.Bg, new Point(70, 40), new Size(310, 20));

        // ----- Tarjeta 1: temperatura -----
        var card1 = new Card { Location = new Point(16, 70), Size = new Size(368, 162) };
        var lblCap = Lbl(L.T("TEMPERATURA OBJETIVO", "TARGET TEMPERATURE"), 8F, FontStyle.Bold,
                         Theme.Muted, Theme.Card, new Point(20, 14), new Size(320, 16));
        tempDisplay.Location = new Point(20, 32);
        tempDisplay.Size = new Size(328, 62);
        slider.Location = new Point(6, 98);
        slider.Size = new Size(356, 40);
        slider.Minimum = MinTemp;
        slider.Maximum = MaxTemp;
        var lblMin = Lbl($"{MinTemp} °C", 8.5F, FontStyle.Regular, Theme.Muted, Theme.Card, new Point(20, 138), new Size(60, 16));
        var lblMax = Lbl($"{MaxTemp} °C", 8.5F, FontStyle.Regular, Theme.Muted, Theme.Card, new Point(288, 138), new Size(60, 16),
                         ContentAlignment.MiddleRight);
        card1.Controls.AddRange(new Control[] { lblCap, tempDisplay, slider, lblMin, lblMax });

        // ----- Tarjeta 3: modo de energía de la laptop -----
        var card3 = new Card { Location = new Point(16, 244), Size = new Size(368, 96) };
        var lblModeCap = Lbl(L.T("MODO DE ENERGÍA", "POWER MODE"), 8F, FontStyle.Bold,
                             Theme.Muted, Theme.Card, new Point(20, 12), new Size(150, 16));
        lblModeInfo.Location = new Point(170, 12);
        lblModeInfo.Size = new Size(178, 16);
        lblModeInfo.AutoSize = false;
        lblModeInfo.TextAlign = ContentAlignment.MiddleRight;
        lblModeInfo.Font = new Font("Segoe UI", 8F);
        lblModeInfo.ForeColor = Theme.Muted;
        lblModeInfo.BackColor = Theme.Card;
        card3.Controls.Add(lblModeCap);
        card3.Controls.Add(lblModeInfo);
        for (int i = 0; i < 3; i++)
        {
            btnMode[i].Text = AsusMode.Name(ModeValues[i]);
            btnMode[i].Font = new Font("Segoe UI Semibold", 9.5F);
            btnMode[i].Location = new Point(20 + i * 112, 40);
            btnMode[i].Size = new Size(104, 40);
            int mv = ModeValues[i];
            btnMode[i].Click += (_, _) => SetMode(mv);
            card3.Controls.Add(btnMode[i]);
        }

        // ----- Tarjeta 4: límite de carga de la batería -----
        var card4 = new Card { Location = new Point(16, 352), Size = new Size(368, 84) };
        var lblBatCap = Lbl(L.T("LÍMITE DE CARGA DE LA BATERÍA", "BATTERY CHARGE LIMIT"), 8F, FontStyle.Bold,
                            Theme.Muted, Theme.Card, new Point(20, 10), new Size(230, 16));
        lblBatInfo.Location = new Point(250, 10);
        lblBatInfo.Size = new Size(98, 16);
        lblBatInfo.AutoSize = false;
        lblBatInfo.TextAlign = ContentAlignment.MiddleRight;
        lblBatInfo.Font = new Font("Segoe UI", 8F);
        lblBatInfo.ForeColor = Theme.Muted;
        lblBatInfo.BackColor = Theme.Card;
        lblBatInfo.Text = "…";
        card4.Controls.Add(lblBatCap);
        card4.Controls.Add(lblBatInfo);
        for (int i = 0; i < BatValues.Length; i++)
        {
            btnBat[i].Text = $"{BatValues[i]}%";
            btnBat[i].Font = new Font("Segoe UI Semibold", 9.5F);
            btnBat[i].Location = new Point(20 + i * 67, 34);
            btnBat[i].Size = new Size(61, 36);
            btnBat[i].Enabled = false;   // se habilita al confirmar que el equipo lo soporta
            int bv = BatValues[i];
            btnBat[i].Click += (_, _) => SetBattery(bv);
            card4.Controls.Add(btnBat[i]);
        }

        // ----- Tarjeta 2: opciones -----
        var card2 = new Card { Location = new Point(16, 448), Size = new Size(368, 132) };
        var lblAuto = Lbl(L.T("Re-aplicar automáticamente", "Re-apply automatically"), 10F, FontStyle.Regular,
                          Theme.Text, Theme.Card, new Point(20, 14), new Size(270, 32));
        tglAuto.Location = new Point(302, 17);
        var sep1 = new Panel { BackColor = Theme.CardBorder, Location = new Point(20, 47), Size = new Size(328, 1) };

        var lblEvery = Lbl(L.T("Intervalo", "Interval"), 10F, FontStyle.Regular,
                           Theme.Text, Theme.Card, new Point(20, 50), new Size(150, 34));
        btnMinus.Text = "−";
        btnMinus.Font = new Font("Segoe UI Semibold", 12F);
        btnMinus.Location = new Point(216, 52);
        btnMinus.Size = new Size(30, 30);
        lblInterval.Location = new Point(250, 52);
        lblInterval.Size = new Size(64, 30);
        lblInterval.AutoSize = false;
        lblInterval.TextAlign = ContentAlignment.MiddleCenter;
        lblInterval.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        lblInterval.BackColor = Theme.Card;
        btnPlus.Text = "+";
        btnPlus.Font = new Font("Segoe UI Semibold", 12F);
        btnPlus.Location = new Point(318, 52);
        btnPlus.Size = new Size(30, 30);
        var sep2 = new Panel { BackColor = Theme.CardBorder, Location = new Point(20, 85), Size = new Size(328, 1) };

        var lblStart = Lbl(L.T("Iniciar con Windows (minimizado)", "Start with Windows (minimized)"), 10F, FontStyle.Regular,
                           Theme.Text, Theme.Card, new Point(20, 88), new Size(270, 34));
        tglStart.Location = new Point(302, 92);
        card2.Controls.AddRange(new Control[] { lblAuto, tglAuto, sep1, lblEvery, btnMinus, lblInterval, btnPlus, sep2, lblStart, tglStart });

        // ----- Botones y estado -----
        btnApply.Text = L.T("Aplicar ahora", "Apply now");
        btnApply.Location = new Point(16, 592);
        btnApply.Size = new Size(252, 42);
        btnAbout.Text = L.T("Acerca de", "About");
        btnAbout.Location = new Point(276, 592);
        btnAbout.Size = new Size(108, 42);

        lblStatus.Location = new Point(20, 642);
        lblStatus.Size = new Size(360, 34);
        lblStatus.AutoSize = false;
        lblStatus.AutoEllipsis = true;
        lblStatus.Font = new Font("Segoe UI", 9F);
        lblStatus.ForeColor = Theme.Muted;
        lblStatus.BackColor = Theme.Bg;
        lblStatus.Text = "●  " + L.T("Listo", "Ready");

        Controls.AddRange(new Control[] { pic, lblTitle, lblSub, card1, card3, card4, card2, btnApply, btnAbout, lblStatus });

        // ----- Cargar ajustes en la interfaz (antes de conectar eventos) -----
        slider.Value = Math.Clamp(s.TargetTemp, MinTemp, MaxTemp);
        s.TargetTemp = slider.Value;
        tempDisplay.Value = s.TargetTemp;
        tglAuto.Checked = s.AutoApply;
        tglStart.Checked = s.AutoStart;
        lblInterval.Text = FormatInterval(s.IntervalSeconds);
        UpdateIntervalEnabled();

        // ----- Eventos -----
        slider.ValueChanged += (_, _) => { s.TargetTemp = slider.Value; tempDisplay.Value = slider.Value; UpdateTray(); };
        slider.Committed += (_, _) => { s.Save(); Apply(); };
        tglAuto.CheckedChanged += (_, _) => { s.AutoApply = tglAuto.Checked; s.Save(); ConfigureTimer(); UpdateIntervalEnabled(); };
        tglStart.CheckedChanged += (_, _) => { s.AutoStart = tglStart.Checked; s.Save(); ApplyAutoStart(); };
        btnMinus.Click += (_, _) => StepInterval(-1);
        btnPlus.Click += (_, _) => StepInterval(+1);
        btnApply.Click += (_, _) => Apply();
        btnAbout.Click += (_, _) => ShowAbout();
        timer.Tick += (_, _) => Apply();

        // ----- Menú (clic derecho en la ventana y en el ícono de la bandeja) -----
        BuildMenu();
        tray.Icon = AppInfo.LoadIcon(SystemInformation.SmallIconSize.Width);
        tray.ContextMenuStrip = menu;
        tray.Visible = true;
        tray.DoubleClick += (_, _) => ShowWindow();
        UpdateTray();
        AttachMenu(this);

        ConfigureTimer();
        ResumeLayout(false);

        _ = Handle;          // fuerza crear el handle aunque inicie oculto (lo necesita el atajo global)
        reapplyTimer.Tick += (_, _) => { reapplyTimer.Stop(); Apply(); };

        // Re-aplicar el límite cuando ocurre algo que suele reiniciarlo: reanudar de suspensión,
        // conectar/desconectar el cargador o cambiar el plan de energía de Windows
        lastPlan = WinPlans.Active();
        planWatch.Tick += (_, _) =>
        {
            var p = WinPlans.Active();
            if (p == lastPlan) return;
            lastPlan = p;
            Log.Write("Cambió el plan de energía de Windows: se vuelve a aplicar el límite");
            ScheduleReapply(1500);
        };
        planWatch.Start();   // si en tu caso nunca cambia, se puede quitar sin perder nada importante
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        RefreshMode();       // leer el modo actual de la laptop
        Task.Run(StartKeyListener);   // escuchar Fn+F5 (evento ASUS); WMI puede tardar, por eso va en segundo plano
        RefreshBattery();    // leer el límite de carga (y restaurarlo si el equipo lo perdió)

        // Si el inicio automático está activado, se recrea la tarea en cada arranque para que siempre apunte
        // al TufDood.exe que está corriendo (si moviste o recompilaste el programa, la ruta vieja dejaba de servir)
        if (s.AutoStart)
        {
            string exePath = Environment.ProcessPath ?? "?";
            Task.Run(() =>
            {
                autoStartGate.Wait();
                try
                {
                    bool ok = AutoStartTask.EnsureCurrent();
                    Log.Write(ok ? $"Inicio automático: tarea actualizada -> {exePath}"
                                 : $"Inicio automático: NO se pudo crear la tarea -> {exePath}");
                }
                finally { autoStartGate.Release(); }
            });
        }
        Apply();             // aplicar al iniciar
    }

    static Label Lbl(string text, float size, FontStyle style, Color fore, Color back, Point loc, Size sz,
                     ContentAlignment align = ContentAlignment.MiddleLeft) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", size, style),
        ForeColor = fore,
        BackColor = back,
        Location = loc,
        Size = sz,
        AutoSize = false,
        TextAlign = align
    };

    static string FormatInterval(int sec) =>
        sec % 3600 == 0 ? $"{sec / 3600} h" : sec % 60 == 0 ? $"{sec / 60} min" : $"{sec} s";

    void AttachMenu(Control c)
    {
        c.ContextMenuStrip = menu;
        foreach (Control child in c.Controls) AttachMenu(child);
    }

    void BuildMenu()
    {
        menu.Font = new Font("Segoe UI", 9.5F);

        miHeader = new ToolStripMenuItem(AppInfo.Name) { Enabled = false, Font = new Font(menu.Font, FontStyle.Bold) };
        var miOpen = new ToolStripMenuItem(L.T("Abrir TufDood", "Open TufDood"), null, (_, _) => ShowWindow());

        var miTarget = new ToolStripMenuItem(L.T("Temperatura objetivo", "Target temperature"));
        for (int t = MinTemp; t <= MaxTemp; t += 5)
        {
            int temp = t;
            var it = new ToolStripMenuItem($"{temp} °C") { Tag = temp };
            it.Click += (_, _) => { SetTarget(temp); Apply(); };
            tempItems.Add(it);
            miTarget.DropDownItems.Add(it);
        }

        var miApply = new ToolStripMenuItem(L.T("Aplicar ahora", "Apply now"), null, (_, _) => Apply());
        miAuto = new ToolStripMenuItem(L.T("Re-aplicar automáticamente", "Re-apply automatically"), null,
                                       (_, _) => tglAuto.Checked = !tglAuto.Checked);

        var miInterval = new ToolStripMenuItem(L.T("Intervalo", "Interval"));
        foreach (int sec in Presets)
        {
            int v = sec;
            var it = new ToolStripMenuItem(FormatInterval(v)) { Tag = v };
            it.Click += (_, _) => SetInterval(v);
            intervalItems.Add(it);
            miInterval.DropDownItems.Add(it);
        }

        var miMode = new ToolStripMenuItem(L.T("Modo de energía (laptop)", "Power mode (laptop)"));
        foreach (int mv in ModeValues)
        {
            int v = mv;
            var it = new ToolStripMenuItem(AsusMode.Name(v)) { Tag = v };
            it.Click += (_, _) => SetMode(v);
            modeItems.Add(it);
            miMode.DropDownItems.Add(it);
        }
        miMode.DropDownItems.Add(new ToolStripSeparator());
        miMode.DropDownItems.Add(new ToolStripMenuItem(L.T("Siguiente modo", "Next mode") + "   (Fn+F5)", null, (_, _) => CycleMode()));

        var miBat = new ToolStripMenuItem(L.T("Límite de carga de la batería", "Battery charge limit"));
        foreach (int bv in BatValues)
        {
            int b = bv;
            var it = new ToolStripMenuItem(b == 100 ? L.T("100 % (sin límite)", "100 % (no limit)") : $"{b} %") { Tag = b };
            it.Click += (_, _) => SetBattery(b);
            batItems.Add(it);
            miBat.DropDownItems.Add(it);
        }

        miPlans = new ToolStripMenuItem(L.T("Plan de energía de Windows", "Windows power plan"));
        miPlans.DropDownItems.Add(new ToolStripMenuItem("…"));

        miHotkey = new ToolStripMenuItem(L.T("Atajo Ctrl+Alt+F5 para cambiar de modo", "Ctrl+Alt+F5 hotkey to switch mode"), null,
                                         (_, _) => { s.Hotkey = !s.Hotkey; s.Save(); ApplyHotkey(); });

        miStart = new ToolStripMenuItem(L.T("Iniciar con Windows", "Start with Windows"), null,
                                        (_, _) => tglStart.Checked = !tglStart.Checked);
        var miLog = new ToolStripMenuItem(L.T("Ver registro", "View log"), null, (_, _) => Log.Open());
        var miAbout = new ToolStripMenuItem(L.T("Acerca de", "About"), null, (_, _) => ShowAbout());
        var miExit = new ToolStripMenuItem(L.T("Salir", "Exit"), null, (_, _) => { reallyExit = true; Close(); });

        menu.Items.AddRange(new ToolStripItem[]
        {
            miHeader, new ToolStripSeparator(),
            miOpen, miTarget, miApply, new ToolStripSeparator(),
            miMode, miBat, miPlans, miHotkey, new ToolStripSeparator(),
            miAuto, miInterval, miStart, new ToolStripSeparator(),
            miLog, miAbout, miExit
        });
        menu.Opening += (_, _) => RefreshMenu();
    }

    void RefreshMenu()
    {
        miHeader.Text = $"{AppInfo.Name}  ·  {s.TargetTemp} °C";
        foreach (var it in tempItems) it.Checked = (int)it.Tag! == s.TargetTemp;
        foreach (var it in intervalItems) it.Checked = (int)it.Tag! == s.IntervalSeconds;
        miAuto.Checked = s.AutoApply;
        miStart.Checked = s.AutoStart;
        miHotkey.Checked = s.Hotkey;
        foreach (var it in modeItems) it.Checked = (int)it.Tag! == curMode;
        foreach (var it in batItems) { it.Enabled = batSupported; it.Checked = (int)it.Tag! == curBat; }

        // La primera vez se lee la lista; después se usa la guardada y se verifica en segundo plano
        bool first = planCache == null;
        planCache ??= WinPlans.List();
        BuildPlanItems(planCache, WinPlans.Active());
        if (!first) RefreshPlansInBackground();
    }

    async void RefreshPlansInBackground()
    {
        try
        {
            var fresh = await Task.Run(WinPlans.List);
            if (IsDisposed || planCache == null || fresh.SequenceEqual(planCache)) return;
            planCache = fresh;
            if (!miPlans.DropDown.Visible) BuildPlanItems(fresh, WinPlans.Active());
        }
        catch { }
    }

    void BuildPlanItems(List<(Guid Id, string Name)> plans, Guid? active)
    {
        miPlans.DropDownItems.Clear();
        foreach (var (id, name) in plans)
        {
            var g = id;
            var it = new ToolStripMenuItem(name) { Checked = active == g };
            it.Click += (_, _) =>
            {
                bool ok = WinPlans.Set(g);
                Log.Write($"Plan de Windows -> {name}: {(ok ? "OK" : "FALLÓ")}");
                SetStatus(ok, ok ? L.T($"Plan de energía: {name}", $"Power plan: {name}")
                                 : L.T("No se pudo cambiar el plan de energía", "Couldn't change the power plan"));
                if (ok) osd.Pop(name, L.T("Plan de energía de Windows", "Windows power plan"), Color.FromArgb(250, 204, 21));
                else osd.Pop(L.T("No se pudo cambiar", "Couldn't change"), L.T("Plan de energía de Windows", "Windows power plan"), Theme.Hot);
            };
            miPlans.DropDownItems.Add(it);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.DarkTitleBar(Handle);
        ApplyHotkey();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && m.WParam.ToInt32() == HotkeyId) CycleMode();   // WM_HOTKEY
        base.WndProc(ref m);
    }

    // ----- Modo de energía de la laptop -----
    void ApplyHotkey()
    {
        if (!IsHandleCreated) return;
        Native.UnregisterHotKey(Handle, HotkeyId);
        if (s.Hotkey && !Native.RegisterHotKey(Handle, HotkeyId, 0x0001 | 0x0002, 0x74))   // Alt+Ctrl+F5
            Log.Write("No se pudo registrar el atajo Ctrl+Alt+F5 (¿ya está en uso?)");
    }

    async void RefreshMode()
    {
        await Task.Run(AsusMode.Get);                    // solo para saber si el equipo lo soporta
        curMode = s.LastMode is >= 0 and <= 2 ? s.LastMode : -1;
        UpdateModeUi();
    }

    void UpdateModeUi()
    {
        for (int i = 0; i < 3; i++)
        {
            btnMode[i].Primary = ModeValues[i] == curMode;
            btnMode[i].Invalidate();
        }
        lblModeInfo.Text = !AsusMode.Available ? L.T("No disponible en este equipo", "Not available on this PC")
                         : "Fn+F5  ·  Ctrl+Alt+F5";
        UpdateTray();
    }

    void ScheduleReapply(int ms)
    {
        reapplyTimer.Stop();
        reapplyTimer.Interval = ms;
        reapplyTimer.Start();
    }

    void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode != Microsoft.Win32.PowerModes.Resume && e.Mode != Microsoft.Win32.PowerModes.StatusChange) return;
        bool resume = e.Mode == Microsoft.Win32.PowerModes.Resume;
        Log.Write(resume ? "Reanudó de suspensión: se vuelve a aplicar el límite"
                         : "Cambió la fuente de energía (cargador/batería): se vuelve a aplicar el límite");
        // este evento llega en otro hilo
        try
        {
            if (!IsDisposed) BeginInvoke(async () =>
            {
                ScheduleReapply(resume ? 4000 : 2000);
                if (resume) { await Task.Delay(4000); RefreshBattery(); }   // el límite de carga también puede perderse al suspender
            });
        }
        catch { }
    }

    // ----- Límite de carga de la batería -----
    async void RefreshBattery()
    {
        var (present, val) = await Task.Run(AsusMode.GetChargeInfo);
        batSupported = present;
        batVerified = val is >= 10 and <= 100;   // en algunos equipos el valor sale 0: no informan el porcentaje
        curBat = batVerified ? val : (s.BatteryLimit is >= 10 and <= 100 ? s.BatteryLimit : -1);
        UpdateBatUi();
        // si el usuario había elegido un límite y el equipo no lo reporta (o ya no lo tiene), se vuelve a aplicar en silencio
        if (present && s.BatteryLimit is >= 10 and <= 100 && s.BatteryLimit != val)
            SetBattery(s.BatteryLimit, quiet: true);
    }

    void UpdateBatUi()
    {
        for (int i = 0; i < BatValues.Length; i++)
        {
            btnBat[i].Enabled = batSupported;
            btnBat[i].Primary = batSupported && BatValues[i] == curBat;
            btnBat[i].Invalidate();
        }
        lblBatInfo.Text = !batSupported ? L.T("No disponible", "Not available")
                        : batVerified ? L.T($"Actual: {curBat} %", $"Current: {curBat} %")
                        : curBat > 0 ? L.T($"Elegido: {curBat} %", $"Chosen: {curBat} %")
                        : L.T("Sin elegir", "Not set");
    }

    async void SetBattery(int percent, bool quiet = false)
    {
        if (!batSupported || changingBat) return;
        changingBat = true;
        try
        {
            var (ok, back) = await Task.Run(() => AsusMode.SetChargeLimit(percent));
            batVerified = back is >= 10 and <= 100;
            if (ok) curBat = batVerified ? back : percent;
            else if (batVerified) curBat = back;
            UpdateBatUi();
            string cap = L.T("Límite de carga de la batería", "Battery charge limit");
            if (ok)
            {
                s.BatteryLimit = percent;
                s.Save();
                if (quiet) return;
                string txt = curBat == 100 ? L.T("Sin límite (100 %)", "No limit (100 %)") : $"{curBat} %";
                string note = batVerified
                    ? (curBat == percent ? "" : L.T(" (el equipo lo ajustó)", " (adjusted by the PC)"))
                    : L.T(" (el equipo no permite confirmarlo)", " (the PC can't confirm it)");
                SetStatus(true, L.T($"Límite de carga: {txt}{note}", $"Charge limit: {txt}{note}"));
                osd.Pop(txt, cap, Color.FromArgb(250, 204, 21));
            }
            else if (!quiet)
            {
                SetStatus(false, L.T("No se pudo cambiar el límite de carga", "Couldn't change the charge limit"));
                osd.Pop(L.T("No se pudo cambiar", "Couldn't change"), cap, Theme.Hot);
            }
        }
        finally { changingBat = false; }
    }

    void CycleMode()
    {
        if (changingMode) return;
        // Si no se conoce el modo actual, el primer toque lleva a Equilibrado (estado conocido)
        SetMode(AsusMode.Next(curMode < 0 ? AsusMode.Silent : curMode));
    }

    async void SetMode(int mode)
    {
        if (changingMode) return;   // solo mientras dura la llamada al driver (milisegundos)
        changingMode = true;
        int prev = curMode;
        try
        {
            // Respuesta inmediata: se actualiza la interfaz y el aviso antes de hablar con el driver
            string name = AsusMode.Name(mode);
            curMode = mode;
            UpdateModeUi();
            osd.Pop(name, L.T("Modo de energía", "Power mode"), ModeColor(mode));

            bool ok = await Task.Run(() => AsusMode.Set(mode));
            if (ok)
            {
                s.LastMode = mode;
                s.Save();
                SetStatus(true, L.T($"Modo: {name}", $"Mode: {name}"));
                // El firmware reinicia los límites al cambiar de modo: volver a aplicar el objetivo de temperatura
                // (una sola vez, 2.5 s después del último cambio, aunque cambies de modo varias veces seguidas)
                ScheduleReapply(2500);
            }
            else
            {
                curMode = prev;
                UpdateModeUi();
                SetStatus(false, L.T("No se pudo cambiar el modo (¿driver ASUS System Control Interface?)",
                                     "Couldn't change the mode (ASUS System Control Interface driver?)"));
                osd.Pop(L.T("No se pudo cambiar", "Couldn't change"), L.T("Modo de energía", "Power mode"), Theme.Hot);
            }
        }
        finally { changingMode = false; }
    }

    // Fn+F5 llega como evento WMI de ASUS (EventID 174), igual que en G-Helper
    void StartKeyListener()
    {
        try
        {
            var query = new System.Management.WqlEventQuery("SELECT * FROM AsusAtkWmiEvent");
            keyWatcher = new System.Management.ManagementEventWatcher(
                new System.Management.ManagementScope(@"root\wmi"), query);
            keyWatcher.EventArrived += (_, e) =>
            {
                try
                {
                    int id = Convert.ToInt32(e.NewEvent["EventID"]);
                    Log.Write($"Tecla ASUS: evento {id}");
                    if (id == 174 && (DateTime.UtcNow - lastKey).TotalMilliseconds > 250)
                    {
                        lastKey = DateTime.UtcNow;
                        if (!IsDisposed) BeginInvoke(CycleMode);
                    }
                }
                catch { }
            };
            keyWatcher.Start();
            Log.Write("Escucha de teclas ASUS iniciada");
        }
        catch (Exception ex)
        {
            Log.Write("No se pudo escuchar Fn+F5: " + ex.Message);
        }
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(startHidden ? false : value);
    }

    void ShowWindow()
    {
        startHidden = false;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    void ShowAbout()
    {
        if (aboutOpen) return;
        aboutOpen = true;
        try
        {
            using var about = new AboutForm();
            if (Visible)
            {
                about.ShowDialog(this);
            }
            else
            {
                about.StartPosition = FormStartPosition.CenterScreen;
                about.TopMost = true;
                about.ShowDialog();
            }
        }
        finally { aboutOpen = false; }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        try { keyWatcher?.Stop(); keyWatcher?.Dispose(); } catch { }
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        planWatch.Stop();
        osd.Dispose();
        if (IsHandleCreated) Native.UnregisterHotKey(Handle, HotkeyId);
        tray.Visible = false;
        Log.Flush();
        base.OnFormClosing(e);
    }

    // ----- Estado / acciones -----
    void UpdateTray() => tray.Text = curMode >= 0
        ? $"{AppInfo.Name}: {s.TargetTemp} °C · {AsusMode.Name(curMode)}"
        : $"{AppInfo.Name}: {s.TargetTemp} °C";

    void SetTarget(int t)
    {
        slider.Value = t;   // el evento ValueChanged actualiza s.TargetTemp y el número
        s.Save();
    }

    void SetInterval(int sec)
    {
        s.IntervalSeconds = sec;
        s.Save();
        lblInterval.Text = FormatInterval(sec);
        ConfigureTimer();
    }

    void StepInterval(int dir)
    {
        int cur = s.IntervalSeconds;
        int next = dir < 0 ? Presets.LastOrDefault(p => p < cur) : Presets.FirstOrDefault(p => p > cur);
        if (next != 0) SetInterval(next);
    }

    void UpdateIntervalEnabled()
    {
        btnMinus.Enabled = btnPlus.Enabled = tglAuto.Checked;
        lblInterval.ForeColor = tglAuto.Checked ? Theme.Text : Theme.Muted;
    }

    void ConfigureTimer()
    {
        timer.Stop();
        if (s.AutoApply)
        {
            timer.Interval = Math.Max(1, s.IntervalSeconds) * 1000;
            timer.Start();
        }
    }

    async void Apply()
    {
        if (applying) { pending = true; return; }
        applying = true;
        try
        {
            do
            {
                pending = false;
                int t = s.TargetTemp;
                var r = await Task.Run(() => RyzenAdj.SetTctlTemp(t));
                ShowStatus(r);
            } while (pending);
        }
        finally { applying = false; }
    }

    void ShowStatus(ApplyResult r)
    {
        SetStatus(r.Ok, r.Msg);
        if (lastOk != r.Ok)
            Log.Write($"{(r.Ok ? "OK" : "FAIL")}  {r.Msg}  |  {r.Detail}");
        lastOk = r.Ok;
    }

    void SetStatus(bool ok, string msg)
    {
        string text = $"{DateTime.Now:HH:mm:ss}  {msg}";
        lblStatus.Text = "●  " + text;
        lblStatus.ForeColor = ok ? Theme.Ok : Theme.Hot;
        tip.SetToolTip(lblStatus, text);
    }

    async void ApplyAutoStart()
    {
        bool want = s.AutoStart;
        await autoStartGate.WaitAsync();
        try
        {
            // schtasks puede tardar: se ejecuta fuera del hilo de la interfaz para que la ventana no se congele
            bool ok = await Task.Run(() => want ? AutoStartTask.Enable() : AutoStartTask.Disable());
            if (IsDisposed || want != s.AutoStart) return;   // si el interruptor cambió otra vez, manda el último estado
            if (ok)
                SetStatus(true, want
                    ? L.T("Inicio automático activado", "Auto-start enabled")
                    : L.T("Inicio automático desactivado", "Auto-start disabled"));
            else if (want)
                SetStatus(false, L.T("No se pudo configurar el inicio automático", "Couldn't set up auto-start"));
            // Si falla al desactivar normalmente es porque la tarea no existía: no es un error real.
        }
        finally { autoStartGate.Release(); }
    }
}

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "TufDood_SingleInstance", out bool isNew);
        if (!isNew) return;

        ApplicationConfiguration.Initialize();
        ToolStripManager.Renderer = new DarkRenderer();
        Application.Run(new MainForm(args.Contains("--minimized")));
        Log.Flush();
    }
}
