using Botijas.Domain.Repositories;
using Botijas.Infrastructure.Hubs;
using Botijas.Infrastructure.Data;
using Botijas.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Database
var rawConnectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Host=localhost;Port=5432;Database=devdb;Username=devuser;Password=devpass;Include Error Detail=true";

var connectionString = NormalizePostgresConnectionString(rawConnectionString);

builder.Services.AddDbContext<BotijasDbContext>(options =>
    options.UseNpgsql(connectionString));

// Repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IRefillOrderRepository, RefillOrderRepository>();
builder.Services.AddScoped<ICylinderRepository, CylinderRepository>();
builder.Services.AddScoped<IPrintJobRepository, PrintJobRepository>();
builder.Services.AddScoped<ICylinderHistoryRepository, CylinderHistoryRepository>();

// Application Handlers
builder.Services.AddScoped<Botijas.Application.Customers.Commands.CreateCustomerCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Customers.Queries.SearchCustomersQueryHandler>();
builder.Services.AddScoped<Botijas.Application.Customers.Queries.GetCustomerCylindersQueryHandler>();
builder.Services.AddScoped<Botijas.Application.Orders.Commands.CreateOrderCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Orders.Commands.AddCylinderToOrderCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Orders.Commands.AddCylindersToOrderBatchCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Orders.Commands.ScanCylinderToOrderCommandHandler>();
// Simulated print dispatcher (auto-acks as printed; swap for SignalRPrintJobDispatcher when real printer is connected)
builder.Services.AddScoped<Botijas.Application.PrintJobs.IPrintJobDispatcher, Botijas.Infrastructure.PrintJobs.SimulatedPrintJobDispatcher>();
builder.Services.AddScoped<Botijas.Application.PrintJobs.Commands.CreatePrintJobCommandHandler>();
builder.Services.AddScoped<Botijas.Application.PrintJobs.Commands.AckPrintJobPrintedCommandHandler>();
builder.Services.AddScoped<Botijas.Application.PrintJobs.Commands.AckPrintJobFailedCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Filling.Queries.GetFillingQueueQueryHandler>();
builder.Services.AddScoped<Botijas.Application.Filling.Commands.MarkCylinderReadyCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Filling.Commands.MarkCylindersReadyBatchCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Filling.Commands.ReportCylinderProblemCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Filling.Commands.AssignLabelCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Cylinders.Queries.GetCylinderHistoryQueryHandler>();
builder.Services.AddScoped<Botijas.Application.Cylinders.Queries.GetCylinderByTokenQueryHandler>();
builder.Services.AddScoped<Botijas.Application.Reports.Queries.IDashboardStatsQuery, Botijas.Infrastructure.Queries.DashboardStatsQuery>();
builder.Services.AddScoped<Botijas.Application.Pickup.Queries.GetReadyForPickupQueryHandler>();
builder.Services.AddScoped<Botijas.Application.Pickup.Commands.DeliverCylinderCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Pickup.Commands.MarkOrderNotifiedCommandHandler>();

// CORS
var configuredOrigins = builder.Configuration["CORS__AllowedOrigins"];
var allowedOrigins = (configuredOrigins ?? string.Empty)
    .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (allowedOrigins.Length == 0)
{
    allowedOrigins = new[]
    {
        "http://localhost:3000",
        "https://botijaslx.vercel.app"
    };
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(configuredOrigins))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
            return;
        }

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.MapHub<PrintHub>("/hubs/print");

// Database bootstrap: apply migrations (or create schema) automatically on startup.
var autoInitDb = builder.Configuration.GetValue("Database__AutoInitialize", true);
if (autoInitDb)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BotijasDbContext>();
    const int maxAttempts = 20;
    var attempt = 0;

    while (true)
    {
        try
        {
            if (dbContext.Database.GetMigrations().Any())
            {
                dbContext.Database.Migrate();
            }
            else
            {
                dbContext.Database.EnsureCreated();
            }

            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            attempt++;
            app.Logger.LogWarning(
                ex,
                "Database not ready yet (attempt {Attempt}/{MaxAttempts}). Retrying in 2s...",
                attempt,
                maxAttempts);

            Thread.Sleep(TimeSpan.FromSeconds(2));
        }
    }
}

app.Run();

static string NormalizePostgresConnectionString(string input)
{
    if (!(input.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
          input.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
    {
        return input;
    }

    var uri = new Uri(input);
    var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.None);
    var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var database = uri.AbsolutePath.TrimStart('/');
    var port = uri.IsDefaultPort ? 5432 : uri.Port;

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = port,
        Database = database,
        Username = username,
        Password = password
    };

    if (!string.IsNullOrWhiteSpace(uri.Query))
    {
        var parts = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var kv = part.Split('=', 2, StringSplitOptions.None);
            var key = Uri.UnescapeDataString(kv[0]);
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;

            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("ssl mode", StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = Enum.TryParse<SslMode>(value, true, out var sslMode)
                    ? sslMode
                    : SslMode.Require;
            }
            else if (key.Equals("channel_binding", StringComparison.OrdinalIgnoreCase))
            {
                builder["Channel Binding"] = value;
            }
            else if (!string.IsNullOrWhiteSpace(value))
            {
                builder[key] = value;
            }
        }
    }

    if (builder.SslMode == SslMode.Disable)
    {
        builder.SslMode = SslMode.Require;
    }

    builder.TrustServerCertificate = true;
    return builder.ConnectionString;
}
