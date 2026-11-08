using Microsoft.EntityFrameworkCore; // EF core
using SchoolAdmission.Api.Data; // DbContext
using SchoolAdmission.Api.Models; // DTO
using SchoolAdmission.Api.Services; // service
using Xunit; // test framework

namespace SchoolAdmission.Tests; // test namespace

public class AdmissionTests : IAsyncLifetime // real Postgres-e test
{
    private static readonly string Conn = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION") ?? throw new InvalidOperationException("TEST_DB_CONNECTION is not set"); // connection env theke

    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options); // notun context
    private static AdmissionService NewSvc(int holdSeconds = 600) => new(NewDb(), new AdmissionOptions(holdSeconds)); // notun service
    private static string Sid(int i) => (10000000000000000L + i).ToString(); // 17 ongker student id

    public async Task InitializeAsync() // prottek test-er age
    {
        await using var db = NewDb(); // context
        await db.Database.EnsureCreatedAsync(); // table toiri
        await db.Database.ExecuteSqlRawAsync("TRUNCATE \"Holds\", \"Classes\" CASCADE"); // data muche felo
    }

    public Task DisposeAsync() => Task.CompletedTask; // kichu korar nai

    private static Task<ClassDto> MakeClass(int seats) => NewSvc().CreateClassAsync(new CreateClassRequest("Class 6 - Morning Shift", seats)); // helper: class toiri

    [Fact] // test 1
    public async Task Creates_hold_successfully() // successful hold
    {
        var c = await MakeClass(2); // 2 seat
        var h = await NewSvc().CreateHoldAsync(c.Id, Sid(1)); // hold
        Assert.Equal("HELD", h.Status); // HELD
        var after = await NewSvc().GetClassAsync(c.Id); // class abar
        Assert.Equal(1, after.HeldSeats); // 1 held
        Assert.Equal(1, after.AvailableSeats); // 1 available
    }

    [Fact] // test 2
    public async Task Expired_hold_releases_seat() // expiry
    {
        var s = NewSvc(1); // 1 second meyad
        var c = await MakeClass(1); // 1 seat
        var a = await s.CreateHoldAsync(c.Id, Sid(1)); // A hold nilo
        var full = await Assert.ThrowsAsync<AppException>(() => s.CreateHoldAsync(c.Id, Sid(2))); // B pabe na
        Assert.Equal(409, full.Status); // 409
        await Task.Delay(1600); // meyad shesh howar opekkha
        Assert.Equal("EXPIRED", (await s.GetHoldAsync(a.HoldId)).Status); // A expired
        var b = await s.CreateHoldAsync(c.Id, Sid(2)); // ekhon B pabe
        Assert.Equal("HELD", b.Status); // HELD
        Assert.Equal(1, (await NewSvc().GetClassAsync(c.Id)).HeldSeats); // shudhu B-er hold
        var late = await Assert.ThrowsAsync<AppException>(() => s.ConfirmHoldAsync(a.HoldId, Sid(1))); // expired confirm
        Assert.Equal(409, late.Status); // 409
    }

    [Fact] // test 3
    public async Task Confirmation_is_idempotent() // repeat confirm
    {
        var c = await MakeClass(3); // class
        var h = await NewSvc().CreateHoldAsync(c.Id, Sid(1)); // hold
        await NewSvc().ConfirmHoldAsync(h.HoldId, Sid(1)); // prothom confirm
        await NewSvc().ConfirmHoldAsync(h.HoldId, Sid(1)); // dwitiyo confirm
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => NewSvc().ConfirmHoldAsync(h.HoldId, Sid(1))))); // 20ta ek shathe
        var after = await NewSvc().GetClassAsync(c.Id); // class
        Assert.Equal(1, after.ConfirmedSeats); // ekbar-i barse
        Assert.Equal(0, after.HeldSeats); // held 0
    }

    [Fact] // test 4
    public async Task Concurrent_holds_never_exceed_total_seats() // concurrency
    {
        const int seats = 5; // total seat
        var c = await MakeClass(seats); // class
        var results = await Task.WhenAll(Enumerable.Range(0, 60).Select(i => Task.Run(async () => // 60 jon ek shathe
        {
            try { await NewSvc().CreateHoldAsync(c.Id, Sid(i)); return true; } // success
            catch (AppException) { return false; } // seat nai
        })));
        var final = await NewSvc().GetClassAsync(c.Id); // final state
        Assert.Equal(seats, results.Count(r => r)); // thik 5 jon
        Assert.True(final.HeldSeats + final.ConfirmedSeats <= seats); // limit cross hoy nai
        Assert.Equal(0, final.AvailableSeats); // available 0
    }

    [Fact] // test 5
    public async Task Same_student_cannot_hold_same_class_twice() // duplicate rule
    {
        var c = await MakeClass(10); // class
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(async () => // ek student 10 bar ek shathe
        {
            try { await NewSvc().CreateHoldAsync(c.Id, Sid(7)); return true; } // success
            catch (AppException) { return false; } // duplicate
        })));
        Assert.Equal(1, results.Count(r => r)); // shudhu 1 ta
        Assert.Equal(1, (await NewSvc().GetClassAsync(c.Id)).HeldSeats); // 1 held
    }

    [Fact] // test 6
    public async Task Invalid_student_id_returns_400() // validation
    {
        var c = await MakeClass(1); // class
        var ex = await Assert.ThrowsAsync<AppException>(() => NewSvc().CreateHoldAsync(c.Id, "abc")); // vul id
        Assert.Equal(400, ex.Status); // 400
    }
}