using System;
using System.Text;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;

#if DEBUG
using System.Runtime.InteropServices;
#endif

namespace ElixirKit;

public class API
{
    public bool mainInstance;
    private Mutex? mutex;
    private string? id;
    private Release? release;

    public bool MainInstance {
        get {
            return mainInstance;
        }
    }

    public bool HasExited {
        get {
            if (!mainInstance)
            {
                throw new Exception("Not on main instance");
            }

            return release!.HasExited;
        }
    }

    // On Windows we need to manually handle the app being launched multiple times.
    // It can be opened directly or via its associated file types and URL schemes.
    // To help with this, we can initialize the API with a unique `id` and we'll
    // ensure only the "main instance" interacts with the release process.
    public API(string? id = null, string? logPath = null)
    {
        if (id == null)
        {
            mainInstance = true;
        }
        else
        {
            this.id = id;
            mutex = new Mutex(true, id, out mainInstance);
        }
    }

    public void Start(string name, ExitHandler? exited = null, string? logPath = null)
    {
        if (!mainInstance)
        {
            throw new Exception("Not on main instance");
        }

        release = new Release(name, exited, logPath);

        if (id != null)
        {
            var t = new Task(() => {
                while (true) {
                    var line = PipeReadLine();

                    if (line != null)
                    {
                        release!.Send(line);
                    }
                }
            });

            t.Start();
        }
    }

    public int Stop()
    {
        if (!mainInstance)
        {
            throw new Exception("Not on main instance");
        }

        return release!.Stop();
    }

    public int WaitForExit()
    {
        if (!mainInstance)
        {
            throw new Exception("Not on main instance");
        }

        return release!.WaitForExit();
    }

    public void Publish(string name, string data)
    {
        if (mainInstance)
        {
            release!.Publish(name, data);
        }
        else
        {
            var message = Release.EncodeEventMessage(name, data);
            PipeWriteLine(message);
        }
    }

    private string? PipeReadLine()
    {
        using var pipe = new NamedPipeServerStream(id!);
        pipe.WaitForConnection();
        using var reader = new StreamReader(pipe);
        var line = reader.ReadLine()!;
        pipe.Disconnect();
        return line;
    }

    private void PipeWriteLine(string line)
    {
        using var pipe = new NamedPipeClientStream(id!);
        pipe.Connect();
        using var writer = new StreamWriter(pipe);
        writer.WriteLine(line);
    }
}

public delegate void ExitHandler(int ExitCode);

internal class Release
{
    Process startProcess;
    NetworkStream stream;
    TcpListener listener;
    TcpClient client;

    internal bool HasExited {
        get {
            return startProcess.HasExited;
        }
    }

    public Release(string name, ExitHandler? exited = null, string? logPath = null)
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 0);
        listener = new(endpoint);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        startProcess = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = relScript(name),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        startProcess.StartInfo.Arguments = "start";
        startProcess.StartInfo.EnvironmentVariables.Add("ELIXIRKIT_PORT", $"{port}");

        if (exited != null)
        {
            startProcess.EnableRaisingEvents = true;
            startProcess.Exited += (sender, args) =>
            {
                exited(startProcess.ExitCode);
            };
        }

        if (logPath == null)
        {
            startProcess.OutputDataReceived += (sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data)) { Console.WriteLine(e.Data); }
            };

            startProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data)) { Console.Error.WriteLine(e.Data); }
            };
        }
        else
        {
            var logWriter = File.AppendText(logPath);
            logWriter.AutoFlush = true;

            startProcess.OutputDataReceived += (sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data)) { logWriter.WriteLine(e.Data); }
            };

            startProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data)) { logWriter.WriteLine(e.Data); }
            };
        }

        startProcess.Start();
        startProcess.BeginOutputReadLine();
        startProcess.BeginErrorReadLine();

        client = listener.AcceptTcpClient();
        stream = client.GetStream();
    }

    internal static string EncodeEventMessage(string name, string data)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        var encoded = System.Convert.ToBase64String(bytes);
        return $"event:{name}:{encoded}";
    }

    public void Publish(string name, string data) {
        Send(EncodeEventMessage(name, data));
    }

    internal void Send(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message + "\n");
        stream.Write(bytes, 0, bytes.Length);
    }

    public int Stop()
    {
        if (HasExited)
        {
            return startProcess!.ExitCode;
        }

        client.Close();
        listener.Stop();
        return WaitForExit();
    }

    public int WaitForExit()
    {
        startProcess!.WaitForExit();
        return startProcess!.ExitCode;
    }

    private string relScript(string name)
    {
        var exe = Process.GetCurrentProcess().MainModule!.FileName;
        var dir = Path.GetDirectoryName(exe)!;

        if (Path.GetExtension(exe) == ".exe")
        {
            return Path.Combine(dir, "rel", "bin", name + ".bat");
        }
        else
        {
            return Path.Combine(dir, "rel", "bin", name);
        }
    }
}

public static class Utils
{

#if DEBUG
    public static void DebugAttachConsole()
    {
        AttachConsole(ATTACH_PARENT_PROCESS);
    }

    [DllImport("kernel32.dll")]
    static extern bool AttachConsole( int dwProcessId );
    private const int ATTACH_PARENT_PROCESS = -1;
#else
    public static void DebugAttachConsole() {}
#endif
}
