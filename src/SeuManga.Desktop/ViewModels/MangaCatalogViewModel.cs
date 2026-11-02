using System.Collections.ObjectModel;
using System.Net.Http;
using SeuManga.Application.Manga;
namespace SeuManga.Desktop.ViewModels;

public sealed class MangaCatalogViewModel : ViewModelBase
{
    private readonly MangaCatalogService _catalog;
    private string _query = "";
    private string _status = "";
    private string _translatedLanguage = "";
    private string _originalLanguage = "";
    private string _author = "";
    private string _artist = "";
    private string _includedTags = "";
    private string _excludedTags = "";
    private string _tagMode = "OR";
    private string _sort = "relevance";
    private string _year = "";
    private string _demographic = "";
    private bool _includeErotica;
    private bool _includePornographic;
    private bool _isBusy;
    private string _message = "Pesquise por título para consultar o catálogo MangaDex.";
    private int _page = 1;
    private const int PageSize = 24;
    public MangaCatalogViewModel(MangaCatalogService catalog)
    {
        _catalog = catalog;
        SearchCommand = new AsyncCommand(() => LoadAsync(1), () => !IsBusy);
        NextPageCommand = new AsyncCommand(() => LoadAsync(Page + 1), () => !IsBusy);
        PreviousPageCommand = new AsyncCommand(() => LoadAsync(Page - 1), () => !IsBusy && Page > 1);
    }
    public ObservableCollection<MangaSearchResult> Results { get; } = [];
    public string Query { get => _query; set => Set(ref _query, value); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public string TranslatedLanguage { get => _translatedLanguage; set => Set(ref _translatedLanguage, value); }
    public string OriginalLanguage { get => _originalLanguage; set => Set(ref _originalLanguage, value); }
    public string Author { get => _author; set => Set(ref _author, value); }
    public string Artist { get => _artist; set => Set(ref _artist, value); }
    public string IncludedTags { get => _includedTags; set => Set(ref _includedTags, value); }
    public string ExcludedTags { get => _excludedTags; set => Set(ref _excludedTags, value); }
    public string TagMode { get => _tagMode; set => Set(ref _tagMode, value); }
    public string Sort { get => _sort; set => Set(ref _sort, value); }
    public string Year { get => _year; set => Set(ref _year, value); }
    public string Demographic { get => _demographic; set => Set(ref _demographic, value); }
    public bool IncludeErotica { get => _includeErotica; set => Set(ref _includeErotica, value); }
    public bool IncludePornographic { get => _includePornographic; set => Set(ref _includePornographic, value); }
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public int Page { get => _page; private set => Set(ref _page, value); }
    public AsyncCommand SearchCommand { get; }
    public AsyncCommand NextPageCommand { get; }
    public AsyncCommand PreviousPageCommand { get; }
    private async Task LoadAsync(int page)
    {
        if (!string.IsNullOrWhiteSpace(Year) && (!int.TryParse(Year, out var requestedYear) || requestedYear is < 1 or > 9999))
        {
            Message = "Informe um ano válido entre 1 e 9999.";
            return;
        }
        IsBusy = true; Message = "Consultando MangaDex…";
        try
        {
            var ratings = new List<string> { "safe", "suggestive" };
            if (IncludeErotica || IncludePornographic) ratings.Add("erotica");
            if (IncludePornographic) ratings.Add("pornographic");
            var filters = new MangaSearchFilters(
                TranslatedLanguage: string.IsNullOrWhiteSpace(TranslatedLanguage) ? null : TranslatedLanguage.Trim(),
                Author: string.IsNullOrWhiteSpace(Author) ? null : Author.Trim(),
                Artist: string.IsNullOrWhiteSpace(Artist) ? null : Artist.Trim(),
                IncludedTags: ParseNames(IncludedTags), ExcludedTags: ParseNames(ExcludedTags),
                IncludedTagsMode: TagMode, ExcludedTagsMode: TagMode, ContentRatings: ratings, Sort: Sort,
                Year: int.TryParse(Year, out var year) ? year : null, Demographic: string.IsNullOrWhiteSpace(Demographic) ? null : Demographic);
            var result = await _catalog.SearchAsync(Query, page, PageSize,
                language: string.IsNullOrWhiteSpace(OriginalLanguage) ? null : OriginalLanguage.Trim(),
                status: string.IsNullOrWhiteSpace(Status) ? null : Status.ToLowerInvariant(), filters: filters);
            Results.Clear(); foreach (var item in result.Items) Results.Add(item);
            Page = page;
            Message = result.Total == 0 ? "Nenhum mangá encontrado. Tente outro título ou filtro." : $"{result.Total:N0} resultados · página {Page}";
        }
        catch (HttpRequestException ex) { Message = ex.Message; Results.Clear(); }
        catch (ArgumentException ex) { Message = ex.Message; Results.Clear(); }
        catch (TaskCanceledException) { Message = "A consulta demorou demais. Verifique a conexão e tente novamente."; Results.Clear(); }
        catch (Exception) { Message = "Não foi possível consultar a MangaDex. Tente novamente mais tarde."; Results.Clear(); }
        finally { IsBusy = false; }
    }
    private static IReadOnlyList<string> ParseNames(string value) => value.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}


