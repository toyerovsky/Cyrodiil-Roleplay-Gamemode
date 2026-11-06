namespace CyrodiilRP.Server.Bank;

internal static class BankRules
{
    // The gold one deposit or one withdrawal moves at the most. A larger sum takes more than one.
    public const int MostAtOnce = 1_000_000;

    public static bool Fits(int amount) => amount is >= 1 and <= MostAtOnce;
}
