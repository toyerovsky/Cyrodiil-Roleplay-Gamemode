using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CyrodiilRP.Tests.Fakes;

// A locale file as the compiler hands it to the texts generator.
internal sealed class LocaleText(string locale, string group, string json) : AdditionalText
{
    public override string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "locales", locale, group + ".json");

    public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(json, Encoding.UTF8);
}
