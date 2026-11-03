using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Server;

// A copy of the server folder with the packed mod, killed and emptied at dispose. Fixed folders, since Windows' firewall
// asks once for each new path of server.exe.
public sealed class RunningServer : IAsyncDisposable
{
    private const string Executable = "server.exe";
    private const string ModName = "CyrodiilRP";
    private const string PortVariable = "RELAY_Server__Port";
    private const int Slots = 4;
    private const int DeleteTries = 10;

    private static readonly TimeSpan DeletePause = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PollPause = TimeSpan.FromMilliseconds(100);

    // What a run writes, and our mod and the chat it replaces, which come from the pack.
    private static readonly string[] NotCopied = ["saves", "data", "logs", "mods-disabled", "permissions.json",
        Path.Combine("mods", ModName), Path.Combine("mods", "OblivionMP.Chat")];

    private readonly List<string> _lines = [];
    private readonly FileStream _slot;
    private Process? _process;

    private RunningServer(string folder, FileStream slot)
    {
        Folder = folder;
        _slot = slot;
    }

    public string Folder { get; }

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
                return [.. _lines];
        }
    }

    public string Output => string.Join(Environment.NewLine, Lines);

    public static async Task<RunningServer> StartAsync(string connectionString)
    {
        var mod = Path.Combine(Repository.Root, "out", "mods", ModName);
        if (!Directory.Exists(mod))
            throw new InvalidOperationException($"No packed mod in {mod}: run ./pack.ps1 -PackOnly first.");

        var server = TakeSlot();
        try
        {
            server.DeleteFolder();
            Copy(Repository.Server, server.Folder, Repository.Server);
            Copy(mod, Path.Combine(server.Folder, "mods", ModName), mod);
            File.WriteAllText(Path.Combine(server.Folder, "mods", ModName, "server", "config.json"),
                $$"""{ "database": { "connectionString": "{{connectionString}}" } }""");
            server.Launch();
            return server;
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    // True when a line containing the text came, false when the server ended or the time ran out first.
    public async Task<bool> WaitForAsync(string text, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (Lines.Any(line => line.Contains(text, StringComparison.Ordinal)))
                return true;

            if (_process is null || _process.HasExited)
                return false;

            await Task.Delay(PollPause);
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);

                await _process.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
            }

            _process.Dispose();
        }

        DeleteFolder();
        await _slot.DisposeAsync();
    }

    // The first fixed folder no other run holds: its lock file stays open, unshared, while the server runs.
    private static RunningServer TakeSlot()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyrodiilrp-smoke");
        Directory.CreateDirectory(root);
        for (var i = 0; i < Slots; i++)
        {
            var folder = Path.Combine(root, $"server{i}");
            try
            {
                var slot = new FileStream($"{folder}.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                return new RunningServer(folder, slot);
            }
            catch (IOException)
            {
                // Another run has it.
            }
        }

        throw new InvalidOperationException($"All {Slots} server folders under {root} are taken by other runs.");
    }

    private void Launch()
    {
        var start = new ProcessStartInfo(Path.Combine(Folder, Executable))
        {
            WorkingDirectory = Folder,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment[PortVariable] = FindFreePort().ToString();

        _process = new Process { StartInfo = start };
        _process.OutputDataReceived += (_, e) => Add(e.Data);
        _process.ErrorDataReceived += (_, e) => Add(e.Data);
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    private void Add(string? line)
    {
        if (line is null)
            return;

        lock (_lines)
            _lines.Add(line);
    }

    // The killed process can hold its files for a moment.
    private void DeleteFolder()
    {
        for (var i = 0; i < DeleteTries && Directory.Exists(Folder); i++)
        {
            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(DeletePause);
            }
        }
    }

    private static void Copy(string from, string to, string root)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from).Where(file => !NotCopied.Contains(Path.GetRelativePath(root, file), StringComparer.OrdinalIgnoreCase)))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);

        foreach (var directory in Directory.EnumerateDirectories(from))
        {
            if (!NotCopied.Contains(Path.GetRelativePath(root, directory), StringComparer.OrdinalIgnoreCase))
                Copy(directory, Path.Combine(to, Path.GetFileName(directory)), root);
        }
    }

    private static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
