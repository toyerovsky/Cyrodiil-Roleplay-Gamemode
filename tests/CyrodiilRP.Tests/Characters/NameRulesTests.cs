using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Tests.Characters;

public sealed class NameRulesTests
{
    [Theory]
    [InlineData("marius", "Marius")]
    [InlineData("  VARRO ", "Varro")]
    [InlineData("gro-malog", "Gro-Malog")]
    [InlineData("RA'JIRR", "Ra'jirr")]
    [InlineData("żaneta", "Żaneta")]
    public void A_name_is_kept_with_a_capital_at_the_start_and_after_a_hyphen(string typed, string kept) =>
        Assert.Equal(kept, NameRules.Normalize(typed));

    [Theory]
    [InlineData("Marius")]
    [InlineData("Gro-Malog")]
    [InlineData("Ra'jirr")]
    [InlineData("Ål")]
    [InlineData("Żaneta")]
    public void Letters_joined_by_single_apostrophes_or_hyphens_are_a_name(string name) =>
        Assert.Null(NameRules.Check(name, CharactersText.FirstName));

    [Fact]
    public void A_name_of_the_longest_length_is_a_name() =>
        Assert.Null(NameRules.Check("A" + new string('b', NameRules.MaxLength - 1), CharactersText.FirstName));

    [Fact]
    public void A_name_too_short_or_too_long_is_refused_with_its_bounds()
    {
        foreach (var name in new[] { new string('M', NameRules.MinLength - 1), "A" + new string('b', NameRules.MaxLength) })
        {
            var problem = NameRules.Check(name, CharactersText.LastName);

            Assert.NotNull(problem);
            Assert.Equal("characters.name_length", problem.Key);
            Assert.Contains(problem.Args, arg => arg is { Name: "min", Value: NameRules.MinLength });
            Assert.Contains(problem.Args, arg => arg is { Name: "max", Value: NameRules.MaxLength });
        }
    }

    // A Cyrillic "а" and a Greek "ο" look like Latin letters, so the name would pass for another.
    [Theory]
    [InlineData("M\u0430rius")]
    [InlineData("Varr\u03BF")]
    public void A_letter_of_another_script_is_refused(string name) =>
        Assert.Equal("characters.name_shape", NameRules.Check(name, CharactersText.FirstName)?.Key);

    [Theory]
    [InlineData("Mar1us")]
    [InlineData("-Marius")]
    [InlineData("Marius-")]
    [InlineData("Ma--rius")]
    [InlineData("Ma'-rius")]
    [InlineData("Mar ius")]
    public void Anything_but_letters_joined_by_single_marks_is_refused(string name) =>
        Assert.Equal("characters.name_shape", NameRules.Check(name, CharactersText.FirstName)?.Key);

    [Theory]
    [InlineData("Admin")]
    [InlineData("MODERATOR")]
    [InlineData("Ooc")]
    public void A_name_that_sounds_like_a_role_on_the_server_is_refused(string name) =>
        Assert.Equal("characters.name_reserved", NameRules.Check(name, CharactersText.FirstName)?.Key);

    [Fact]
    public void The_refusal_names_the_part_of_the_name_it_is_about()
    {
        var problem = NameRules.Check("M", CharactersText.LastName);

        Assert.Contains(problem!.Args, arg => arg.Name == "part" && ReferenceEquals(arg.Value, CharactersText.LastName));
    }
}
