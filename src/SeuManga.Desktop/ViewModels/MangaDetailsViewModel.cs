using System.Collections.ObjectModel;
using System.Net.Http;
using Serilog;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Manga;
namespace SeuManga.Desktop.ViewModels;
public sealed record ChapterLanguageOption(string Code, string Name);

public sealed class MangaDetailsViewModel : ViewModelBase
{
    private static readonly IReadOnlyDictionary<string, string> LanguageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = "Árabe", ["bg"] = "Búlgaro", ["ca"] = "Catalão", ["cs"] = "Tcheco", ["da"] = "Dinamarquês",
        ["de"] = "Alemão", ["el"] = "Grego", ["en"] = "Inglês", ["es"] = "Espanhol", ["es-la"] = "Espanhol (América Latina)",
        ["fa"] = "Persa", ["fi"] = "Finlandês", ["fil"] = "Filipino", ["fr"] = "Francês", ["he"] = "Hebraico",
        ["hi"] = "Hindi", ["hr"] = "Croata", ["hu"] = "Húngaro", ["id"] = "Indonésio", ["it"] = "Italiano",
        ["ja"] = "Japonês", ["ko"] = "Coreano", ["ms"] = "Malaio", ["my"] = "Birmanês", ["nl"] = "Holandês",
        ["no"] = "Norueguês", ["pl"] = "Polonês", ["pt"] = "Português", ["pt-br"] = "Português (Brasil)",
        ["ro"] = "Romeno", ["ru"] = "Russo", ["th"] = "Tailandês", ["tr"] = "Turco", ["uk"] = "Ucraniano",
        ["vi"] = "Vietnamita", ["zh"] = "Chinês", ["zh-hk"] = "Chinês (Hong Kong)"
    };
    private readonly MangaCatalogService _catalog;
    private bool _loading;
    private string _message = "Carregando traduções disponíveis…";
    private int _chapterOffset;
    private int _chapterTotal;
    private const int PageSize = 100;
    private readonly HashSet<string> _chapterKeys = new(StringComparer.OrdinalIgnoreCase);
    private ChapterLanguageOption? _selectedLanguage;
    private bool _languageSelectionReady;
    public MangaDetailsViewModel(MangaCatalogService catalog, MangaSearchResult manga, IChapterImageProvider imageProvider)
    {
        _catalog = catalog; Manga = manga; ImageProvider = imageProvider;
        LoadCommand = new AsyncCommand(LoadAsync, () => !Loading);
        LoadMoreCommand = new AsyncCommand(LoadMoreAsync, () => !Loading && _chapterOffset < _chapterTotal);
    }
    public MangaSearchResult Manga { get; }
    public IChapterImageProvider ImageProvider { get; }
    public MangaDetails? Details { get; private set; }
    public ObservableCollection<MangaChapter> Chapters { get; } = [];
    public ObservableCollection<ChapterLanguageOption> Languages { get; } = [];
    public ChapterLanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!Set(ref _selectedLanguage, value)) return;
            Notify(nameof(ChapterListHeading));
            Notify(nameof(CanChooseLanguage));
            if (_languageSelectionReady && value is not null && !Loading)
                _ = ReloadLanguageAsync();
        }
    }
    public string ChapterListHeading => SelectedLanguage is null ? "Capítulos disponíveis" : $"Capítulos · {SelectedLanguage.Name}";
    public bool CanChooseLanguage => !Loading && Languages.Count > 1;
    public bool Loading
    {
        get => _loading;
        private set
        {
            if (!Set(ref _loading, value)) return;
            Notify(nameof(CanChooseLanguage));
            LoadCommand.NotifyCanExecuteChanged();
            LoadMoreCommand.NotifyCanExecuteChanged();
        }
    }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public AsyncCommand LoadCommand { get; }
    public AsyncCommand LoadMoreCommand { get; }
    public async Task LoadAsync()
    {
        if (Details is not null || Loading) return;
        Loading = true;
        try
        {
            Details = await _catalog.GetDetailsAsync(Manga.Id);
            Notify(nameof(Details));
            PopulateLanguages(Details.AvailableTranslatedLanguages ?? []);
            if (SelectedLanguage is null)
                Message = "A MangaDex não informou traduções disponíveis para este título.";
            else
                await LoadMoreCoreAsync();
        }
        catch (HttpRequestException ex) { Message = ProviderErrorMessage("MangaDex", ex); }
        catch (TaskCanceledException) { Message = "A MangaDex não respondeu a tempo. Verifique a conexão e tente novamente."; }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível carregar os detalhes do mangá {MangaId}", Manga.Id);
            Message = "Não foi possível carregar os detalhes deste título.";
        }
        finally { Loading = false; }
    }
    private async Task LoadMoreAsync()
    {
        Loading = true;
        try { await LoadMoreCoreAsync(); }
        catch (HttpRequestException ex) { Message = ProviderErrorMessage("MangaDex", ex); }
        catch (TaskCanceledException) { Message = "A MangaDex não respondeu a tempo. Tente carregar os capítulos novamente."; }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível carregar mais capítulos do mangá {MangaId}", Manga.Id);
            Message = "Não foi possível carregar mais capítulos.";
        }
        finally { Loading = false; }
    }
    private async Task ReloadLanguageAsync()
    {
        if (Loading || SelectedLanguage is null) return;
        Loading = true;
        Chapters.Clear();
        _chapterKeys.Clear();
        _chapterOffset = 0;
        _chapterTotal = 0;
        LoadMoreCommand.NotifyCanExecuteChanged();
        Message = $"Carregando capítulos em {SelectedLanguage.Name}…";
        try { await LoadMoreCoreAsync(); }
        catch (HttpRequestException ex) { Message = ProviderErrorMessage("MangaDex", ex); }
        catch (TaskCanceledException) { Message = "A MangaDex não respondeu a tempo. Tente novamente."; }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível carregar capítulos em {Language} para o mangá {MangaId}", SelectedLanguage.Code, Manga.Id);
            Message = "Não foi possível carregar os capítulos neste idioma.";
        }
        finally { Loading = false; }
    }
    private void PopulateLanguages(IReadOnlyList<string> languages)
    {
        Languages.Clear();
        foreach (var code in languages.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(code)) continue;
            var name = LanguageNames.TryGetValue(code, out var translatedName) ? translatedName : code.ToUpperInvariant();
            Languages.Add(new ChapterLanguageOption(code, name));
        }
        SelectedLanguage = Languages.FirstOrDefault(x => x.Code.Equals("pt-br", StringComparison.OrdinalIgnoreCase))
            ?? Languages.FirstOrDefault();
        _languageSelectionReady = true;
        Notify(nameof(CanChooseLanguage));
    }
    private async Task LoadMoreCoreAsync()
    {
        var language = SelectedLanguage ?? throw new InvalidOperationException("Selecione um idioma antes de carregar os capítulos.");
        var page = await _catalog.GetChaptersAsync(Manga.Id, language.Code, _chapterOffset, PageSize);
        _chapterTotal = page.Total;
        var nextOffset = page.NextOffset ?? _chapterOffset + page.Items.Count;
        _chapterOffset = nextOffset > _chapterOffset ? nextOffset : _chapterTotal;
        foreach (var chapter in page.Items)
        {
            var key = string.IsNullOrWhiteSpace(chapter.Number) ? chapter.Id : $"{chapter.Volume}:{chapter.Number}";
            if (_chapterKeys.Add(key)) Chapters.Add(chapter);
        }
        Message = Chapters.Count == 0
            ? $"Nenhum capítulo em {language.Name} encontrado."
            : _chapterOffset >= _chapterTotal
                ? $"{Chapters.Count} capítulos disponíveis · {language.Name}"
                : $"{Chapters.Count} capítulos carregados · {language.Name}";
        LoadMoreCommand.NotifyCanExecuteChanged();
    }
}
