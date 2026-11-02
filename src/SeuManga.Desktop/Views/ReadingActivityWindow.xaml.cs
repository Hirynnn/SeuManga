using System.Windows;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Library;
using SeuManga.Domain.Entities;
using SeuManga.Domain.Enums;
using Serilog;
namespace SeuManga.Desktop.Views;
public sealed record ReadingActivityCard(ReadingHistoryEntry Entry, int ProgressPercent, string ProgressLabel, string ChapterLabel, string UpdatedLabel);
public sealed record FavoriteActivityCard(string Title, string? CoverUrl, string StatusLabel);
public partial class ReadingActivityWindow : Window
{
    private readonly LibraryService _library;
    private readonly bool _favorites;
    public ReadingActivityWindow(LibraryService library, IChapterImageProvider imageProvider, bool favorites) { InitializeComponent(); _library = library; ImageProvider = imageProvider; _favorites = favorites; Owner = System.Windows.Application.Current.MainWindow; Heading.Text = favorites ? "Favoritos" : "Histórico"; Summary.Text = favorites ? "Suas obras marcadas com coração." : "Seu acompanhamento mais recente primeiro."; HistoryView.Visibility = favorites ? Visibility.Collapsed : Visibility.Visible; FavoritesView.Visibility = favorites ? Visibility.Visible : Visibility.Collapsed; }
    public IChapterImageProvider ImageProvider { get; }
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_favorites)
            {
                var favorites = (await _library.GetLibraryAsync()).Where(x => x.IsFavorite)
                    .Select(x => new FavoriteActivityCard(x.Title, x.CoverUrl, StatusLabel(x.Status)))
                    .ToArray();
                FavoriteItems.ItemsSource = favorites;
                if (favorites.Length == 0)
                    ShowEmptyMessage("Você ainda não favoritou nenhum mangá.");
                else
                    Summary.Text = $"{favorites.Length} obra(s) marcadas com coração.";
                return;
            }

            var history = (await _library.GetHistoryAsync()).Select(CreateActivityCard).ToArray();
            HistoryItems.ItemsSource = history;
            if (history.Length == 0)
                ShowEmptyMessage("Seu histórico aparecerá aqui depois que começar a ler.");
            else
                Summary.Text = $"{history.Length} leitura(s) recente(s) neste dispositivo.";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível carregar a tela de atividade");
            ShowEmptyMessage("Não foi possível carregar estes dados. Tente novamente.");
        }
    }

    private void ShowEmptyMessage(string message)
    {
        Summary.Text = message;
        EmptyMessage.Text = message;
        EmptyMessage.Visibility = Visibility.Visible;
    }

    private static ReadingActivityCard CreateActivityCard(ReadingHistoryEntry entry)
    {
        var progress = entry.PageCount > 0
            ? Math.Clamp((int)Math.Round((entry.LastPage + 1) * 100d / entry.PageCount), 0, 100)
            : 0;
        var pageLabel = entry.PageCount > 0
            ? $"{Math.Clamp(entry.LastPage + 1, 1, entry.PageCount)} de {entry.PageCount} páginas"
            : "Progresso indisponível";
        var chapterLabel = string.IsNullOrWhiteSpace(entry.ChapterNumber)
            ? "Capítulo"
            : $"Último capítulo: {entry.ChapterNumber}";
        return new ReadingActivityCard(entry, progress, pageLabel, chapterLabel, entry.UpdatedAt.ToLocalTime().ToString("g"));
    }

    private static string StatusLabel(LibraryStatus status) => status switch
    {
        LibraryStatus.Reading => "Lendo",
        LibraryStatus.Completed => "Concluído",
        LibraryStatus.OnHold => "Pausado",
        LibraryStatus.Dropped => "Abandonado",
        _ => "Quero ler"
    };
}
