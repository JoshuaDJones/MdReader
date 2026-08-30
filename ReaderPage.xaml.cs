using MdReader.Models;
using MdReader.Services;

namespace MdReader;

public partial class ReaderPage : ContentPage
{
    private const int MinimumFontSize = 14;
    private const int MaximumFontSize = 26;

    private readonly DocumentInfo _document;
    private readonly DocumentLibrary _library;
    private string? _markdown;
    private int _fontSize = 18;
    private bool _hasLoaded;

    public ReaderPage(DocumentInfo document, DocumentLibrary library)
    {
        InitializeComponent();
        _document = document;
        _library = library;
        Title = document.Title;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Application.Current!.RequestedThemeChanged += OnRequestedThemeChanged;

        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await LoadDocumentAsync();
    }

    protected override void OnDisappearing()
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        }

        base.OnDisappearing();
    }

    private async Task LoadDocumentAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            ErrorPanel.IsVisible = false;

            _markdown = await _library.GetMarkdownAsync(_document);
            RenderDocument();
        }
        catch (Exception exception)
        {
            ErrorMessageLabel.Text = exception.Message;
            ErrorPanel.IsVisible = true;
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void RenderDocument()
    {
        if (_markdown is null)
        {
            return;
        }

        var useDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
        MarkdownWebView.Source = new HtmlWebViewSource
        {
            Html = _library.RenderHtml(_markdown, _document.Title, useDarkTheme, _fontSize)
        };
    }

    private void OnDecreaseTextSize(object? sender, EventArgs e)
    {
        _fontSize = Math.Max(MinimumFontSize, _fontSize - 2);
        RenderDocument();
    }

    private void OnIncreaseTextSize(object? sender, EventArgs e)
    {
        _fontSize = Math.Min(MaximumFontSize, _fontSize + 2);
        RenderDocument();
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) =>
        RenderDocument();

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return;
        }

        e.Cancel = true;
        await Launcher.Default.OpenAsync(uri);
    }
}
