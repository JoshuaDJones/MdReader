using System.Collections.ObjectModel;
using MdReader.Models;
using MdReader.Services;

namespace MdReader;

public partial class MainPage : ContentPage
{
    private readonly DocumentLibrary _library = new();
    private readonly ObservableCollection<DocumentInfo> _visibleDocuments = [];
    private IReadOnlyList<DocumentInfo> _allDocuments = [];
    private bool _hasLoaded;

    public MainPage()
    {
        InitializeComponent();
        DocumentList.ItemsSource = _visibleDocuments;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await LoadLibraryAsync();
    }

    private async Task LoadLibraryAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            ErrorPanel.IsVisible = false;

            _allDocuments = await _library.GetDocumentsAsync();
            ApplyFilter(DocumentSearchBar.Text);
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

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) =>
        ApplyFilter(e.NewTextValue);

    private void OnSearchButtonPressed(object? sender, EventArgs e) =>
        DocumentSearchBar.Unfocus();

    private void ApplyFilter(string? searchText)
    {
        var term = searchText?.Trim();
        var matchingDocuments = string.IsNullOrEmpty(term)
            ? _allDocuments
            : _allDocuments.Where(document =>
                document.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                document.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                document.Category.Contains(term, StringComparison.OrdinalIgnoreCase));

        _visibleDocuments.Clear();
        foreach (var document in matchingDocuments)
        {
            _visibleDocuments.Add(document);
        }
    }

    private async void OnDocumentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not DocumentInfo document)
        {
            return;
        }

        DocumentList.SelectedItem = null;
        await Navigation.PushAsync(new ReaderPage(document, _library));
    }
}
