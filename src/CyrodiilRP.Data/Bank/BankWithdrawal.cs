namespace CyrodiilRP.Data.Bank;

// What a withdrawal came to. Done: the gold left the account. Balance: what the account holds after it, the same as
// before when there was too little.
public sealed record BankWithdrawal(bool Done, long Balance);
