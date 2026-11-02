using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SeuManga.Application.Anime;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Library;
using SeuManga.Application.Manga;
using SeuManga.Desktop.ViewModels;
using SeuManga.Desktop.Views;
using SeuManga.Desktop.Networking;
using SeuManga.Domain.Entities;
using SeuManga.Domain.Enums;
namespace SeuManga.Desktop;
public sealed record HomeAnimeCard(int JikanId, string Title, string? ImageUrl, string ProgressText, string StatusLabel, double ProgressPercent);
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly IServiceProvider _services;
    private readonly MangaDexBrowserTransport _mangaDexBrowserTransport;
    private readonly IChapterImageProvider _imageProvider;
    private readonly Dictionary<Window, object> _pageContents = [];
    private readonly Stack<(Window? Page, string Title, string? NavigationKey)> _navigationHistory = [];
    private Window? _activePage;
    private ResourceDictionary? _activePageResources;
    private string? _activeNavigationKey;
    private bool _showingHome = true;
    private bool _homeLoaded;
    private string _homeMessage = "A página inicial usa seus dados locais e catálogos online.";
    private ReadingHistoryEntry? _featuredReading;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ReadingHistoryEntry> RecentReads { get; } = [];
    public ObservableCollection<HomeAnimeCard> TrackedAnime { get; } = [];
    public ObservableCollection<AnimeSearchResult> PopularAnime { get; } = [];
    public ObservableCollection<AnimeRecommendation> HomeRecommendations { get; } = [];
    public IChapterImageProvider ImageProvider => _imageProvider;
    public ReadingHistoryEntry? FeaturedReading
    {
        get => _featuredReading;
        private set
        {
            if (ReferenceEquals(_featuredReading, value)) return;
            _featuredReading = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FeaturedReading)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FeaturedPageLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFeaturedReading)));
        }
    }
    public bool HasFeaturedReading => FeaturedReading is not null;
    public string FeaturedPageLabel => FeaturedReading is { PageCount: > 0 } reading
        ? $"Página {Math.Clamp(reading.LastPage + 1, 1, reading.PageCount)} de {reading.PageCount}"
        : "Continue sua leitura";
    public string HomeMessage
    {
        get => _homeMessage;
        private set
        {
            if (_homeMessage == value) return;
            _homeMessage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HomeMessage)));
        }
    }
    public MainWindow(IServiceProvider services, MangaDexBrowserTransport mangaDexBrowserTransport)
    {
        InitializeComponent();
        _services = services;
        _mangaDexBrowserTransport = mangaDexBrowserTransport;
        _imageProvider = services.GetRequiredService<IChapterImageProvider>();
        _mangaDexBrowserTransport.Attach(MangaDexNetworkBrowser);
        DataContext = this;
        UpdateNavigationHighlight("home");
    }
    public void NavigateToPage(Window page, string title, string? navigationKey = null)
    {
        if (ReferenceEquals(page, _activePage)) return;
        if (_showingHome || _activePage is not null)
            _navigationHistory.Push((_activePage, PageHeading.Text, _activeNavigationKey));
        DisplayPage(page, title, navigationKey);
    }
    private void DisplayPage(Window page, string title, string? navigationKey)
    {
        if (_activePageResources is not null)
            PageHost.Resources.MergedDictionaries.Remove(_activePageResources);
        if (!_pageContents.TryGetValue(page, out var content))
        {
            content = page.Content ?? throw new InvalidOperationException("A tela não possui conteúdo para exibir.");
            if (content is FrameworkElement root && page.DataContext is not null)
                root.DataContext = page.DataContext;
            page.Content = null;
            _pageContents.Add(page, content);
        }
        _activePageResources = page.Resources.Count > 0 ? page.Resources : null;
        if (_activePageResources is not null)
            PageHost.Resources.MergedDictionaries.Add(_activePageResources);

        HomeView.Visibility = Visibility.Collapsed;
        PageContainer.Visibility = Visibility.Visible;
        PageHost.Content = content;
        _activePage = page;
        _showingHome = false;
        _activeNavigationKey = navigationKey;
        PageHeading.Text = title;
        BackButton.IsEnabled = _navigationHistory.Count > 0;
        UpdateNavigationHighlight(navigationKey);
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
    }
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_navigationHistory.Count == 0) return;
        var previous = _navigationHistory.Pop();
        if (previous.Page is null)
        {
            ShowHome(previous.NavigationKey);
            return;
        }
        DisplayPage(previous.Page, previous.Title, previous.NavigationKey);
    }
    private void ShowHome(string? navigationKey = "home")
    {
        if (_activePageResources is not null)
            PageHost.Resources.MergedDictionaries.Remove(_activePageResources);
        _activePageResources = null;
        PageHost.Content = null;
        PageContainer.Visibility = Visibility.Collapsed;
        HomeView.Visibility = Visibility.Visible;
        _activePage = null;
        _activeNavigationKey = navigationKey ?? "home";
        _showingHome = true;
        UpdateNavigationHighlight(_activeNavigationKey);
    }
    private void UpdateNavigationHighlight(string? key)
    {
        var navigation = new (string Key, System.Windows.Controls.Button Button)[]
        {
            ("home", HomeNavButton), ("manga", MangaNavButton), ("anime", AnimeNavButton),
            ("library", LibraryNavButton), ("history", HistoryNavButton), ("favorites", FavoritesNavButton),
            ("lists", ListsNavButton), ("settings", SettingsNavButton)
        };
        foreach (var item in navigation)
        {
            var active = item.Key == key;
            item.Button.Background = active ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 42, 64)) : System.Windows.Media.Brushes.Transparent;
            item.Button.Foreground = active ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 243, 255)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170));
            item.Button.BorderBrush = active ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 92, 246)) : System.Windows.Media.Brushes.Transparent;
        }
    }
    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (_activePage is ReaderWindow reader) reader.HandleReaderKeyDown(e);
    }
    private async void Home_Loaded(object sender, RoutedEventArgs e)
    {
        if (_homeLoaded) return;
        _homeLoaded = true;
        var errors = new List<string>();
        IReadOnlyList<AnimeLibraryEntry> anime = [];
        try
        {
            var history = await _services.GetRequiredService<LibraryService>().GetHistoryAsync();
            FeaturedReading = history.FirstOrDefault();
            foreach (var item in history.Take(8)) RecentReads.Add(item);
            var tracking = _services.GetRequiredService<AnimeTrackingService>();
            anime = await tracking.GetLibraryAsync();
            foreach (var item in anime.Where(x => x.Status == AnimeLibraryStatus.Watching).Take(8))
            {
                var watched = (await tracking.GetWatchedEpisodesAsync(item.JikanId)).Count;
                var progress = item.TotalEpisodes is > 0 ? $"{watched} de {item.TotalEpisodes} episódios assistidos" : $"{watched} episódios assistidos";
                var progressPercent = item.TotalEpisodes is > 0 ? Math.Clamp(watched * 100d / item.TotalEpisodes.Value, 0, 100) : 0;
                TrackedAnime.Add(new HomeAnimeCard(item.JikanId, item.Title, item.ImageUrl, progress, item.IsFavorite ? "Assistindo · Favorito" : "Assistindo", progressPercent));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível carregar os dados locais da página inicial");
            errors.Add("Não foi possível carregar parte dos seus dados locais.");
        }

        var catalog = _services.GetRequiredService<AnimeCatalogService>();
        try
        {
            var top = await catalog.GetTopAnimeAsync(filter: "bypopularity", limit: 8);
            foreach (var item in top.Items) PopularAnime.Add(item);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível carregar animes populares na página inicial");
            errors.Add("Animes populares indisponíveis no momento.");
        }

        if (anime.Count > 0)
        {
            try
            {
                foreach (var item in await catalog.GetRecommendationsAsync(anime[0].JikanId))
                    if (HomeRecommendations.Count < 8) HomeRecommendations.Add(item);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Não foi possível carregar recomendações para a página inicial");
                errors.Add("Recomendações indisponíveis no momento.");
            }
        }

        HomeMessage = errors.Count == 0
            ? "Seus dados locais e descobertas atuais aparecem aqui."
            : $"{string.Join(" ", errors)} Seus dados disponíveis continuam acessíveis.";
    }
    private void Home_Click(object sender, RoutedEventArgs e) => ShowHome();
    private void Manga_Click(object sender, RoutedEventArgs e) => NavigateToPage(_services.GetRequiredService<MangaCatalogWindow>(), "Explorar mangás", "manga");
    private void Anime_Click(object sender, RoutedEventArgs e) => NavigateToPage(_services.GetRequiredService<AnimeCatalogWindow>(), "Explorar animes", "anime");
    private void Library_Click(object sender, RoutedEventArgs e) => NavigateToPage(_services.GetRequiredService<LibraryWindow>(), "Minha biblioteca", "library");
    private void Lists_Click(object sender, RoutedEventArgs e) => NavigateToPage(_services.GetRequiredService<CustomListsWindow>(), "Listas personalizadas", "lists");
    private void History_Click(object sender, RoutedEventArgs e) => NavigateToPage(new ReadingActivityWindow(_services.GetRequiredService<LibraryService>(), _services.GetRequiredService<IChapterImageProvider>(), false), "Histórico de leitura", "history");
    private void Favorites_Click(object sender, RoutedEventArgs e) => NavigateToPage(new ReadingActivityWindow(_services.GetRequiredService<LibraryService>(), _services.GetRequiredService<IChapterImageProvider>(), true), "Favoritos", "favorites");
    private void Settings_Click(object sender, RoutedEventArgs e) => NavigateToPage(_services.GetRequiredService<SettingsWindow>(), "Configurações", "settings");
    private void GlobalSearch_Click(object sender, RoutedEventArgs e)
    {
        var window = _services.GetRequiredService<GlobalSearchWindow>();
        var viewModel = (GlobalSearchViewModel)window.DataContext; viewModel.Query = GlobalQuery.Text;
        if (viewModel.Query.Trim().Length >= 2) viewModel.SearchCommand.Execute(null);
        NavigateToPage(window, "Pesquisa global");
    }
    private void GlobalQuery_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) GlobalSearch_Click(sender, e); }
    private async void ResumeReading_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ReadingHistoryEntry saved) return;
        try
        {
            var catalog = _services.GetRequiredService<MangaCatalogService>();
            var chapters = new List<MangaChapter>();
            for (var offset = 0; offset < 5000; offset += 100)
            {
                var page = await catalog.GetChaptersAsync(saved.MangaDexId, "pt-br", offset, 100);
                chapters.AddRange(page.Items);
                if (page.Items.Any(x => x.Id == saved.ChapterId) || chapters.Count >= page.Total || page.Items.Count == 0) break;
            }
            var index = chapters.FindIndex(x => x.Id == saved.ChapterId);
            if (index < 0) { MessageBox.Show(this, "O capítulo salvo não está disponível no catálogo atual.", "Continuar lendo", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var manga = new MangaSearchResult(saved.MangaDexId, saved.Title, "", "", "", saved.CoverUrl, []);
            var pages = await catalog.GetChapterPageUrlsAsync(saved.ChapterId);
            NavigateToPage(new ReaderWindow(chapters[index], chapters, catalog, pages, manga, _services.GetRequiredService<LibraryService>(), _services.GetRequiredService<IChapterImageProvider>()) { Owner = this }, $"Leitura · {saved.Title}");
        }
        catch { MessageBox.Show(this, "Não foi possível retomar a leitura. Verifique a conexão e tente novamente.", "Continuar lendo", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
    private void ContinueAnime_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not HomeAnimeCard anime) return;
        OpenAnime(anime.Title);
    }
    private void PopularAnime_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AnimeSearchResult anime) OpenAnime(anime.Title);
    }
    private void RecommendedAnime_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AnimeSearchResult anime) OpenAnime(anime.Title);
    }
    private void OpenAnime(string title)
    {
        var window = _services.GetRequiredService<AnimeCatalogWindow>();
        var viewModel = (AnimeCatalogViewModel)window.DataContext; viewModel.Query = title; viewModel.SearchCommand.Execute(null);
        NavigateToPage(window, "Explorar animes", "anime");
    }
}
