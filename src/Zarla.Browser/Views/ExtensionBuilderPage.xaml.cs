using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Zarla.Core.Extensions;

namespace Zarla.Browser.Views;

public partial class ExtensionBuilderPage : UserControl
{
    private ZarlaExtensionManager? _manager;
    private ZarlaExtension? _currentExtension;
    private ExtensionBlock? _selectedBlock;
    private Action<string>? _navigateBack;

    public ExtensionBuilderPage()
    {
        InitializeComponent();
        LoadBlockPalette();
    }

    public void Initialize(ZarlaExtensionManager manager, ZarlaExtension? extension = null, Action<string>? navigateBack = null)
    {
        _manager = manager;
        _navigateBack = navigateBack;

        if (extension != null)
        {
            _currentExtension = extension;
        }
        else
        {
            _currentExtension = new ZarlaExtension
            {
                Name = "New Extension",
                Description = "My custom extension"
            };
        }

        LoadExtension();
    }

    private void LoadBlockPalette()
    {
        BlockPalette.Children.Clear();

        var blockTypes = BlockTypeInfo.GetAllTypes();
        var categories = blockTypes.GroupBy(b => b.Category);

        foreach (var category in categories)
        {
            // Category header
            var header = new TextBlock
            {
                Text = category.Key,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                Margin = new Thickness(0, 12, 0, 8)
            };
            BlockPalette.Children.Add(header);

            // Block items
            foreach (var blockType in category)
            {
                var item = CreatePaletteItem(blockType);
                BlockPalette.Children.Add(item);
            }
        }
    }

