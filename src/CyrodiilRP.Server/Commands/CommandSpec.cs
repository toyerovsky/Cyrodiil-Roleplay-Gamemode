using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Commands;

internal sealed record CommandSpec
{
    public required string Name { get; init; }

    // "/name <arguments> - what it does": the menu's help shows the two halves apart.
    public required Phrase Usage { get; init; }

    public required CommandCategory Category { get; init; }

    public string[] Aliases { get; init; } = [];

    // A staff command needs the permission of its own name, so adding a command never needs a new permission.
    public bool Restricted => Category == CommandCategory.Staff;

    // A staff command that only shows or talks, which the staff run off duty too.
    public bool OffDuty { get; init; }

    public bool NeedsCharacter { get; init; }

    // Said in character or out: a mute holds it back, and as a mute is held by the character, it needs one.
    public bool Speaks { get; init; }
}
