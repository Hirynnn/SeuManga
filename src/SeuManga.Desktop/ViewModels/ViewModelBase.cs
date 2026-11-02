using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Windows.Input;
namespace SeuManga.Desktop.ViewModels;
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (Equals(field, value)) return false; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); return true; }
    protected void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    protected static string ProviderErrorMessage(string provider, HttpRequestException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is AuthenticationException authentication && authentication.Message.Contains("TLS alert: 'ProtocolVersion'", StringComparison.OrdinalIgnoreCase))
                return $"{provider} rejeitou a versão TLS negociada pelo Windows. Confira se o Windows, o Microsoft Edge WebView2 e o antivírus estão atualizados.";
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is AuthenticationException || current is HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError })
                return $"Não foi possível estabelecer uma conexão segura com {provider}. Confira a data e hora do Windows e se VPN, proxy ou antivírus estão interceptando HTTPS.";
        return exception.Message;
    }
}
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running && (canExecute?.Invoke() ?? true);
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public async void Execute(object? parameter) { if (!CanExecute(parameter)) return; _running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty); try { await execute(); } finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); } }
}
