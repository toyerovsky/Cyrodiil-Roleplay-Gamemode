namespace CyrodiilRP.Server.Infrastructure;

internal sealed record NamedWork(string Name, Action Run);
