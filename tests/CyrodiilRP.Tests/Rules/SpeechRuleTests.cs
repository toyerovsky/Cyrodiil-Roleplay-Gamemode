using CyrodiilRP.Server;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Conditions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Rules;

// A mute keeps a character from speaking, by a command or by plain words, and a freeze does not.
public sealed class SpeechRuleTests
{
    private const string Speech = "talk";
    private const string Silencing = "test.silencing";
    private const string Holding = "test.holding";

    private static readonly Phrase Silenced = new("test.silenced");

    private readonly CharacterConditions _conditions = new(null!, null!, null!);
    private readonly CommandRegistry _commands;
    private readonly RecordedOutput _output = new();
    private readonly List<string> _ran = [];

    public SpeechRuleTests()
    {
        _conditions.Define(Silencing, new Phrase(Silencing), _ => Silenced);
        _conditions.Define(Holding, new Phrase(Holding));
        _commands = new CommandRegistry(null!, new StaffRoles(null!, new RpConfig()), _conditions, new ListLogger());
        _commands.Add(new CommandSpec { Name = Speech, Usage = new Phrase($"test.{Speech}"), Category = CommandCategory.Roleplay, Speaks = true },
            _ => _ran.Add(Speech));
    }

    [Fact]
    public void Speech_needs_a_character()
    {
        var player = CreatePlayer(withCharacter: false);

        _commands.Run(player, $"/{Speech}", _output);

        Assert.Empty(_ran);
        Assert.Equal([ChatText.NeedCharacter.Key], _output.Refusals);
        Assert.Equal(ChatText.NeedCharacter.Key, _commands.FindSpeechRefusal(player)?.Key);
    }

    [Fact]
    public void A_condition_that_silences_refuses_speech_with_its_own_words_by_a_command_or_without_one()
    {
        var player = CreatePlayer(Silencing);

        _commands.Run(player, $"/{Speech}", _output);

        Assert.Empty(_ran);
        Assert.Equal([Silenced.Key], _output.Refusals);
        Assert.Equal(Silenced.Key, _commands.FindSpeechRefusal(player)?.Key);
        Assert.Equal([Silencing], _conditions.ListSilencing(player).Select(c => c.Key));
    }

    [Fact]
    public void A_condition_that_does_not_silence_lets_the_character_speak()
    {
        var player = CreatePlayer(Holding);

        _commands.Run(player, $"/{Speech}", _output);

        Assert.Equal([Speech], _ran);
        Assert.Null(_commands.FindSpeechRefusal(player));
        Assert.Empty(_conditions.ListSilencing(player));
    }

    private static OnlinePlayer CreatePlayer(string? condition = null, bool withCharacter = true)
    {
        var player = new OnlinePlayer(default, Guid.NewGuid(), DateTimeOffset.UtcNow) { IsLoaded = true };
        if (!withCharacter)
            return player;

        player.Character = new CharacterRecord(1, "Marius", "Varro", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0);
        if (condition is not null)
            player.Character.Conditions[condition] = new ActiveCondition(condition, DateTimeOffset.UtcNow, null, null, null);

        return player;
    }
}