    private Border CreatePaletteItem(BlockTypeInfo blockType)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 6),
            Cursor = Cursors.Hand,
            Tag = blockType
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new TextBlock
        {
            Text = blockType.Icon,
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(icon, 0);

        var textStack = new StackPanel();
        textStack.Children.Add(new TextBlock
        {
            Text = blockType.Name,
            FontSize = 13,
            Foreground = Brushes.White
        });
        textStack.Children.Add(new TextBlock
        {
            Text = blockType.Description,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(textStack, 1);

        grid.Children.Add(icon);
        grid.Children.Add(textStack);
        border.Child = grid;

        // Hover effect
        border.MouseEnter += (s, e) => border.Background = new SolidColorBrush(Color.FromRgb(55, 55, 55));
        border.MouseLeave += (s, e) => border.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));

        // Click to add block
        border.MouseLeftButtonDown += (s, e) =>
        {
            if (border.Tag is BlockTypeInfo info)
            {
                AddBlock(info);
            }
        };

        return border;
    }

    private void LoadExtension()
    {
        if (_currentExtension == null) return;

        ExtensionName.Text = _currentExtension.Name;
        ExtensionDescription.Text = _currentExtension.Description;

        // Load active sites
        RefreshActiveSites();

        // Load blocks
        RefreshBlocksList();
    }

    private void RefreshActiveSites()
    {
        if (_currentExtension == null) return;

        ActiveSitesPanel.Children.Clear();

        foreach (var site in _currentExtension.ActiveOnSites)
        {
            var chip = CreateSiteChip(site);
            ActiveSitesPanel.Children.Add(chip);
        }
    }

    private Border CreateSiteChip(string site)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 6, 4),
            Margin = new Thickness(0, 0, 6, 6)
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(new TextBlock
        {
            Text = site,
            FontSize = 12,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });

        var removeBtn = new Button
        {
            Content = "✕",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0, 0, 0),
            FontSize = 10,
            Cursor = Cursors.Hand,
            Tag = site
        };
        removeBtn.Click += (s, e) =>
        {
            if (removeBtn.Tag is string siteToRemove && _currentExtension != null)
            {
                _currentExtension.ActiveOnSites.Remove(siteToRemove);
                RefreshActiveSites();
            }
        };
        stack.Children.Add(removeBtn);

        border.Child = stack;
        return border;
    }

    private void AddSite_Click(object sender, RoutedEventArgs e)
    {
        if (_currentExtension == null) return;

        var site = ActiveSitesInput.Text.Trim();
        if (string.IsNullOrEmpty(site)) return;

        if (!_currentExtension.ActiveOnSites.Contains(site))
        {
            _currentExtension.ActiveOnSites.Add(site);
            RefreshActiveSites();
        }

        ActiveSitesInput.Text = "";
    }

    private void RefreshBlocksList()
    {
        if (_currentExtension == null) return;

        // Clear existing blocks (except empty state)
        var toRemove = BlocksList.Children.OfType<Border>()
            .Where(b => b != EmptyBlocksState)
            .ToList();
        foreach (var item in toRemove)
        {
            BlocksList.Children.Remove(item);
        }

        EmptyBlocksState.Visibility = _currentExtension.Blocks.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        BlockCount.Text = $"({_currentExtension.Blocks.Count})";

        foreach (var block in _currentExtension.Blocks)
        {
            var blockItem = CreateBlockItem(block);
            BlocksList.Children.Add(blockItem);
        }
    }

    private Border CreateBlockItem(ExtensionBlock block)
    {
        var blockInfo = BlockTypeInfo.GetAllTypes().FirstOrDefault(b => b.Type == block.Type);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(37, 37, 37)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand,
            Tag = block,
            Opacity = block.IsEnabled ? 1.0 : 0.5
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Icon
        var icon = new TextBlock
        {
            Text = blockInfo?.Icon ?? "🧩",
            FontSize = 24,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(icon, 0);

        // Info
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock
        {
            Text = blockInfo?.Name ?? block.Type.ToString(),
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = Brushes.White
        });

        var label = string.IsNullOrEmpty(block.Label) ? "Click to configure" : block.Label;
        infoStack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(infoStack, 1);

        // Status indicator
        var status = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = block.IsEnabled
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                : new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(status, 2);

        grid.Children.Add(icon);
        grid.Children.Add(infoStack);
        grid.Children.Add(status);
        border.Child = grid;

        // Hover effect
        border.MouseEnter += (s, e) =>
        {
            if (border.Tag != _selectedBlock)
                border.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
        };
        border.MouseLeave += (s, e) =>
        {
            if (border.Tag != _selectedBlock)
                border.Background = new SolidColorBrush(Color.FromRgb(37, 37, 37));
        };

        // Click to configure
        border.MouseLeftButtonDown += (s, e) =>
        {
            if (border.Tag is ExtensionBlock b)
            {
                SelectBlock(b);
            }
        };

        return border;
    }

    private void AddBlock(BlockTypeInfo blockType)
    {
        if (_currentExtension == null) return;

        var block = new ExtensionBlock
        {
            Type = blockType.Type,
            Label = blockType.Name
        };

        _currentExtension.Blocks.Add(block);
        RefreshBlocksList();
        SelectBlock(block);
    }

    private void SelectBlock(ExtensionBlock block)
    {
        _selectedBlock = block;
        var blockInfo = BlockTypeInfo.GetAllTypes().FirstOrDefault(b => b.Type == block.Type);

        // Update config panel header
        ConfigBlockIcon.Text = blockInfo?.Icon ?? "🧩";
        ConfigBlockType.Text = blockInfo?.Name ?? block.Type.ToString();
        BlockEnabledCheckbox.IsChecked = block.IsEnabled;

        // Build config fields
        ConfigFields.Children.Clear();

        // Label field (always present)
        AddConfigField("label", "Label", block.Label, "Describe what this block does");

        // Block-specific fields
        if (blockInfo != null)
        {
            foreach (var field in blockInfo.ConfigFields)
            {
                var value = block.Config.TryGetValue(field.Key, out var v) ? v : "";
                AddConfigField(field.Key, field.Label, value, field.Placeholder, field.Multiline);
            }
        }

        // Show config panel
        ConfigPanel.Visibility = Visibility.Visible;

        // Highlight selected block in list
        foreach (var item in BlocksList.Children.OfType<Border>())
        {
            if (item.Tag == block)
            {
                item.Background = new SolidColorBrush(Color.FromRgb(55, 75, 95));
            }
            else if (item != EmptyBlocksState)
            {
                item.Background = new SolidColorBrush(Color.FromRgb(37, 37, 37));
            }
        }
    }

    private void AddConfigField(string key, string label, string value, string placeholder, bool multiline = false)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
            Margin = new Thickness(0, 0, 0, 6)
        });

        if (multiline)
        {
            var textBox = new TextBox
            {
                Text = value,
                Tag = key,
                FontSize = 13,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 37)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                MinHeight = 100,
                MaxHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas")
            };
            textBox.TextChanged += ConfigField_TextChanged;
            stack.Children.Add(textBox);
        }
        else
        {
            var textBox = new TextBox
            {
                Text = value,
                Tag = key,
                FontSize = 13,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 37)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8)
            };

            // Add placeholder behavior
            if (string.IsNullOrEmpty(value))
            {
                textBox.Text = placeholder;
                textBox.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            }

            textBox.GotFocus += (s, e) =>
            {
                if (textBox.Foreground is SolidColorBrush brush &&
                    brush.Color == Color.FromRgb(102, 102, 102))
                {
                    textBox.Text = "";
                    textBox.Foreground = Brushes.White;
                }
            };

            textBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Text = placeholder;
                    textBox.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                }
            };

            textBox.TextChanged += ConfigField_TextChanged;
            stack.Children.Add(textBox);
        }

        ConfigFields.Children.Add(stack);
    }

    private void ConfigField_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_selectedBlock == null || sender is not TextBox textBox || textBox.Tag is not string key)
            return;

        // Ignore placeholder text
        if (textBox.Foreground is SolidColorBrush brush &&
            brush.Color == Color.FromRgb(102, 102, 102))
            return;

        var value = textBox.Text;

        if (key == "label")
        {
            _selectedBlock.Label = value;
        }
        else
        {
            _selectedBlock.Config[key] = value;
        }

        RefreshBlocksList();
        // Re-select to maintain highlight
        foreach (var item in BlocksList.Children.OfType<Border>())
        {
            if (item.Tag == _selectedBlock)
            {
                item.Background = new SolidColorBrush(Color.FromRgb(55, 75, 95));
            }
        }
    }

    private void BlockEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_selectedBlock == null) return;
        _selectedBlock.IsEnabled = BlockEnabledCheckbox.IsChecked ?? true;
        RefreshBlocksList();

        // Re-select to maintain highlight
        foreach (var item in BlocksList.Children.OfType<Border>())
        {
            if (item.Tag == _selectedBlock)
            {
                item.Background = new SolidColorBrush(Color.FromRgb(55, 75, 95));
            }
        }
    }

    private void RemoveBlock_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBlock == null || _currentExtension == null) return;

        _currentExtension.Blocks.Remove(_selectedBlock);
        _selectedBlock = null;
        ConfigPanel.Visibility = Visibility.Collapsed;
        RefreshBlocksList();
    }

    private void CloseConfig_Click(object sender, RoutedEventArgs e)
    {
        _selectedBlock = null;
        ConfigPanel.Visibility = Visibility.Collapsed;

        // Remove highlight from all blocks
        foreach (var item in BlocksList.Children.OfType<Border>())
        {
            if (item != EmptyBlocksState)
            {
                item.Background = new SolidColorBrush(Color.FromRgb(37, 37, 37));
            }
        }
    }

    private void ExtensionName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_currentExtension != null)
        {
            _currentExtension.Name = ExtensionName.Text;
        }
    }

    private void ExtensionDescription_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_currentExtension != null)
        {
            _currentExtension.Description = ExtensionDescription.Text;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_manager == null || _currentExtension == null) return;

        // Check if this is a new extension or existing
        var existing = _manager.GetExtension(_currentExtension.Id);
        if (existing == null)
        {
            // It's new - add it via manager
            var newExt = _manager.CreateExtension(_currentExtension.Name, _currentExtension.Description);
            newExt.Blocks = _currentExtension.Blocks;
            newExt.ActiveOnSites = _currentExtension.ActiveOnSites;
            newExt.Icon = _currentExtension.Icon;
            _manager.UpdateExtension(newExt);
            _currentExtension = newExt;
        }
        else
        {
            _manager.UpdateExtension(_currentExtension);
        }

        MessageBox.Show($"Extension '{_currentExtension.Name}' saved successfully!", "Saved",
            MessageBoxButton.OK, MessageBoxImage.Information);

        _navigateBack?.Invoke("zarla://extensions");
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_manager == null || _currentExtension == null) return;

        var json = _manager.ExportExtension(_currentExtension);

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Extension",
            Filter = "Zarla Extension (*.zarla)|*.zarla|JSON (*.json)|*.json",
            FileName = $"{_currentExtension.Name}.zarla"
        };

        if (dialog.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(dialog.FileName, json);
            MessageBox.Show("Extension exported successfully!", "Exported",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
