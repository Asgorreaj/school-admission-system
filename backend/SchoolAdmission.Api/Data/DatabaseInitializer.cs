using System.Net; // DNS lookup
using Microsoft.EntityFrameworkCore; // EF core
using Npgsql; // connection string parser

namespace SchoolAdmission.Api.Data; // data namespace

public static class DatabaseInitializer // startup DB init with retry
{
    public static async Task InitAsync(IServiceProvider sp, string connString, ILogger log, CancellationToken ct = default) // entry point
    {
        var b = new NpgsqlConnectionStringBuilder(connString); // parse (password is never logged)
        log.LogInformation("DB init: host={Host} port={Port} db={Db} user={User}", b.Host, b.Port, b.Database, b.Username); // what we connect to
        const int maxAttempts = 30; // about 2.5 minutes total
        for (var attempt = 1; ; attempt++) // retry loop
        {
            try // one attempt
            {
                using var scope = sp.CreateScope(); // scope for DbContext
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); // DbContext
                await db.Database.OpenConnectionAsync(ct); // one connection for lock + create
                try // keep lock and create together
                {
                    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(42)", ct); // only one process creates tables
                    await db.Database.EnsureCreatedAsync(ct); // no-op if tables already exist
                    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(42)", ct); // release lock
                }
                finally { await db.Database.CloseConnectionAsync(); } // close (pool reset also frees the lock on errors)
                log.LogInformation("Database ready (attempt {Attempt})", attempt); // success
                return; // done
            }
            catch (Exception ex) when (attempt < maxAttempts && !ct.IsCancellationRequested) // not the last attempt
            {
                log.LogWarning("DB init attempt {Attempt}/{Max} failed: {Type}: {Msg}", attempt, maxAttempts, ex.GetType().Name, ex.InnerException?.Message ?? ex.Message); // reason
                await LogDnsAsync(b.Host, log); // DNS diagnostics
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 5)), ct); // 1s, 2s ... max 5s
            }
        }
    }

    private static async Task LogDnsAsync(string host, ILogger log) // shows whether name resolution works
    {
        if (IPAddress.TryParse(host, out _)) return; // already an IP
        try // lookup
        {
            var addrs = await Dns.GetHostAddressesAsync(host); // resolve
            log.LogWarning("DNS: {Host} resolves to {Addrs}", host, string.Join(", ", addrs.Select(a => a.ToString()))); // result
        }
        catch (Exception ex) { log.LogWarning("DNS: lookup of {Host} failed: {Type}: {Msg}", host, ex.GetType().Name, ex.Message); } // resolution failed
    }
}