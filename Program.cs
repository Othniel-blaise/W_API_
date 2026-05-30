using Microsoft.Extensions.Options;
using Serilog;
using W_API.Api.Hubs;
using W_API.Api.Logging;
using W_API.Core.Configuration;
using W_API.Core.Interfaces;
using W_API.Core.Services;
using W_API.Infrastructure.BackgroundWorker;
using W_API.Infrastructure.Data;
using W_API.Infrastructure.IO;
using W_API.Infrastructure.Ocr;
using W_API.Infrastructure.Pdf;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .WriteTo.Sink(new SignalRSink()));   // ← pousse chaque log vers le terminal web

    // ── Configuration ─────────────────────────────────────────────────
    builder.Services.Configure<AppSettings>(builder.Configuration);

    // ── SignalR + static files ────────────────────────────────────────
    builder.Services.AddSignalR();
    builder.Services.AddHostedService<LogBroadcastService>();

    // ── CORS (pour dev local — même origin) ──────────────────────────
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.WithOrigins("http://localhost:5179", "https://localhost:7003")
         .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

    // ── Infrastructure ────────────────────────────────────────────────
    builder.Services.AddSingleton<OcrEngine>();
    builder.Services.AddScoped<IPdfTextExtractor, PdfTextExtractor>();
    builder.Services.AddScoped<IAqManagerRepository, AqManagerRepository>();
    builder.Services.AddScoped<IFileManager, FileManager>();

    // ── Core ──────────────────────────────────────────────────────────
    builder.Services.AddScoped<IDocumentTypeDetector, DocumentTypeDetector>();
    builder.Services.AddScoped<SiteResolver>();
    builder.Services.AddScoped<IIngestionService, IngestionService>();

    // ── Background ingestion worker (optionnel) ───────────────────────
    builder.Services.AddHostedService<IngestionBackgroundService>();

    builder.Services.AddControllers();

    var app = builder.Build();

    // ── Startup checks ────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var cfg = scope.ServiceProvider.GetRequiredService<IOptions<AppSettings>>().Value;

        try
        {
            var fm = scope.ServiceProvider.GetRequiredService<IFileManager>();
            fm.VerifyWriteAccess();
            logger.LogInformation("Accès écriture OK : {DocsFolder}", cfg.Paths.DocsPhysicalFolder);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible d'écrire dans {DocsFolder}", cfg.Paths.DocsPhysicalFolder);
        }

        try
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAqManagerRepository>();
            var notNull = await repo.GetNotNullColumnsAsync("Documents");
            logger.LogInformation("Colonnes NOT NULL dans Documents : {Cols}", string.Join(", ", notNull));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de lire INFORMATION_SCHEMA — connexion SQL à vérifier.");
        }
    }

    // ── Middleware pipeline ───────────────────────────────────────────
    app.UseCors();
    app.UseSerilogRequestLogging();
    app.UseDefaultFiles();      // sert index.html sur /
    app.UseStaticFiles();       // sert wwwroot/

    app.MapControllers();
    app.MapHub<LogHub>("/hubs/logs");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Démarrage de l'API échoué");
}
finally
{
    Log.CloseAndFlush();
}
