namespace CyrodiilRP.Tests.Database;

// Character names are unique in a class's database, which its tests share, so each test makes its own.
internal static class Names
{
    private const string Letters = "bcdfghjklmnpstvz";
    private const int Length = 8;

    public static string CreateUnique()
    {
        var letters = Random.Shared.GetItems(Letters.AsSpan(), Length);
        letters[0] = char.ToUpperInvariant(letters[0]);
        return new string(letters);
    }
}
