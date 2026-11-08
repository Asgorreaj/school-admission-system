using System.Text.RegularExpressions; // student id check
using Microsoft.EntityFrameworkCore; // EF core
using SchoolAdmission.Api.Data; // DbContext
using SchoolAdmission.Api.Models; // models

namespace SchoolAdmission.Api.Services; // service namespace

public record AdmissionOptions(int HoldDurationSeconds); // hold-er meyad (second)

public class AdmissionService(AppDbContext db, AdmissionOptions opt) // sob business logic
{
    private static readonly Regex StudentRx = new(@"^\d{17}$"); // thik 17 ongko

    public static string RequireStudent(string? id) // header validate
    {
        var v = id?.Trim(); // trim
        if (string.IsNullOrEmpty(v)) throw new AppException(400, "X-Student-Id header is required"); // nai hole 400
        if (!StudentRx.IsMatch(v)) throw new AppException(400, "X-Student-Id must be a 17-digit birth registration number"); // vul format 400
        return v; // valid
    }

    private static string NewId(string prefix) => prefix + Guid.NewGuid().ToString("N"); // c_xxx / h_xxx

    private static ClassDto ToClassDto(ClassRoom c, int held, int conf) => // class -> DTO
        new(c.Id, c.Name, c.TotalSeats, held, conf, Math.Max(0, c.TotalSeats - held - conf)); // available = total - held - confirmed

    private static string Effective(Hold h, DateTime now) => // dekhanor status
        (h.Status == HoldStatus.Held && h.ExpiresAt <= now ? HoldStatus.Expired : h.Status).ToString().ToUpperInvariant(); // meyad shesh hole EXPIRED

    private static HoldDto ToHoldDto(Hold h, string className, DateTime now) => // hold -> DTO
        new(h.Id, h.ClassId, className, h.StudentId, Effective(h, now), h.ExpiresAt, now); // DTO

    private async Task<(int Held, int Confirmed)> CountsAsync(string classId, DateTime now) // seat gonona
    {
        var held = await db.Holds.CountAsync(h => h.ClassId == classId && h.Status == HoldStatus.Held && h.ExpiresAt > now); // meyad shesh hoy nai emon hold
        var conf = await db.Holds.CountAsync(h => h.ClassId == classId && h.Status == HoldStatus.Confirmed); // confirmed
        return (held, conf); // return
    }

    private async Task<ClassRoom> LockClassAsync(string id) // class row lock
    {
        var list = await db.Classes.FromSqlInterpolated($"SELECT * FROM \"Classes\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(); // FOR UPDATE: onno transaction (even onno process) opekkha korbe
        return list.FirstOrDefault() ?? throw new AppException(404, "Class not found"); // na pele 404
    }

    private async Task<Hold?> LockHoldAsync(string id) => // hold row lock
        (await db.Holds.FromSqlInterpolated($"SELECT * FROM \"Holds\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync()).FirstOrDefault(); // lock shoho ana

    public async Task<ClassDto> CreateClassAsync(CreateClassRequest r) // class toiri
    {
        var name = r.Name?.Trim(); // nam trim
        if (string.IsNullOrEmpty(name) || name.Length > 100) throw new AppException(400, "name is required (max 100 characters)"); // nam validate
        if (r.TotalSeats < 1 || r.TotalSeats > 100000) throw new AppException(400, "totalSeats must be between 1 and 100000"); // seat validate
        var c = new ClassRoom { Id = NewId("c_"), Name = name, TotalSeats = r.TotalSeats, CreatedAt = DateTime.UtcNow }; // notun class
        db.Classes.Add(c); // add
        await db.SaveChangesAsync(); // save
        return ToClassDto(c, 0, 0); // notun class-e hold nai
    }

    public async Task<List<ClassDto>> ListClassesAsync() // sob class
    {
        var now = DateTime.UtcNow; // ekhon
        var classes = await db.Classes.AsNoTracking().OrderBy(c => c.CreatedAt).ToListAsync(); // class gulo
        var rows = await db.Holds.AsNoTracking() // count ek query-te
            .Where(h => h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > now)) // valid hold
            .GroupBy(h => new { h.ClassId, h.Status }) // group
            .Select(g => new { g.Key.ClassId, g.Key.Status, N = g.Count() }).ToListAsync(); // count
        return classes.Select(c => ToClassDto(c, // DTO
            rows.Where(r => r.ClassId == c.Id && r.Status == HoldStatus.Held).Sum(r => r.N), // held
            rows.Where(r => r.ClassId == c.Id && r.Status == HoldStatus.Confirmed).Sum(r => r.N))).ToList(); // confirmed
    }

    public async Task<ClassDto> GetClassAsync(string id) // ekta class
    {
        var c = await db.Classes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new AppException(404, "Class not found"); // khuji
        var (h, f) = await CountsAsync(id, DateTime.UtcNow); // count
        return ToClassDto(c, h, f); // DTO
    }

    public async Task<HoldDto> CreateHoldAsync(string classId, string? studentHeader) // abedon (seat hold)
    {
        var sid = RequireStudent(studentHeader); // student id
        await using var tx = await db.Database.BeginTransactionAsync(); // transaction
        var cls = await LockClassAsync(classId); // class lock: ek shomoy ekjon-i dhukte pare
        var now = DateTime.UtcNow; // ekhon
        await db.Holds.Where(h => h.ClassId == classId && h.StudentId == sid && h.Status == HoldStatus.Held && h.ExpiresAt <= now) // ei student-er meyad-shesh hold
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.Expired)); // Expired kore dei (unique index-er jonno)
        if (await db.Holds.AnyAsync(h => h.ClassId == classId && h.StudentId == sid && h.Status == HoldStatus.Confirmed)) // age-i bhorti?
            throw new AppException(409, "This student is already admitted to this class"); // 409
        if (await db.Holds.AnyAsync(h => h.ClassId == classId && h.StudentId == sid && h.Status == HoldStatus.Held)) // active abedon ache?
            throw new AppException(409, "This student already has an active application for this class"); // 409
        var (held, conf) = await CountsAsync(classId, now); // seat count
        if (held + conf >= cls.TotalSeats) throw new AppException(409, "No seats available"); // seat nai
        var hold = new Hold { Id = NewId("h_"), ClassId = classId, StudentId = sid, CreatedAt = now, ExpiresAt = now.AddSeconds(opt.HoldDurationSeconds), Status = HoldStatus.Held }; // notun hold
        db.Holds.Add(hold); // add
        await db.SaveChangesAsync(); // save
        await tx.CommitAsync(); // commit, lock chhere jay
        return ToHoldDto(hold, cls.Name, now); // return
    }

