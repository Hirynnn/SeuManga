using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SeuManga.Application.Abstractions;
using Serilog;

namespace SeuManga.Desktop.Controls;

public sealed class MangaDexCoverImage : Image
{
    public static readonly DependencyProperty CoverUrlProperty = DependencyProperty.Register(
        nameof(CoverUrl), typeof(string), typeof(MangaDexCoverImage), new PropertyMetadata(null, OnCoverChanged));

    public static readonly DependencyProperty ImageProviderProperty = DependencyProperty.Register(
        nameof(ImageProvider), typeof(IChapterImageProvider), typeof(MangaDexCoverImage), new PropertyMetadata(null, OnCoverChanged));

    private CancellationTokenSource? _loading;

    public string? CoverUrl
    {
        get => (string?)GetValue(CoverUrlProperty);
        set => SetValue(CoverUrlProperty, value);
    }

    public IChapterImageProvider? ImageProvider
    {
        get => (IChapterImageProvider?)GetValue(ImageProviderProperty);
        set => SetValue(ImageProviderProperty, value);
    }

    private static void OnCoverChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args) =>
        ((MangaDexCoverImage)dependencyObject).LoadCover();

    private async void LoadCover()
    {
        _loading?.Cancel();
        var loading = new CancellationTokenSource();
        _loading = loading;
        Source = null;
        ToolTip = null;

        var url = CoverUrl;
        var provider = ImageProvider;
        if (string.IsNullOrWhiteSpace(url) || provider is null)
            return;

        try
        {
            var (content, _) = await provider.DownloadAsync(url, loading.Token);
            using var stream = new MemoryStream(content);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            if (ReferenceEquals(_loading, loading) && !loading.IsCancellationRequested)
                Source = bitmap;
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or IOException or FileFormatException or ArgumentException or NotSupportedException)
        {
            Log.Warning(ex, "Não foi possível carregar a capa MangaDex {CoverUrl}.", url);
            if (ReferenceEquals(_loading, loading))
                ToolTip = "Não foi possível carregar a capa. Verifique a conexão e tente novamente.";
        }
        finally
        {
            if (ReferenceEquals(_loading, loading))
                _loading = null;
            loading.Dispose();
        }
    }
}
