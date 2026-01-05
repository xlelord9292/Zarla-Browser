using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Zarla.Core.Data.Models;

namespace Zarla.Browser.Views;

public class BookmarkViewModel
{
    public long Id { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }

    public string Initial => GetInitial();
    public string Domain => GetDomain();

    private string GetInitial()
    {
        try
        {
            var uri = new Uri(Url);
            var host = uri.Host.Replace("www.", "");
            return host[0].ToString().ToUpper();
        }
        catch
        {
            return "?";
        }
    }

    private string GetDomain()
    {
        try
        {
            var uri = new Uri(Url);
            return uri.Host;
        }
        catch
        {
            return Url;
        }
    }
}

public partial class BookmarksPage : UserControl
{
    private readonly MainWindow _mainWindow;
    private List<BookmarkViewModel> _allBookmarks = new();

    public BookmarksPage(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        Loaded += BookmarksPage_Loaded;
    }

    private async void BookmarksPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadBookmarks();
    }

    private async Task LoadBookmarks()
    {
        var bookmarks = await App.Database.GetBookmarksAsync();

        _allBookmarks = bookmarks.Select(b => new BookmarkViewModel
        {
            Id = b.Id,
            Title = b.Title,
            Url = b.Url
        }).ToList();

        UpdateList(_allBookmarks);
    }

    private void UpdateList(List<BookmarkViewModel> items)
    {
        BookmarksList.ItemsSource = items;
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim().ToLower();

        if (string.IsNullOrEmpty(query))
        {
            UpdateList(_allBookmarks);
        }
        else
        {
            var filtered = _allBookmarks
                .Where(b => b.Title.ToLower().Contains(query) || b.Url.ToLower().Contains(query))
                .ToList();
            UpdateList(filtered);
        }
    }

    private void BookmarkItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is BookmarkViewModel item)
        {
            _mainWindow.NavigateToUrl(item.Url);
        }
    }

    private async void DeleteBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is long id)
        {
            e.Handled = true; // Prevent triggering parent click

            await App.Database.DeleteBookmarkAsync(id);
            _allBookmarks.RemoveAll(b => b.Id == id);
            UpdateList(_allBookmarks);
        }
    }
}
