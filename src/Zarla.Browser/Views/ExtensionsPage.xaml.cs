using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using Zarla.Core.Extensions;

namespace Zarla.Browser.Views;

public partial class ExtensionsPage : UserControl
{
    private ZarlaExtensionManager? _zarlaManager;
    private Action<string>? _navigateToUrl;
    private Action<ZarlaExtension>? _openBuilder;

    public ExtensionsPage()
    {
        InitializeComponent();
        Loaded += ExtensionsPage_Loaded;
    }

    public void SetZarlaExtensionManager(
        ZarlaExtensionManager manager,
        Action<string>? navigateToUrl = null,
        Action<ZarlaExtension>? openBuilder = null)
    {
        _zarlaManager = manager;
        _navigateToUrl = navigateToUrl;
        _openBuilder = openBuilder;

        _zarlaManager.ExtensionCreated += OnExtensionChanged;
        _zarlaManager.ExtensionUpdated += OnExtensionChanged;
        _zarlaManager.ExtensionDeleted += OnExtensionDeleted;
    }

    private void ExtensionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshExtensionsList();
        RefreshTemplatesList();
    }

    private void RefreshExtensionsList()
    {
        if (_zarlaManager == null) return;

        var extensions = _zarlaManager.Extensions.ToList();
        ExtensionsList.ItemsSource = null;
        ExtensionsList.ItemsSource = extensions;
        EmptyState.Visibility = extensions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshTemplatesList()
    {
        if (_zarlaManager == null) return;

        var templates = _zarlaManager.BuiltInTemplates.ToList();
        TemplatesList.ItemsSource = null;
        TemplatesList.ItemsSource = templates;
    }

    private void CreateExtension_Click(object sender, RoutedEventArgs e)
    {
        if (_openBuilder != null)
        {
            _openBuilder(null!);
        }
        else
        {
            _navigateToUrl?.Invoke("zarla://extension-builder");
        }
    }

    private void EditExtension_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string extensionId) return;
        if (_zarlaManager == null) return;

        var extension = _zarlaManager.GetExtension(extensionId);
        if (extension != null)
        {
            if (_openBuilder != null)
            {
                _openBuilder(extension);
            }
            else
            {
                _navigateToUrl?.Invoke($"zarla://extension-builder?id={extensionId}");
            }
        }
    }

    private void ExportExtension_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string extensionId) return;
        if (_zarlaManager == null) return;

        var extension = _zarlaManager.GetExtension(extensionId);
        if (extension == null) return;

        var json = _zarlaManager.ExportExtension(extension);

        var dialog = new SaveFileDialog
        {
            Title = "Export Extension",
            Filter = "Zarla Extension (*.zarla)|*.zarla|JSON (*.json)|*.json",
            FileName = $"{extension.Name}.zarla"
        };

        if (dialog.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(dialog.FileName, json);
            MessageBox.Show("Extension exported successfully!", "Exported",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RemoveExtension_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string extensionId) return;
        if (_zarlaManager == null) return;

        var extension = _zarlaManager.GetExtension(extensionId);
        if (extension == null) return;

        var result = MessageBox.Show(
            $"Remove extension '{extension.Name}'?",
            "Remove Extension",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _zarlaManager.DeleteExtension(extensionId);
        RefreshExtensionsList();
    }

    private void ToggleExtension_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle || toggle.Tag is not string extensionId) return;
        if (_zarlaManager == null) return;

        _zarlaManager.SetExtensionEnabled(extensionId, toggle.IsChecked ?? false);
    }

    private void InstallTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string templateId) return;
        if (_zarlaManager == null) return;

        var template = _zarlaManager.BuiltInTemplates.FirstOrDefault(t => t.Id == templateId);
        if (template == null) return;

        // Create extension from template
        var extension = _zarlaManager.CreateFromTemplate(template);

        MessageBox.Show($"'{extension.Name}' installed successfully!", "Installed",
            MessageBoxButton.OK, MessageBoxImage.Information);

        RefreshExtensionsList();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_zarlaManager == null) return;

        var dialog = new OpenFileDialog
        {
            Title = "Import Extension",
            Filter = "Zarla Extension (*.zarla)|*.zarla|JSON (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = System.IO.File.ReadAllText(dialog.FileName);
                var extension = _zarlaManager.ImportExtension(json);

                if (extension != null)
                {
                    MessageBox.Show($"'{extension.Name}' imported successfully!", "Imported",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshExtensionsList();
                }
                else
                {
                    MessageBox.Show("Failed to import extension. Invalid format.", "Import Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Import Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnExtensionChanged(object? sender, ZarlaExtension extension)
    {
        Dispatcher.Invoke(RefreshExtensionsList);
    }

    private void OnExtensionDeleted(object? sender, string extensionId)
    {
        Dispatcher.Invoke(RefreshExtensionsList);
    }
}
