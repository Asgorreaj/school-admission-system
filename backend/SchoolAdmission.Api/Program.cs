using Microsoft.EntityFrameworkCore; // EF core
using SchoolAdmission.Api.Data; // DbContext
using SchoolAdmission.Api.Services; // service

var builder = WebApplication.CreateBuilder(args); // builder
var conn = builder.Configuration["DB_CONNECTION"] ?? throw new InvalidOperationException("DB_CONNECTION is not set"); // DB connection env theke
var secs = int.TryParse(builder.Configuration["HOLD_DURATION_SECONDS"], out var s) && s > 0 ? s : 600; // meyad, default 600
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn)); // Postgres
builder.Services.AddSingleton(new AdmissionOptions(secs)); // options
builder.Services.AddScoped<AdmissionService>(); // service
builder.Services.AddHostedService<HoldExpiryWorker>(); // expiry worker
builder.Services.AddControllers(); // controllers
builder.Services.AddEndpointsApiExplorer(); // Swagger endpoint discovery
builder.Services.AddSwaggerGen(); // Swagger generator

var app = builder.Build(); // app toiri

app.Use(async (ctx, next) => // global error handler
{
    try { await next(); } // request cholao
    catch (AppException ex) { ctx.Response.StatusCode = ex.Status; await ctx.Response.WriteAsJsonAsync(new { error = ex.Message }); } // 4xx
    catch (Exception ex) // onno error
    {
        ctx.RequestServices.GetRequiredService<ILogger<Program>>().LogError(ex, "Unhandled error"); // log
        ctx.Response.StatusCode = 500; // 500
        await ctx.Response.WriteAsJsonAsync(new { error = "Unexpected server error" }); // generic message
    }
});

await DatabaseInitializer.InitAsync(app.Services, conn, app.Logger); // retry + diagnostics

app.UseSwagger(); // Swagger JSON
app.UseSwaggerUI(); // Swagger browser interface
app.MapControllers(); // route
app.Run(); // chalu