namespace CyrodiilRP.Server.Localization;

// The CLDR categories, written out because the server may run without ICU.
internal static class PluralRules
{
    public static string PickCategory(string locale, decimal n)
    {
        var isInt = n == decimal.Truncate(n);
        var i = isInt ? (long)decimal.Truncate(decimal.Abs(n)) : -1;
        return locale switch
        {
            "pl" when !isInt => "other",
            "pl" when i == 1 => "one",
            "pl" when i % 10 is >= 2 and <= 4 && i % 100 is < 12 or > 14 => "few",
            "pl" => "many",

            _ when isInt && i == 1 => "one",
            _ => "other",
        };
    }
}
