using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SeuManga.Application.Abstractions;
using SeuManga.Infrastructure.Persistence;
using SeuManga.Infrastructure.Security;
using Serilog;
namespace SeuManga.Desktop.Views;
public partial class SettingsWindow : Window
{
    private readonly ISecureSettingsStore _store;
    private readonly ApiConnectionTester _tester;
    private readonly DatabaseBackupService _backup;
    public SettingsWindow(ISecureSettingsStore store, ApiConnectionTester tester, DatabaseBackupService backup)
    {
        InitializeComponent();
        _store = store;
        _tester = tester;
        _backup = backup;
        Owner = System.Windows.Application.Current.MainWindow;
        VersionText.Text = $"Versão {typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "desconhecida"}";
        SelectSection("Appearance");
    }
    private void SettingsSection_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string section)
            SelectSection(section);
    }
    private void SelectSection(string section)
    {
        AppearanceSection.Visibility = section == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        ReadingSection.Visibility = section == "Reading" ? Visibility.Visible : Visibility.Collapsed;
        LibrarySection.Visibility = section == "Library" ? Visibility.Visible : Visibility.Collapsed;
        LanguageSection.Visibility = section == "Language" ? Visibility.Visible : Visibility.Collapsed;
        StorageSection.Visibility = section == "Storage" ? Visibility.Visible : Visibility.Collapsed;
        SourcesSection.Visibility = section == "Sources" ? Visibility.Visible : Visibility.Collapsed;
        AboutSection.Visibility = section == "About" ? Visibility.Visible : Visibility.Collapsed;

        var navigation = new (string Key, Button Button)[]
        {
            ("Appearance", AppearanceNav), ("Reading", ReadingNav), ("Library", LibraryNav),
            ("Language", LanguageNav), ("Storage", StorageNav), ("Sources", SourcesNav), ("About", AboutNav)
        };
        foreach (var item in navigation)
        {
            var selected = item.Key == section;
            item.Button.Background = selected ? new SolidColorBrush(Color.FromRgb(51, 42, 64)) : Brushes.Transparent;
            item.Button.Foreground = selected ? new SolidColorBrush(Color.FromRgb(245, 243, 255)) : new SolidColorBrush(Color.FromRgb(161, 161, 170));
        }
    }
    private async void SaveTmdb_Click(object sender, RoutedEventArgs e) => await SaveAsync("TMDB", TmdbKey.Password, TmdbStatus);
    private async void SaveWatchmode_Click(object sender, RoutedEventArgs e) => await SaveAsync("Watchmode", WatchmodeKey.Password, WatchmodeStatus);
    private async Task SaveAsync(string name, string value, System.Windows.Controls.TextBlock output)
    {
        if (string.IsNullOrWhiteSpace(value)) { output.Text = "Digite uma chave antes de salvar."; return; }
        try { await _store.SaveSecretAsync(name, value); output.Text = "Chave protegida salva para este usuário do Windows."; if (name == "TMDB") TmdbKey.Clear(); else WatchmodeKey.Clear(); }
        catch { output.Text = "Não foi possível proteger e salvar a chave neste dispositivo."; }
    }
    private async void TestTmdb_Click(object sender, RoutedEventArgs e) => TmdbStatus.Text = await TestAsync("TMDB");
    private async void TestWatchmode_Click(object sender, RoutedEventArgs e) => WatchmodeStatus.Text = await TestAsync("Watchmode");
    private async Task<string> TestAsync(string provider) { try { return await _tester.TestAsync(provider); } catch { return "Não foi possível testar a conexão agora."; } }
    private async void CreateBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = "Criar backup do SeuManga", Filter = "Banco SQLite (*.db)|*.db", DefaultExt = ".db", AddExtension = true, FileName = $"seumanga-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db" };
        if (dialog.ShowDialog(System.Windows.Application.Current.MainWindow) != true) return;
        try
        {
            await _backup.BackupAsync(dialog.FileName);
            BackupStatus.Text = "Backup criado. As chaves de API não são incluídas.";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível criar o backup local");
            BackupStatus.Text = "Não foi possível criar o backup. Verifique o caminho e o espaço disponível.";
        }
    }
    private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Restaurar backup do SeuManga", Filter = "Banco SQLite (*.db)|*.db", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(System.Windows.Application.Current.MainWindow) != true) return;
        if (MessageBox.Show(System.Windows.Application.Current.MainWindow, "A restauração substituirá sua biblioteca, listas e progresso atuais. Uma cópia temporária será criada para permitir a recuperação se houver falha. Deseja continuar?", "Restaurar backup", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            await _backup.RestoreAsync(dialog.FileName);
            BackupStatus.Text = "Backup restaurado com sucesso.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Não foi possível restaurar o backup local");
            BackupStatus.Text = "Não foi possível restaurar o backup. Os dados anteriores foram preservados quando possível.";
        }
    }
}
