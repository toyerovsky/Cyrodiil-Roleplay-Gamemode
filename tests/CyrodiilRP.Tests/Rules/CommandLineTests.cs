using CyrodiilRP.Server.Commands;

namespace CyrodiilRP.Tests.Rules;

public sealed class CommandLineTests
{
    [Theory]
    [InlineData("/me waves", "me", "waves")]
    [InlineData("/help", "help", "")]
    [InlineData("/pm 3   hello   there  ", "pm", "3   hello   there")]
    [InlineData("/ROLL 6", "ROLL", "6")]
    public void A_line_starting_with_a_slash_is_a_name_and_the_rest(string line, string name, string arguments)
    {
        Assert.True(CommandLine.TryParse(line, out var parsedName, out var parsedArguments));
        Assert.Equal(name, parsedName);
        Assert.Equal(arguments, parsedArguments);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("/")]
    [InlineData("/ me")]
    [InlineData("")]
    public void Anything_else_is_no_command(string line) =>
        Assert.False(CommandLine.TryParse(line, out _, out _));

    [Theory]
    [InlineData("3 hello there", 1, "hello there")]
    [InlineData("3   hello  there", 1, "hello  there")]
    [InlineData("3 Marius Varro", 2, "Varro")]
    [InlineData("3", 1, "")]
    [InlineData("anything", 0, "anything")]
    public void The_rest_after_some_words_keeps_its_own_spacing(string arguments, int skip, string rest) =>
        Assert.Equal(rest, CommandLine.SkipWords(arguments, skip));

    [Fact]
    public void Minutes_are_a_duration_up_to_the_most()
    {
        Assert.True(CommandLine.TryMinutes("30", out var duration));
        Assert.Equal(TimeSpan.FromMinutes(30), duration);
        Assert.True(CommandLine.TryMinutes($"{CommandLine.MaxMinutes}", out var longest));
        Assert.Equal(TimeSpan.FromMinutes(CommandLine.MaxMinutes), longest);
        Assert.False(CommandLine.TryMinutes($"{CommandLine.MaxMinutes + 1}", out _));
    }

    [Fact]
    public void Zero_minutes_is_until_lifted()
    {
        Assert.True(CommandLine.TryMinutes("0", out var duration));
        Assert.Null(duration);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("1,000")]
    [InlineData("1e3")]
    [InlineData("cheating")]
    public void Anything_else_is_no_minutes(string word) => Assert.False(CommandLine.TryMinutes(word, out _));

    [Theory]
    [InlineData("50", 50)]
    [InlineData("-50", -50)]
    public void A_number_may_be_negative(string word, int value)
    {
        Assert.True(CommandLine.TryNumber(word, out var parsed));
        Assert.Equal(value, parsed);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1 000")]
    [InlineData(" 5")]
    public void A_number_is_digits_whatever_the_culture(string word) => Assert.False(CommandLine.TryNumber(word, out _));

    [Theory]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("+3")]
    public void A_player_number_is_digits_in_range(string word) => Assert.False(CommandLine.TryPlayerNumber(word, out _));
}
