using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Lore;

public sealed class ShownNameTests
{
    private const string Label = "Dunmer";

    private static readonly Localizer Texts = Localizer.LoadFolder(Path.Combine(AppContext.BaseDirectory, "locales"), new ListLogger());

    private readonly OnlinePlayer _marius = CreatePlayer("Marius", "Varro");
    private readonly OnlinePlayer _lucia = CreatePlayer("Lucia", "Aurelia");

    [Fact]
    public void A_stranger_reads_the_race_and_one_who_knows_the_name_reads_it()
    {
        Assert.Equal(Label, ShownNames.Pick(hasCharacter: true, knowsName: false, _marius.Name, () => Label));
        Assert.Equal(_marius.Name, ShownNames.Pick(hasCharacter: true, knowsName: true, _marius.Name, () => Label));
    }

    // Without a character there is no race to show, and the number or nickname gives nothing away.
    [Fact]
    public void A_player_without_a_character_keeps_what_they_are_called()
    {
        Assert.Equal("[7]", ShownNames.Pick(hasCharacter: false, knowsName: false, "[7]", () => Label));
    }

    [Fact]
    public void A_line_names_each_speaker_as_its_reader_sees_them()
    {
        var line = ChatText.Greet(new ShownName(_marius), new ShownName(_lucia));

        var forLucia = ShownName.Resolve(line, subject => subject == _lucia ? subject.Name : Label);
        var forMarius = ShownName.Resolve(line, subject => subject.Name);

        Assert.Equal($"* {Label} greets Lucia Aurelia.", Texts.Get("en", forLucia));
        Assert.Equal("* Marius Varro greets Lucia Aurelia.", Texts.Get("en", forMarius));
    }

    [Fact]
    public void A_name_inside_a_phrase_inside_a_phrase_is_resolved_too()
    {
        var line = DiceText.RolledFor(new ShownName(_marius), "5d20", LoreFields.Get("trait.lazy").Label, "d20 [3]", 3);

        Assert.True(ShownName.IsIn(line));
        Assert.Equal($"* {Label} rolls 5d20 for Lazy: d20 [3] = 3.", Texts.Get("en", ShownName.Resolve(line, _ => Label)));
    }

    [Fact]
    public void A_text_without_a_name_stays_as_it_is()
    {
        var text = LoreText.TooFast;

        Assert.False(ShownName.IsIn(text));
        Assert.Same(text, ShownName.Resolve(text, _ => Label));
    }

    // A name nobody resolved would read as the record, so the translation refuses it.
    [Fact]
    public void A_name_left_unresolved_never_reaches_a_reader()
    {
        Assert.Throws<InvalidOperationException>(() => Texts.Get("en", ChatText.Say(new ShownName(_marius), "Hello.")));
    }

    private static OnlinePlayer CreatePlayer(string firstName, string lastName) =>
        new(default, Guid.NewGuid(), DateTimeOffset.UnixEpoch)
        {
            IsLoaded = true,
            Character = new CharacterRecord(1, firstName, lastName, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0),
        };
}
