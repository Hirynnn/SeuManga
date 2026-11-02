using System.Windows;
using SeuManga.Application.Library;
namespace SeuManga.Desktop.Views;
public partial class ReadingActivityWindow : Window
{
    private readonly LibraryService _library;
    private readonly bool _favorites;
    public ReadingActivityWindow(LibraryService library, bool favorites) { InitializeComponent(); _library = library; _favorites = favorites; Owner = System.Windows.Application.Current.MainWindow; Heading.Text = favorites ? "Favoritos" : "Histórico de leitura"; Summary.Text = favorites ? "Títulos favoritos salvos na sua biblioteca." : "Últimas páginas e capítulos acessados neste dispositivo."; }
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_favorites) Items.ItemsSource = (await _library.GetLibraryAsync()).Where(x => x.IsFavorite).ToArray();
        else Items.ItemsSource = await _library.GetHistoryAsync();
        if (Items.Items.Count == 0) Summary.Text = _favorites ? "Você ainda não favoritou nenhum mangá." : "Seu histórico aparecerá aqui depois que começar a ler.";
    }
}
