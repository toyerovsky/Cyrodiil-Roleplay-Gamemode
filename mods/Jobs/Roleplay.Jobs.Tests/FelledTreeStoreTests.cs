using Microsoft.Extensions.Logging;
using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class FelledTreeStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("jobs-tests-").FullName;
    private readonly JobsConfig _config = new();
    private readonly FailOnErrorLogger _logger = new();

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Felled_trees_survive_a_restart_with_their_regrow_time()
    {
        var growsBack = new DateTime(2026, 11, 2, 12, 30, 0, DateTimeKind.Utc);
        var oak = new FelledTree("/Game/Trees/SM_Oak.SM_Oak", 100, -200, 30, growsBack, 1.25f, -135f);
        var pine = new FelledTree("/Game/Trees/SM_Pine.SM_Pine", -5, 7, 0, growsBack.AddMinutes(5), 0.8f, 10f);

        var first = Store();
        first.Add(oak);
        first.Add(pine);
        first.Remove(pine.Id);

        var second = Store();
        Assert.Equal([oak], second.Felled);
        Assert.Equal(DateTimeKind.Utc, second.Felled.Single().GrowsBackUtc.Kind);
    }

    [Fact]
    public void An_unreadable_file_is_kept_aside_and_every_tree_stands()
    {
        var path = Path.Combine(_folder, _config.SaveFile);
        File.WriteAllText(path, "{ not json");

        var store = Store();

        Assert.Empty(store.Felled);
        Assert.Equal("{ not json", File.ReadAllText(path + ".broken"));
        Assert.Contains(_logger.Warnings, warning => warning.Contains(".broken"));
    }

    [Fact]
    public void A_file_from_before_scale_and_fall_were_saved_reads_as_scale_one()
    {
        File.WriteAllText(Path.Combine(_folder, _config.SaveFile),
            "[{ \"Mesh\": \"/Game/Trees/SM_Oak.SM_Oak\", \"X\": 1, \"Y\": 2, \"Z\": 3, \"GrowsBackUtc\": \"2026-11-02T12:30:00Z\" }]");

        var tree = Store().Felled.Single();

        Assert.Equal(0f, tree.Scale);
        Assert.Equal(1f, tree.TreeScale);
        Assert.Equal(0f, tree.FallYaw);
    }

    [Fact]
    public void No_file_means_no_felled_trees()
    {
        Assert.Empty(Store().Felled);
        Assert.Empty(_logger.Warnings);
    }

    private FelledTreeStore Store()
    {
        var store = new FelledTreeStore(_config, _logger);
        store.UseFolder(_folder);
        return store;
    }

    internal sealed class FailOnErrorLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (logLevel >= LogLevel.Error)
                throw new InvalidOperationException($"Logged at {logLevel}: {message}", exception);
            if (logLevel == LogLevel.Warning)
                Warnings.Add(message);
        }
    }
}
