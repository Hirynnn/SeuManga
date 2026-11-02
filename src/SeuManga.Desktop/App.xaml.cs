using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.IO;
using System.Windows;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Manga;
using SeuManga.Application.Anime;
using SeuManga.Application.Library;
using SeuManga.Application.Streaming;
using SeuManga.Infrastructure.MangaDex;
using SeuManga.Infrastructure.Jikan;
using SeuManga.Infrastructure.Persistence;
using SeuManga.Infrastructure.Security;
using SeuManga.Infrastructure.TMDB;
using SeuManga.Infrastructure.Watchmode;
using SeuManga.Desktop.ViewModels;
using SeuManga.Desktop.Networking;
namespace SeuManga.Desktop;
public partial class App : System.Windows.Application
{
    private IHost? _host;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var dataDirectory = Environment.GetEnvironmentVariable("SEUMANGA_DATA_DIR");
            if (string.IsNullOrWhiteSpace(dataDirectory)) dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SeuManga");
            Directory.CreateDirectory(dataDirectory);
            var databaseFile = Path.Combine(dataDirectory, "seumanga.db");
            _host = Host.CreateDefaultBuilder().UseSerilog((_, logger) => logger.MinimumLevel.Information().WriteTo.File(Path.Combine(dataDirectory, "logs", "seumanga-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)).ConfigureServices(services =>
            {
                services.AddMemoryCache();
                services.AddSingleton<MangaDexBrowserTransport>();
                services.AddHttpClient<IChapterImageProvider, MangaDexImageClient>(client => { client.Timeout = TimeSpan.FromSeconds(90); client.DefaultRequestHeaders.UserAgent.ParseAdd("SeuManga/0.1"); })
                    .ConfigurePrimaryHttpMessageHandler(provider => new MangaDexBrowserHttpMessageHandler(provider.GetRequiredService<MangaDexBrowserTransport>()))
                    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
                services.AddHttpClient<IMangaCatalogProvider, MangaDexClient>(client =>
                {
                    client.BaseAddress = new Uri("https://api.mangadex.org/"); client.Timeout = TimeSpan.FromSeconds(90);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("SeuManga/0.1 (Windows desktop application)");
                }).ConfigurePrimaryHttpMessageHandler(provider => new MangaDexBrowserHttpMessageHandler(provider.GetRequiredService<MangaDexBrowserTransport>()))
                    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
                services.AddSingleton<ISecureSettingsStore>(_ => new DpapiSettingsStore(Path.Combine(dataDirectory, "api-secrets.json")));
                services.AddTransient<ApiConnectionTester>();
                services.AddHttpClient<ITmdbAnimeClient, TmdbClient>("TMDB", client => { client.BaseAddress = new Uri("https://api.themoviedb.org/3/"); client.Timeout = TimeSpan.FromSeconds(20); });
                services.AddHttpClient<IWatchmodeClient, WatchmodeClient>("Watchmode", client => { client.BaseAddress = new Uri("https://api.watchmode.com/v1/"); client.Timeout = TimeSpan.FromSeconds(20); });
                services.AddHttpClient<IAnimeCatalogProvider, JikanClient>(client => { client.BaseAddress = new Uri("https://api.jikan.moe/v4/"); client.Timeout = TimeSpan.FromSeconds(12); client.DefaultRequestHeaders.UserAgent.ParseAdd("SeuManga/0.1"); });
                services.AddTransient<AnimeCatalogService>(); services.AddTransient<AnimeCatalogViewModel>(); services.AddTransient<SeuManga.Desktop.Views.AnimeCatalogWindow>();
                services.AddTransient<GlobalSearchViewModel>(); services.AddTransient<SeuManga.Desktop.Views.GlobalSearchWindow>();
                services.AddTransient<SeuManga.Desktop.Views.SettingsWindow>();
                services.AddDbContextFactory<SeuMangaDbContext>(options => options.UseSqlite($"Data Source={databaseFile}"));
                services.AddTransient<DatabaseBackupService>();
                services.AddSingleton<LibraryRepository>();
                services.AddSingleton<ILibraryRepository>(sp => sp.GetRequiredService<LibraryRepository>());
                services.AddSingleton<IAnimeLibraryRepository>(sp => sp.GetRequiredService<LibraryRepository>());
                services.AddSingleton<ICustomListRepository>(sp => sp.GetRequiredService<LibraryRepository>());
                services.AddSingleton<IExternalIdentifierRepository>(sp => sp.GetRequiredService<LibraryRepository>());
                services.AddTransient<AnimeTrackingService>(); services.AddTransient<LibraryService>(); services.AddTransient<CustomListService>();
                services.AddTransient<MangaCatalogService>(); services.AddTransient<MangaCatalogViewModel>();
                services.AddTransient<SeuManga.Desktop.Views.MangaCatalogWindow>(); services.AddTransient<SeuManga.Desktop.Views.LibraryWindow>(); services.AddTransient<SeuManga.Desktop.Views.CustomListsWindow>();
                services.AddTransient<MainWindow>();
            }).Build();
            await _host.StartAsync();
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SeuMangaDbContext>>();
                await using var db = await factory.CreateDbContextAsync();
                await db.Database.MigrateAsync();
            }
            var window = _host.Services.GetRequiredService<MainWindow>(); MainWindow = window; window.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "SeuManga não conseguiu iniciar");
            MessageBox.Show("O SeuManga não conseguiu iniciar. Verifique o acesso à pasta de dados do usuário e consulte os logs.", "SeuManga", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null) { await _host.StopAsync(); _host.Dispose(); }
        Log.CloseAndFlush(); base.OnExit(e);
    }
}
