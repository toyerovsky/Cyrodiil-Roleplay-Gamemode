using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Sanctuary;

internal readonly record struct Verdict<T>(SanctuaryVerdict Kind, IReadOnlyList<T> Suspects);
