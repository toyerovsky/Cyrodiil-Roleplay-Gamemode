using Microsoft.EntityFrameworkCore.Design;

namespace CyrodiilRP.Data;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<CyrodiilContext>
{
    public CyrodiilContext CreateDbContext(string[] args) =>
        new CyrodiilContextFactory(Environment.GetEnvironmentVariable(CyrodiilContextFactory.ConnectionVariable)
                                   ?? CyrodiilContextFactory.LocalConnectionString).Create();
}
