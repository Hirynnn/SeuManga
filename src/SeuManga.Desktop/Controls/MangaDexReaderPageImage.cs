using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Serilog;
using SeuManga.Application.Abstractions;

namespace SeuManga.Desktop.Controls;

public sealed class MangaDexReaderPageImage : Image
{
    public static readonly DependencyProperty PageUrlProperty = DependencyProperty.Register(
        nameof(PageUrl), typeof(string), typeof(MangaDexReaderPageImage), new PropertyMetadata(null, OnPageChanged));

    public static readonly DependencyProperty ImageProviderProperty = DependencyProperty.Register(
        nameof(ImageProvider), typeof(IChapterImageProvider), typeof(MangaDexReaderPageImage), new PropertyMetadata(null, OnPageChanged));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(MangaDexReaderPageImage), new PropertyMetadata(false, OnActiveChanged));

    private CancellationTokenSource? _loading;
    private Task? _loadingTask;

    public MangaDexReaderPageImage()
    {
        Stretch = System.Windows.Media.Stretch.Uniform;
        HorizontalAlignment = HorizontalAlignment.Center;
        Height = 520;
        Loaded += async (_, _) => await LoadPageAsync();
        Unloaded += (_, _) => ReleasePage();
    }

    public string? PageUrl
    {
        get => (string?)GetValue(PageUrlProperty);
        set => SetValue(PageUrlProperty, value);
    }

    public IChapterImageProvider? ImageProvider
    {
        get => (IChapterImageProvider?)GetValue(ImageProviderProperty);
        set => SetValue(ImageProviderProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private static void OnPageChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var image = (MangaDexReaderPageImage)dependencyObject;
        image.ReleasePage();
        image.Height = 520;
        _ = image.LoadPageAsync();
    }

    private static void OnActiveChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var image = (MangaDexReaderPageImage)dependencyObject;
        if ((bool)args.NewValue)
            _ = image.LoadPageAsync();
        else
            image.ReleasePage();
    }

    public Task EnsureLoadedAsync() => LoadPageAsync();

    private Task LoadPageAsync()
    {
        if (Source is not null || !IsActive)
            return Task.CompletedTask;
        if (_loadingTask is not null)
            return _loadingTask;
        var url = PageUrl;
        var provider = ImageProvider;
        if (!IsLoaded || string.IsNullOrWhiteSpace(url) || provider is null)
            return Task.CompletedTask;

        var loading = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _loading = loading;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _loadingTask = completion.Task;
        _ = LoadPageCoreAsync(url, provider, loading, completion);
        return _loadingTask;
    }

    private async Task LoadPageCoreAsync(string url, IChapterImageProvider provider, CancellationTokenSource loading, TaskCompletionSource<bool> completion)
    {
        try
        {
            var (content, _) = await provider.DownloadAsync(url, loading.Token);
            using var stream = new MemoryStream(content, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            if (ReferenceEquals(_loading, loading) && !loading.IsCancellationRequested)
            {
                Source = bitmap;
                Height = double.NaN;
            }
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível carregar a página MangaDex {PageUrl}.", url);
            if (ReferenceEquals(_loading, loading))
                ToolTip = "Não foi possível carregar esta página. Verifique a conexão e tente novamente.";
        }
        finally
        {
            if (ReferenceEquals(_loading, loading))
            {
                _loading = null;
                _loadingTask = null;
            }
            loading.Dispose();
            completion.TrySetResult(true);
        }
    }

    private void ReleasePage()
    {
        CancelLoad();
        if (Source is not null)
            Height = Math.Max(520, ActualHeight);
        Source = null;
        ToolTip = null;
    }

    private void CancelLoad()
    {
        var loading = _loading;
        _loading = null;
        _loadingTask = null;
        if (loading is null)
            return;
        try
        {
            loading.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
