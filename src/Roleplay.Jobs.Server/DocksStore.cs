using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The crate deposits and foremen placed in game, in <see cref="DocksConfig.SaveFile"/> next to the server DLL. Written
/// whole on every change, through a temporary file.
/// </summary>
[Service]
public sealed partial class DocksStore(JobsConfig config, ILogger logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // The relay is published AOT with reflection JSON off; naming a resolver opts this file back in.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private DocksSave _save = new();
    private string? _path;

    public IReadOnlyList<DockZone> Zones => _save.Zones;
    public IReadOnlyList<Foreman> Foremen => _save.Foremen;

    /// <summary>The crate's size along its own axes, as a client measured it; zero before one has.</summary>
    public Vector3 CrateSize => new(_save.CrateX, _save.CrateY, _save.CrateZ);

    /// <summary>How far the crate's bottom is above its origin, as a client measured it; zero before one has.</summary>
    public float CrateBottom => _save.CrateBottom;

    public void SetCrate(Vector3 size, float bottom)
    {
        _save.CrateX = size.X;
        _save.CrateY = size.Y;
        _save.CrateZ = size.Z;
        _save.CrateBottom = bottom;
        Save();
    }

    /// <summary>The pallet board's box at scale 1 around its origin, as a client measured it; zero before one has.</summary>
    public Vector3 BoardMin => new(_save.BoardMinX, _save.BoardMinY, _save.BoardMinZ);

    public Vector3 BoardMax => new(_save.BoardMaxX, _save.BoardMaxY, _save.BoardMaxZ);

    public void SetBoard(Vector3 min, Vector3 max)
    {
        _save.BoardMinX = min.X;
        _save.BoardMinY = min.Y;
        _save.BoardMinZ = min.Z;
        _save.BoardMaxX = max.X;
        _save.BoardMaxY = max.Y;
        _save.BoardMaxZ = max.Z;
        Save();
    }

    /// <summary>The server mod folder, read at once. Set by the entry point before any service starts.</summary>
    public void UseFolder(string folder)
    {
        _path = Path.Combine(folder, config.Docks.SaveFile);
        Load();
    }

    // Internal so the tests can load a file without the SDK starting the service
    internal void Load()
    {
        _save = new DocksSave();
        if (_path is null || !File.Exists(_path))
            return;

        try
        {
            _save = JsonSerializer.Deserialize<DocksSave>(File.ReadAllText(_path), Json) ?? new DocksSave();
            logger.LogInformation("Jobs: {Zones} crate deposits and {Foremen} foremen loaded from {Path}.", _save.Zones.Count, _save.Foremen.Count, _path);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            var broken = _path + ".broken";
            File.Copy(_path, broken, overwrite: true);
            logger.LogWarning(exception, "Jobs: could not read {Path}, copied it to {Broken}; no deposits, no foremen.", _path, broken);
        }
    }

    public void Add(DockZone zone)
    {
        _save.Zones.Add(zone);
        Save();
    }

    public void Add(Foreman foreman)
    {
        _save.Foremen.Add(foreman);
        Save();
    }

    public void Replace(Foreman foreman)
    {
        var index = _save.Foremen.FindIndex(other => other.Id == foreman.Id);
        if (index < 0)
            throw new InvalidOperationException($"Jobs: no foreman {foreman.Id} to replace.");
        _save.Foremen[index] = foreman;
        Save();
    }

    public bool Remove(string id)
    {
        var removed = _save.Zones.RemoveAll(zone => zone.Id == id) + _save.Foremen.RemoveAll(foreman => foreman.Id == id) > 0;
        if (removed)
            Save();
        return removed;
    }

    private void Save()
    {
        if (_path is null)
        {
            logger.LogError("Jobs: the docks cannot be saved, the store was never given its folder.");
            return;
        }

        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_save, Json));
        File.Move(temporary, _path, overwrite: true);
    }
}
