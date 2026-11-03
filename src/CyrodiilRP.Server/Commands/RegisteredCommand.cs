namespace CyrodiilRP.Server.Commands;

internal sealed record RegisteredCommand(CommandSpec Spec, CommandHandler Run);
