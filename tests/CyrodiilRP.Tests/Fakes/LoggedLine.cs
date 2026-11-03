using Microsoft.Extensions.Logging;

namespace CyrodiilRP.Tests.Fakes;

internal sealed record LoggedLine(LogLevel Level, string Text, Exception? Exception);
