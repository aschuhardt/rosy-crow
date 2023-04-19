using System.Diagnostics;
using CommunityToolkit.Maui;
using LiteDB;
using Microsoft.Extensions.Logging;
using Opal;
using RosyCrow.Database;
using RosyCrow.Interfaces;
using RosyCrow.Services.Cache;
using RosyCrow.Services.Fingerprint;
using RosyCrow.Services.Fingerprint.Abstractions;
using RosyCrow.Services.Identity;
using RosyCrow.Views;

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
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services
            .AddSingleton<ILiteDatabase>(_ => new LiteDatabase(Path.Combine(FileSystem.AppDataDirectory, "app.db")))
            .AddSingleton<ISettingsDatabase, SettingsDatabase>()
            .AddSingleton<IBrowsingDatabase, BrowsingDatabase>()
            .AddSingleton<MainPage>()
            .AddSingleton<BookmarksPage>()
            .AddSingleton<IdentityPage>()
            .AddSingleton<SettingsPage>()
            .AddSingleton<HistoryPage>()
            .AddSingleton<AboutPage>()
            .AddSingleton(typeof(IFingerprint), CrossFingerprint.Current)
            .AddSingleton<IIdentityService, IdentityService>()
            .AddTransient<IOpalClient, OpalClient>()
            .AddTransient<ICacheService, DiskCacheService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        AppDomain.CurrentDomain.UnhandledException += (sender, e) => Debugger.Break();

        var app = builder.Build();

        Services = app.Services;

        return app;
    }
}