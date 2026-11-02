using System.Windows;
using System.Windows.Controls;
using SeuManga.Application.Library;
using SeuManga.Domain.Entities;
using SeuManga.Domain.Enums;
namespace SeuManga.Desktop.Views;
public sealed record ListLibraryCandidate(string ContentId, LibraryContentKind ContentKind, string Title, string? CoverUrl)
{
    public string DisplayLabel => $"{(ContentKind == LibraryContentKind.Manga ? "Mangá" : "Anime")} · {Title}";
}
public partial class CustomListsWindow : Window
{
    private readonly CustomListService _lists;
    private readonly LibraryService _library;
    private readonly AnimeTrackingService _anime;
    public CustomListsWindow(CustomListService lists, LibraryService library, AnimeTrackingService anime) { InitializeComponent(); _lists = lists; _library = library; _anime = anime; Owner = System.Windows.Application.Current.MainWindow; }
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var mangas = (await _library.GetLibraryAsync()).Select(x => new ListLibraryCandidate(x.MangaDexId, LibraryContentKind.Manga, x.Title, x.CoverUrl));
        var animes = (await _anime.GetLibraryAsync()).Select(x => new ListLibraryCandidate(x.JikanId.ToString(), LibraryContentKind.Anime, x.Title, x.ImageUrl));
        LibraryChoices.ItemsSource = mangas.Concat(animes).OrderBy(x => x.Title).ToArray();
        await RefreshListsAsync();
    }
    private async Task RefreshListsAsync() { Lists.ItemsSource = await _lists.GetListsAsync(); Message.Text = "As listas aceitam mangás e animes da biblioteca local."; }
    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        try { var list = await _lists.CreateAsync(NewListName.Text); NewListName.Clear(); await RefreshListsAsync(); Lists.SelectedValue = list.Id; Message.Text = "Lista criada."; }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
    private void Lists_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (Lists.SelectedItem is MangaCustomList selected) ListItems.ItemsSource = selected.Items; else ListItems.ItemsSource = null; }
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (Lists.SelectedItem is not MangaCustomList list || LibraryChoices.SelectedItem is not ListLibraryCandidate item) { Message.Text = "Escolha uma lista e um título salvo na biblioteca."; return; }
        try
        {
            await _lists.AddAsync(list.Id, item.ContentKind, item.ContentId, item.Title, item.CoverUrl);
            await RefreshListsAsync(); Lists.SelectedValue = list.Id; Message.Text = $"{(item.ContentKind == LibraryContentKind.Manga ? "Mangá" : "Anime")} adicionado à lista.";
        }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (Lists.SelectedItem is not MangaCustomList list || ListItems.SelectedItem is not MangaCustomListItem item) return;
        await _lists.RemoveAsync(list.Id, item.ContentKind, item.ContentId); await RefreshListsAsync(); Lists.SelectedValue = list.Id;
    }
}