    public async Task<HoldDto> GetHoldAsync(string id) // hold details (header lagbe na)
    {
        var h = await db.Holds.AsNoTracking().Include(x => x.Class).FirstOrDefaultAsync(x => x.Id == id) ?? throw new AppException(404, "Hold not found"); // khuji
        return ToHoldDto(h, h.Class!.Name, DateTime.UtcNow); // DTO
    }

    public async Task<List<HoldDto>> ListMyHoldsAsync(string? studentHeader) // /me/holds
    {
        var sid = RequireStudent(studentHeader); // student id
        var list = await db.Holds.AsNoTracking().Include(h => h.Class).Where(h => h.StudentId == sid).OrderByDescending(h => h.CreatedAt).ToListAsync(); // nijer hold
        var now = DateTime.UtcNow; // ekhon
        return list.Select(h => ToHoldDto(h, h.Class!.Name, now)).ToList(); // DTO
    }

    public async Task<HoldDto> ConfirmHoldAsync(string id, string? studentHeader) // fee joma, bhorti confirm
    {
        var sid = RequireStudent(studentHeader); // student id
        await using var tx = await db.Database.BeginTransactionAsync(); // transaction
        var hold = await LockHoldAsync(id) ?? throw new AppException(404, "Hold not found"); // hold lock
        if (hold.StudentId != sid) throw new AppException(403, "This hold belongs to another student"); // onner hold
        var cname = await db.Classes.Where(c => c.Id == hold.ClassId).Select(c => c.Name).FirstAsync(); // class nam
        var now = DateTime.UtcNow; // ekhon
        if (hold.Status == HoldStatus.Confirmed) return ToHoldDto(hold, cname, now); // idempotent: ager result, count barbe na
        if (hold.Status == HoldStatus.Cancelled) throw new AppException(409, "This application was cancelled"); // cancelled
        if (hold.Status == HoldStatus.Expired || hold.ExpiresAt <= now) throw new AppException(409, "This application has expired"); // meyad shesh
        hold.Status = HoldStatus.Confirmed; // confirm
        hold.ConfirmedAt = now; // somoy
        await db.SaveChangesAsync(); // save
        await tx.CommitAsync(); // commit
        return ToHoldDto(hold, cname, now); // return
    }

    public async Task CancelHoldAsync(string id, string? studentHeader) // abedon batil
    {
        var sid = RequireStudent(studentHeader); // student id
        await using var tx = await db.Database.BeginTransactionAsync(); // transaction
        var hold = await LockHoldAsync(id) ?? throw new AppException(404, "Hold not found"); // lock
        if (hold.StudentId != sid) throw new AppException(403, "This hold belongs to another student"); // onner hold
        if (hold.Status == HoldStatus.Cancelled) return; // age-i batil: idempotent
        if (hold.Status == HoldStatus.Confirmed) throw new AppException(409, "A confirmed admission cannot be cancelled"); // confirmed
        if (hold.Status == HoldStatus.Expired || hold.ExpiresAt <= DateTime.UtcNow) throw new AppException(409, "This application has already expired"); // expired
        hold.Status = HoldStatus.Cancelled; // batil
        await db.SaveChangesAsync(); // save
        await tx.CommitAsync(); // commit
    }
}