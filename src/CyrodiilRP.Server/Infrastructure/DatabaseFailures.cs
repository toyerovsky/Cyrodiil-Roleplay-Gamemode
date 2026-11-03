using System.Data.Common;

namespace CyrodiilRP.Server.Infrastructure;

internal static class DatabaseFailures
{
    // EF wraps a connection it could not open in an InvalidOperationException ("likely due to a transient failure").
    public static bool Is(Exception exception) => exception is DbException || exception.InnerException is DbException;
}
