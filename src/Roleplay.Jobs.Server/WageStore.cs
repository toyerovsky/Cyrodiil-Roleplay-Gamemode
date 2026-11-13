using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The crates each player has delivered and not been paid for, by their ReadyM account (one character to an account),
/// in <see cref="DocksConfig.WagesFile"/> next to the server DLL, so a restart owes them the same. Written whole on
/// every change, through a temporary file.
/// </summary>
[Service]
public sealed partial class WageStore(JobsConfig config, ILogger logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // The relay is published AOT with reflection JSON off; naming a resolver opts this file back in.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private Dictionary<Guid, int> _unpaid = [];
    private string? _path;

    /// <summary>The server mod folder, read at once. Set by the entry point before any service starts.</summary>
    public void UseFolder(string folder)
    {
        _path = Path.Combine(folder, config.Docks.WagesFile);
        Load();
    }

    // Internal so the tests can load a file without the SDK starting the service
    internal void Load()
    {
        _unpaid = [];
        if (_path is null || !File.Exists(_path))
            return;

        try
        {
            _unpaid = JsonSerializer.Deserialize<Dictionary<Guid, int>>(File.ReadAllText(_path), Json) ?? [];
            logger.LogInformation("Jobs: unpaid crates for {Count} players loaded from {Path}.", _unpaid.Count, _path);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            var broken = _path + ".broken";
            File.Copy(_path, broken, overwrite: true);
            logger.LogWarning(exception, "Jobs: could not read {Path}, copied it to {Broken}; nobody is owed anything.", _path, broken);
        }
    }

    public int Unpaid(Guid account) => _unpaid.GetValueOrDefault(account);

    /// <summary>One more crate delivered; how many are unpaid now.</summary>
    public int AddCrate(Guid account)
    {
        var unpaid = Unpaid(account) + 1;
        _unpaid[account] = unpaid;
        Save();
        return unpaid;
    }

    /// <summary>Every unpaid crate, now paid: how many there were.</summary>
    public int TakeAll(Guid account)
    {
        if (!_unpaid.Remove(account, out var unpaid))
            return 0;
        Save();
        return unpaid;
    }

    private void Save()
    {
        if (_path is null)
        {
            logger.LogError("Jobs: the wages cannot be saved, the store was never given its folder.");
            return;
        }

        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_unpaid, Json));
        File.Move(temporary, _path, overwrite: true);
    }
}
