using System.Windows;
using System.Windows.Controls;
using SeuManga.Application.Library;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Manga;
using SeuManga.Desktop.ViewModels;
namespace SeuManga.Desktop.Views;
public partial class MangaDetailsWindow : Window
{
    private readonly MangaCatalogService _catalog;
    private readonly LibraryService _library;
    private readonly IChapterImageProvider _images;
    public MangaDetailsWindow(MangaCatalogService catalog, LibraryService library, IChapterImageProvider images, MangaSearchResult manga)
    {
        InitializeComponent(); _catalog = catalog; _library = library; _images = images; DataContext = new MangaDetailsViewModel(catalog, manga); Loaded += async (_, _) => await ((MangaDetailsViewModel)DataContext).LoadAsync();
    }
    private async void AddToLibrary_Click(object sender, RoutedEventArgs e)
    {
        var manga = ((MangaDetailsViewModel)DataContext).Manga;
        try { await _library.AddAsync(manga.Id, manga.Title, manga.CoverUrl); ChapterMessage.Text = "Adicionado à biblioteca local."; }
        catch { ChapterMessage.Text = "Não foi possível salvar na biblioteca."; }
    }
    private async void ChapterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChapterList.SelectedItem is not MangaChapter chapter) return;
        var manga = ((MangaDetailsViewModel)DataContext).Manga;
        try
        {
            var pages = await _catalog.GetChapterPageUrlsAsync(chapter.Id);
            var reader = new ReaderWindow(chapter, ((MangaDetailsViewModel)DataContext).Chapters.ToArray(), _catalog, pages, manga, _library, _images);
            if (System.Windows.Application.Current.MainWindow is MainWindow shell)
                shell.NavigateToPage(reader, $"Leitura · {manga.Title}", "manga");
        }
        catch { MessageBox.Show(System.Windows.Application.Current.MainWindow, "Não foi possível abrir este capítulo. Ele pode estar indisponível.", "Leitor", MessageBoxButton.OK, MessageBoxImage.Information); }
        ChapterList.SelectedItem = null;
    }
}

