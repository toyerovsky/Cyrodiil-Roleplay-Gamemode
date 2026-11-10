using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The carryables lying in the world, in <see cref="CarryConfig.SaveFile"/> next to the server DLL, so a restart keeps
/// what was put down where it was. One in someone's hand is not here: it is put down again when they leave. Written
/// whole on every change, through a temporary file.
/// </summary>
[Service]
public sealed partial class CarryableStore(JobsConfig config, ILogger logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // The relay is published AOT with reflection JSON off; naming a resolver opts this file back in.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private List<Carryable> _lying = [];
    private string? _path;

    public IReadOnlyList<Carryable> Lying => _lying;

    /// <summary>The server mod folder, read at once. Set by the entry point before any service starts.</summary>
    public void UseFolder(string folder)
    {
        _path = Path.Combine(folder, config.Carrying.SaveFile);
        Load();
    }

    // Internal so the tests can load a file without the SDK starting the service
    internal void Load()
    {
        _lying = [];
        if (_path is null || !File.Exists(_path))
            return;

        try
        {
            _lying = JsonSerializer.Deserialize<List<Carryable>>(File.ReadAllText(_path), Json) ?? [];
            logger.LogInformation("Jobs: {Count} carryables loaded from {Path}.", _lying.Count, _path);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            var broken = _path + ".broken";
            File.Copy(_path, broken, overwrite: true);
            logger.LogWarning(exception, "Jobs: could not read {Path}, copied it to {Broken}; no carryables lying.", _path, broken);
        }
    }

    public void Add(Carryable carryable)
    {
        _lying.Add(carryable);
        Save();
    }

    public bool Remove(string id)
    {
        var removed = _lying.RemoveAll(carryable => carryable.Id == id) > 0;
        if (removed)
            Save();
        return removed;
    }

    private void Save()
    {
        if (_path is null)
        {
            logger.LogError("Jobs: the carryables cannot be saved, the store was never given its folder.");
            return;
        }

        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_lying, Json));
        File.Move(temporary, _path, overwrite: true);
    }
}
