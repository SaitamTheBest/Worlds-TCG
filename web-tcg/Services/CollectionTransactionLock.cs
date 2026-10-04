using Microsoft.EntityFrameworkCore;
using web_tcg.Data;

namespace web_tcg.Services;

// PostgreSQL transaction lock shared by collection transfers and market settlement.
public static class CollectionTransactionLock
{
    public static Task AcquireAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction == null) throw new InvalidOperationException("Une transaction est nécessaire.");
        return context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(741203951)", cancellationToken);
    }
}
