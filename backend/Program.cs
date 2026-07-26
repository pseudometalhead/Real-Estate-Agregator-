using EstateAggregator.Data;
using EstateAggregator.Jobs;
using EstateAggregator.Services;
using EstateAggregator.Services.Scrapers;
using EstateAggregator.Utilities;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog) ---
// LOG_FILE_PATH is a flat env var (set by docker-compose) and does not
// auto-bind to the nested "Serilog:LogFilePath" configuration key, so it's
// read explicitly here with the appsettings value as a fallback.
var logFilePath = Environment.GetEnvironmentVariable("LOG_FILE_PATH")
    ?? builder.Configuration["Serilog:LogFilePath"]
    ?? "Logs/estate-aggregator-.log";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DATABASE_PATH is a flat env var (set by docker-compose) and does not
// auto-bind to the nested "ConnectionStrings:DefaultConnection" key, so it's
// read explicitly here with the appsettings value as a fallback.
var databasePath = Environment.GetEnvironmentVariable("DATABASE_PATH");
var connectionString = !string.IsNullOrEmpty(databasePath)
    ? $"Data Source={databasePath}"
    : builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=data/estate.db";

// SQLite does not create the parent directory for the database file itself.
var sqliteFilePath = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
var sqliteDirectory = Path.GetDirectoryName(Path.GetFullPath(sqliteFilePath));
if (!string.IsNullOrEmpty(sqliteDirectory))
    Directory.CreateDirectory(sqliteDirectory);

builder.Services.AddDbContext<EstateDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddHttpClient<IdealistaScraper>(client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(HttpClientPolicies.GetRetryPolicy())
    .AddPolicyHandler(HttpClientPolicies.GetCircuitBreakerPolicy());

builder.Services.AddHttpClient<ImoVirtualScraper>(client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(HttpClientPolicies.GetRetryPolicy())
    .AddPolicyHandler(HttpClientPolicies.GetCircuitBreakerPolicy());

builder.Services.AddHttpClient<ImobiliarioScraper>(client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(HttpClientPolicies.GetRetryPolicy())
    .AddPolicyHandler(HttpClientPolicies.GetCircuitBreakerPolicy());

builder.Services.AddHttpClient<CasaSapoScraper>(client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "pt-PT,pt;q=0.9,en;q=0.8");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(HttpClientPolicies.GetRetryPolicy())
    .AddPolicyHandler(HttpClientPolicies.GetCircuitBreakerPolicy());

// AddHttpClient<TClient>() above already registers IdealistaScraper/etc. as
// typed clients wired to their configured HttpClient. Registering
// IPropertyScraper directly against the concrete type (AddScoped<IPropertyScraper,
// IdealistaScraper>) would create a second, independent construction path with
// no HttpClient available to inject, so IPropertyScraper is forwarded to the
// already-configured typed client instance instead.
builder.Services.AddScoped<IPropertyScraper>(sp => sp.GetRequiredService<IdealistaScraper>());
builder.Services.AddScoped<IPropertyScraper>(sp => sp.GetRequiredService<ImoVirtualScraper>());
builder.Services.AddScoped<IPropertyScraper>(sp => sp.GetRequiredService<ImobiliarioScraper>());
builder.Services.AddScoped<IPropertyScraper>(sp => sp.GetRequiredService<CasaSapoScraper>());

builder.Services.AddScoped<DataNormalizationService>();
builder.Services.AddScoped<DeduplicationService>();
builder.Services.AddScoped<AppSettingsService>();
builder.Services.AddScoped<ScraperService>();
builder.Services.AddScoped<ReportService>();

// --- Quartz scheduling ---
var morningCron = Environment.GetEnvironmentVariable("SCRAPER_MORNING_CRON")
    ?? builder.Configuration["Quartz:MorningCron"]
    ?? "0 0 8 * * ?";
var eveningCron = Environment.GetEnvironmentVariable("SCRAPER_EVENING_CRON")
    ?? builder.Configuration["Quartz:EveningCron"]
    ?? "0 0 17 * * ?";

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("ScraperJob");
    q.AddJob<ScraperJob>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("morning-scraper")
        .WithCronSchedule(morningCron));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("evening-scraper")
        .WithCronSchedule(eveningCron));
});

builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

// --- Health checks ---
// Referenced by the docker-compose healthcheck; not defined in the original
// spec, so it's added here explicitly.
builder.Services.AddHealthChecks();

// --- CORS ---
// The frontend (port 3000) and backend (port 5000) run on different
// origins; the spec never mentions CORS but the frontend can't call the
// API without it.
var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigin).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EstateDbContext>();
    dbContext.Database.Migrate();
    await EstateAggregator.Data.DataSeeder.SeedAsync(dbContext);
}

app.Run();
