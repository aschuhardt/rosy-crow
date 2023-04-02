using System.Diagnostics;
using CommunityToolkit.Maui;
using LiteDB;
using Microsoft.Extensions.Logging;
using Opal;
using Yarrow.Database;
using Yarrow.Interfaces;

namespace Yarrow;

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
            .AddTransient<IOpalClient, OpalClient>()
            .AddTransient<MainPage>()
            .AddTransient<BookmarksPage>()
            .AddTransient<HistoryPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        AppDomain.CurrentDomain.UnhandledException += (sender, e) => Debugger.Break();

        var app = builder.Build();

        Services = app.Services;

        return app;
    }
}