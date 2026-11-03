using System.Linq.Expressions;

namespace CyrodiilRP.Data.Characters;

// A character under the name players and staff read. An expression, so the database builds the name where a query
// filters or sorts by it.
internal sealed class NamedCharacter
{
    public static readonly Expression<Func<Character, NamedCharacter>> From = c => new NamedCharacter
    {
        Id = c.Id,
        AccountId = c.AccountId,
        Name = c.FirstName + " " + c.LastName,
    };

    public long Id { get; init; }

    public long AccountId { get; init; }

    public required string Name { get; init; }
}
