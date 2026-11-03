using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Characters;

// A name given in the form, normalized, before the game's creator closes and makes it a character.
internal sealed record ReservedName(string FirstName, string LastName)
{
    public string FullName => $"{FirstName} {LastName}";

    public string Key => Character.MakeNameKey(FirstName, LastName);
}
