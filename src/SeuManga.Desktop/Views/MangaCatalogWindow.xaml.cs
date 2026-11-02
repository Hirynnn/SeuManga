using System.Windows;
using System.Windows.Controls;
using SeuManga.Application.Manga;
using SeuManga.Desktop.ViewModels;
namespace SeuManga.Desktop.Views;
public partial class MangaCatalogWindow : Window
{
    private readonly IServiceProvider _services;
    public MangaCatalogWindow(MangaCatalogViewModel viewModel, IServiceProvider services) { InitializeComponent(); DataContext = viewModel; _services = services; Owner = System.Windows.Application.Current.MainWindow; }
    private void MangaCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not MangaSearchResult manga) return;
        var details = new MangaDetailsWindow(
            (SeuManga.Application.Manga.MangaCatalogService)_services.GetService(typeof(SeuManga.Application.Manga.MangaCatalogService))!,
            (SeuManga.Application.Library.LibraryService)_services.GetService(typeof(SeuManga.Application.Library.LibraryService))!,
            (SeuManga.Application.Abstractions.IChapterImageProvider)_services.GetService(typeof(SeuManga.Application.Abstractions.IChapterImageProvider))!,
            manga);
        if (System.Windows.Application.Current.MainWindow is not MainWindow shell)
            throw new InvalidOperationException("A janela principal do SeuManga não está disponível.");
        shell.NavigateToPage(details, $"Detalhes · {manga.Title}", "manga");
    }
}
