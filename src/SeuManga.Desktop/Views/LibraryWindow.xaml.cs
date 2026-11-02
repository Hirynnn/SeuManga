using System.Windows;
using SeuManga.Application.Library;
using SeuManga.Domain.Enums;
namespace SeuManga.Desktop.Views;
public sealed record AnimeLibraryProgress(int JikanId, string Title, string? ImageUrl, string ProgressText, double ProgressPercent, string StatusLabel, bool IsFavorite)
{
    public string FavoriteLabel => IsFavorite ? "♥ Favorito" : "♡ Favoritar";
}
public partial class LibraryWindow : Window
{
    private readonly LibraryService _library;
    private readonly AnimeTrackingService _anime;
    public LibraryWindow(LibraryService library, AnimeTrackingService anime) { InitializeComponent(); _library = library; _anime = anime; Owner = System.Windows.Application.Current.MainWindow; }
    private async void Window_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        var mangas = await _library.GetLibraryAsync(); Entries.ItemsSource = mangas;
        var animes = await _anime.GetLibraryAsync(); var display = new List<AnimeLibraryProgress>();
        foreach (var anime in animes)
        {
            var watched = (await _anime.GetWatchedEpisodesAsync(anime.JikanId)).Count;
            var total = anime.TotalEpisodes;
            var status = anime.Status switch { AnimeLibraryStatus.PlanToWatch => "Planejo assistir", AnimeLibraryStatus.Watching => "Assistindo", AnimeLibraryStatus.Completed => "Concluído", AnimeLibraryStatus.OnHold => "Pausado", AnimeLibraryStatus.Dropped => "Abandonado", _ => "Planejo assistir" };
            display.Add(new AnimeLibraryProgress(anime.JikanId, anime.Title, anime.ImageUrl, total is > 0 ? $"{watched} de {total} episódios assistidos" : $"{watched} episódios assistidos", total is > 0 ? Math.Min(100, watched * 100d / total.Value) : 0, status, anime.IsFavorite));
        }
        AnimeEntries.ItemsSource = display; Message.Text = $"{mangas.Count} mangás · {animes.Count} animes guardados localmente";
    }
    private async void Reading_Click(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.Tag is string id) { await _library.SetStatusAsync(id, LibraryStatus.Reading); await RefreshAsync(); } }
    private async void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var row = (await _library.GetLibraryAsync()).FirstOrDefault(x => x.MangaDexId == id);
        if (row is not null) { await _library.SetFavoriteAsync(id, !row.IsFavorite); await RefreshAsync(); }
    }
    private async void Remove_Click(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.Tag is string id) { await _library.RemoveAsync(id); await RefreshAsync(); } }
    private async void AnimeFavorite_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not int id) return;
        var current = (await _anime.GetLibraryAsync()).FirstOrDefault(x => x.JikanId == id);
        if (current is not null) { await _anime.SetFavoriteAsync(id, !current.IsFavorite); await RefreshAsync(); }
    }
    private async void AnimeStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not int id || button.CommandParameter is not string status || !Enum.TryParse<AnimeLibraryStatus>(status, out var parsed)) return;
        await _anime.SetStatusAsync(id, parsed); await RefreshAsync();
    }
    private async void AnimeRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is int id) { await _anime.RemoveAsync(id); await RefreshAsync(); }
    }
}
