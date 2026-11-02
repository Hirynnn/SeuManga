using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SeuManga.Application.Anime;
using SeuManga.Application.Streaming;
using SeuManga.Desktop.ViewModels;
namespace SeuManga.Desktop.Views;
public partial class AnimeCatalogWindow : Window
{
    private readonly AnimeCatalogViewModel _viewModel;
    public AnimeCatalogWindow(AnimeCatalogViewModel viewModel) { InitializeComponent(); DataContext = _viewModel = viewModel; Owner = System.Windows.Application.Current.MainWindow; }
    private void Streaming_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string link && Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps) Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }
    private async void Recommendation_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AnimeSearchResult recommendation) return;
        if (!_viewModel.Results.Contains(recommendation)) _viewModel.Results.Add(recommendation);
        AnimeList.SelectedItem = recommendation; AnimeList.ScrollIntoView(recommendation);
    }
    private async void AnimeList_SelectionChanged(object sender, SelectionChangedEventArgs e) => await _viewModel.SelectAnimeAsync(AnimeList.SelectedItem as AnimeSearchResult);
    private async void LoadTmdbSeason_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TmdbSeasonViewModel season) await _viewModel.LoadTmdbSeasonAsync(season);
    }
    private async void AddAnime_Click(object sender, RoutedEventArgs e) { await _viewModel.AddSelectedAnimeAsync(); }
    private async void EpisodeWatched_Click(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.Tag is TrackedAnimeEpisode ep) await _viewModel.ToggleEpisodeAsync(ep); }
}



