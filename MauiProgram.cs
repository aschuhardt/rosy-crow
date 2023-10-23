using System.Diagnostics;
using CommunityToolkit.Maui;
using Microsoft.Maui.LifecycleEvents;
using Opal;
using Opal.Response;
using RosyCrow.Database;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Services.Cache;
using RosyCrow.Services.Document;
using RosyCrow.Services.Fingerprint;
using RosyCrow.Services.Fingerprint.Abstractions;
using RosyCrow.Services.Identity;
using RosyCrow.Views;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;
using SQLite;
using SQLitePCL;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("NotoEmoji-Regular.ttf", "NotoEmoji");
                fonts.AddFont("NotoSans-Regular.ttf", "NotoSansRegular");
                fonts.AddFont("NotoSans-Bold.ttf", "NotoSansBold");
            })
            .ConfigureLifecycleEvents(events =>
            {
                events.AddAndroid(android =>
                {
                    android.OnApplicationCreating(async _ =>
                        await Services.GetRequiredService<IDocumentService>().LoadResources());
                });
            })
            .ConfigureEssentials(config => { config.UseVersionTracking(); });

        if (VersionTracking.IsFirstLaunchForVersion("1.2.0"))
        {
            // caching strategy changed; clear the old cache
            foreach (var path in Directory.GetDirectories(FileSystem.CacheDirectory))
                Directory.Delete(path, true);
        }

        Batteries.Init();

        builder.Services
            .AddSingleton(_ => new SQLiteConnection(Path.Combine(FileSystem.AppDataDirectory, Constants.DatabaseName),
                Constants.SQLiteFlags))
            .AddSingleton<ISettingsDatabase, SettingsDatabase>()
            .AddSingleton<IBrowsingDatabase, BrowsingDatabase>()
            .AddSingleton<MainPage>()
            .AddSingleton<BookmarksPage>()
            .AddSingleton<IdentityPage>()
            .AddSingleton<SettingsPage>()
            .AddSingleton<HistoryPage>()
            .AddSingleton<AboutPage>()
            .AddSingleton<WhatsNewPage>()
            .AddSingleton<CertificatePage>()
            .AddSingleton(typeof(IFingerprint), CrossFingerprint.Current)
            .AddSingleton<IIdentityService, IdentityService>()
            .AddSingleton<IDocumentService, DocumentService>()
            .AddTransient<ExportIdentityPage>()
            .AddTransient<ImportIdentityPage>()
            .AddTransient<TitanUploadPage>()
            .AddTransient<BrowserView>()
            .AddTransient<IOpalClient>(services =>
                new OpalClient(services.GetRequiredService<IBrowsingDatabase>(), RedirectBehavior.Follow))
            .AddTransient<ICacheService, DiskCacheService>();

        var certsDirectory = Path.Combine(FileSystem.AppDataDirectory, Constants.CertificateDirectory);
        if (!Directory.Exists(certsDirectory))
            Directory.CreateDirectory(certsDirectory);

        var logDirectory = Path.Combine(FileSystem.AppDataDirectory, Constants.LogDirectory);
        if (!Directory.Exists(logDirectory))
            Directory.CreateDirectory(logDirectory);

        var logConfig = new LoggerConfiguration()
            .Enrich.WithExceptionDetails()
            .WriteTo.Async(a =>
                a.File(new CompactJsonFormatter(),
                    Path.Combine(logDirectory, "log.json"),
                    LogEventLevel.Warning,
                    retainedFileCountLimit: 7,
                    rollingInterval: RollingInterval.Day))
            .WriteTo.Debug(LogEventLevel.Debug);

        builder.Logging.AddSerilog(logConfig.CreateLogger());

        AppDomain.CurrentDomain.UnhandledException += (sender, e) => Debugger.Break();

        var app = builder.Build();

        Services = app.Services;

        return app;
    }
}