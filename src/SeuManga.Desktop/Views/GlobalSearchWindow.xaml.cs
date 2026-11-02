using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Anime;
using SeuManga.Application.Library;
using SeuManga.Application.Manga;
using SeuManga.Desktop.ViewModels;
namespace SeuManga.Desktop.Views;
public partial class GlobalSearchWindow : Window
{
    private readonly GlobalSearchViewModel _viewModel;
    private readonly IServiceProvider _services;
    public GlobalSearchWindow(GlobalSearchViewModel viewModel, IServiceProvider services) { InitializeComponent(); DataContext = _viewModel = viewModel; _services = services; Owner = System.Windows.Application.Current.MainWindow; }
    private void SearchBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) _viewModel.SearchCommand.Execute(null); }
    private void Suggestion_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string query) return;
        _viewModel.Query = query;
        _viewModel.SearchCommand.Execute(null);
    }
    private void Manga_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (MangaResults.SelectedItem is not MangaSearchResult manga) return;
        var details = new MangaDetailsWindow(_services.GetRequiredService<MangaCatalogService>(), _services.GetRequiredService<LibraryService>(), _services.GetRequiredService<IChapterImageProvider>(), manga);
        if (System.Windows.Application.Current.MainWindow is MainWindow shell)
            shell.NavigateToPage(details, $"Detalhes · {manga.Title}", "manga");
    }
    private void Anime_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AnimeResults.SelectedItem is not AnimeSearchResult anime) return;
        var window = _services.GetRequiredService<AnimeCatalogWindow>();
        var vm = (AnimeCatalogViewModel)window.DataContext; vm.Query = anime.Title; vm.SearchCommand.Execute(null);
        if (System.Windows.Application.Current.MainWindow is MainWindow shell)
            shell.NavigateToPage(window, "Explorar animes", "anime");
    }
}
