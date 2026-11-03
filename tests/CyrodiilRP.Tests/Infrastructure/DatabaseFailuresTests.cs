using CyrodiilRP.Server.Infrastructure;
using Npgsql;

namespace CyrodiilRP.Tests.Infrastructure;

public sealed class DatabaseFailuresTests
{
    [Fact]
    public void A_database_that_is_not_up_counts_as_EF_reports_it()
    {
        var refused = new NpgsqlException("Failed to connect to 127.0.0.1:5433");

        Assert.True(DatabaseFailures.Is(refused));
        Assert.True(DatabaseFailures.Is(new InvalidOperationException("An exception has been raised that is likely due to a transient failure.", refused)));
    }

    [Fact]
    public void A_mistake_in_the_code_is_not_one()
    {
        Assert.False(DatabaseFailures.Is(new InvalidOperationException("Sequence contains no elements")));
    }
}
