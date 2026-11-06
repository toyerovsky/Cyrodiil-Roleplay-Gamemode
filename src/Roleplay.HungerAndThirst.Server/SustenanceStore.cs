using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>
/// The meters of every character the server has seen, keyed by ReadyM account id (a PlayerId only lasts one
/// connection), in <see cref="SustenanceConfig.SaveFile"/> next to the server DLL. Read once at start, written by
/// <see cref="Save"/>. Writes go to a temporary file first, so a crash mid-write never leaves a half file behind.
/// </summary>
[Service]
public sealed partial class SustenanceStore(SustenanceConfig config, ILogger logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // The relay server is published AOT and switches reflection-based JSON off for the whole process. Naming a
        // resolver opts this file back in, the same way the SDK's own config reader does.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private readonly Dictionary<Guid, SavedMeters> _saved = [];
    private string? _path;
    private bool _dirty;

    /// <summary>The server mod folder. Set by the entry point before any service starts.</summary>
    public void UseFolder(string folder)
    {
        _path = Path.Combine(folder, config.SaveFile);
    }

    private void Start() => Load();

    // Internal so the tests can load a file without the SDK starting the service.
    internal void Load()
    {
        if (_path is null)
        {
            logger.LogError("HungerAndThirst: no mod folder, meters will not be saved.");
            return;
        }
        if (!File.Exists(_path))
        {
            logger.LogInformation("HungerAndThirst: no {File} yet, every character starts fresh.", config.SaveFile);
            return;
        }
        try
        {
            Dictionary<Guid, SavedMeters>? saved = JsonSerializer.Deserialize<Dictionary<Guid, SavedMeters>>(File.ReadAllText(_path), Json);
            foreach ((Guid account, SavedMeters meters) in saved ?? [])
                _saved[account] = meters;
            logger.LogInformation("HungerAndThirst: loaded meters for {Count} character(s) from {File}.", _saved.Count, config.SaveFile);
        }
        catch (Exception exception)
        {
            // Keep the broken file for a human to look at instead of overwriting it with an empty one.
            string broken = _path + ".broken";
            File.Copy(_path, broken, overwrite: true);
            logger.LogError(exception, "HungerAndThirst: {File} could not be read, copied it to {Broken} and starting fresh.", config.SaveFile, broken);
        }
    }

    public bool TryGet(Guid account, out SavedMeters meters) => _saved.TryGetValue(account, out meters);

    public void Put(Guid account, SavedMeters meters)
    {
        if (_saved.TryGetValue(account, out SavedMeters old) && old == meters)
            return;
        _saved[account] = meters;
        _dirty = true;
    }

    /// <summary>Writes the file if anything changed since the last write. Never throws.</summary>
    public void Save()
    {
        if (!_dirty || _path is null)
            return;
        try
        {
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_saved, Json));
            File.Move(temporary, _path, overwrite: true);
            _dirty = false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "HungerAndThirst: saving {File} failed, will try again.", config.SaveFile);
        }
    }
}
