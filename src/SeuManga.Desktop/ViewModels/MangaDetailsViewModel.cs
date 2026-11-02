using System.Collections.ObjectModel;
using SeuManga.Application.Manga;
namespace SeuManga.Desktop.ViewModels;
public sealed class MangaDetailsViewModel : ViewModelBase
{
    private readonly MangaCatalogService _catalog;
    private bool _loading;
    private string _message = "Carregando capítulos em português…";
    private int _chapterOffset;
    private int _chapterTotal;
    private const int PageSize = 100;
    private readonly HashSet<string> _chapterKeys = new(StringComparer.OrdinalIgnoreCase);
    public MangaDetailsViewModel(MangaCatalogService catalog, MangaSearchResult manga)
    {
        _catalog = catalog; Manga = manga;
        LoadCommand = new AsyncCommand(LoadAsync, () => !Loading);
        LoadMoreCommand = new AsyncCommand(LoadMoreAsync, () => !Loading && Chapters.Count < _chapterTotal);
    }
    public MangaSearchResult Manga { get; }
    public MangaDetails? Details { get; private set; }
    public ObservableCollection<MangaChapter> Chapters { get; } = [];
    public bool Loading { get => _loading; private set => Set(ref _loading, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public AsyncCommand LoadCommand { get; }
    public AsyncCommand LoadMoreCommand { get; }
    public async Task LoadAsync()
    {
        if (Details is not null || Loading) return;
        Loading = true;
        try { Details = await _catalog.GetDetailsAsync(Manga.Id); Notify(nameof(Details)); await LoadMoreCoreAsync(); }
        catch { Message = "Não foi possível carregar os detalhes e capítulos deste título."; }
        finally { Loading = false; }
    }
    private async Task LoadMoreAsync()
    {
        Loading = true;
        try { await LoadMoreCoreAsync(); }
        catch { Message = "Não foi possível carregar mais capítulos."; }
        finally { Loading = false; }
    }
    private async Task LoadMoreCoreAsync()
    {
        var page = await _catalog.GetChaptersAsync(Manga.Id, "pt-br", _chapterOffset, PageSize);
        _chapterTotal = page.Total; _chapterOffset += page.Items.Count;
        foreach (var chapter in page.Items)
        {
            var key = string.IsNullOrWhiteSpace(chapter.Number) ? chapter.Id : $"{chapter.Volume}:{chapter.Number}";
            if (_chapterKeys.Add(key)) Chapters.Add(chapter);
        }
        Message = Chapters.Count == 0 ? "Nenhum capítulo em português encontrado." : $"{Chapters.Count} de {page.Total} capítulos · português";
    }
}
