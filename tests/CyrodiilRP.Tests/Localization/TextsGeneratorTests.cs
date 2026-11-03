using CyrodiilRP.Tests.Fakes;
using CyrodiilRP.Texts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CyrodiilRP.Tests.Localization;

public sealed class TextsGeneratorTests
{
    [Fact]
    public void A_group_becomes_an_internal_class_with_a_property_or_a_method_per_key()
    {
        var run = Run(new LocaleText("en", "chat", """{ "chat.loading": "Loading.", "chat.say": "{name} says: {text}" }"""));

        Assert.Empty(run.Diagnostics);
        var source = Assert.Single(run.GeneratedTrees).ToString();
        Assert.Contains("internal static partial class ChatText", source, StringComparison.Ordinal);
        Assert.Contains("public static Phrase Loading { get; }", source, StringComparison.Ordinal);
        Assert.Contains("public static Phrase Say(object? name, object? text)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_outside_its_group_is_reported_on_its_own_line()
    {
        var run = Run(new LocaleText("en", "chat", "{\n  \"chat.a\": \"A\",\n  \"chat.b\": \"B\",\n  \"economy.c\": \"C\"\n}"));

        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("CRT002", diagnostic.Id);
        Assert.Equal(3, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }

    [Theory]
    [InlineData("{ \"chat.a\": \"A\" } trailing")]
    [InlineData("{ \"chat.a\": \"A\\")]
    [InlineData("{ \"chat.a\": \"\\u12\" }")]
    public void A_broken_file_is_reported_and_not_thrown(string json)
    {
        var run = Run(new LocaleText("en", "chat", json), new LocaleText("en", "economy", """{ "economy.a": "A" }"""));

        Assert.Equal("CRT001", Assert.Single(run.Diagnostics).Id);
    }

    [Fact]
    public void A_translation_missing_a_text_or_a_placeholder_is_reported()
    {
        var run = Run(
            new LocaleText("en", "chat", """{ "chat.say": "{name} says: {text}", "chat.loading": "Loading." }"""),
            new LocaleText("pl", "chat", """{ "chat.say": "{name} mówi" }"""));

        Assert.Equal(["CRT003", "CRT005"], run.Diagnostics.Select(d => d.Id).Order(StringComparer.Ordinal));
    }

    private static GeneratorDriverRunResult Run(params AdditionalText[] files)
    {
        var driver = CSharpGeneratorDriver.Create(new TextsGenerator()).AddAdditionalTexts([.. files]);
        return driver.RunGenerators(CSharpCompilation.Create("texts")).GetRunResult();
    }
}
