using Microsoft.EntityFrameworkCore; // EF core
using SchoolAdmission.Api.Data; // DbContext
using SchoolAdmission.Api.Models; // models

namespace SchoolAdmission.Api.Services; // service namespace

public class HoldExpiryWorker(IServiceScopeFactory sf, ILogger<HoldExpiryWorker> log) : BackgroundService // background-e meyad-shesh hold Expired mark kore
{
    protected override async Task ExecuteAsync(CancellationToken ct) // loop
    {
        while (!ct.IsCancellationRequested) // jotokkhon app chole
        {
            try // error hole worker thambe na
            {
                using var scope = sf.CreateScope(); // notun scope
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); // DbContext
                var now = DateTime.UtcNow; // ekhon
                await db.Holds.Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= now) // meyad-shesh hold
                    .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.Expired), ct); // Expired kori
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Expiry sweep failed"); } // log
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } // 5 second opekkha
            catch (OperationCanceledException) { } // shutdown
        }
    }
}