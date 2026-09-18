using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SpaceLens.Core.Models;
using SpaceLens.Core.Settings;
using SpaceLens.ViewModels;

namespace SpaceLens;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TryRestoreWindowPlacement();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.SkipWelcomeOnStart && Vm.ShowWelcome)
            Vm.SkipWelcomeCommand.Execute(null);

        if (!string.IsNullOrWhiteSpace(App.DemoScanPath) && Directory.Exists(App.DemoScanPath))
            await Vm.ScanFolderPathAsync(App.DemoScanPath);
    }

    private MainViewModel Vm => (MainViewModel)DataContext;

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // Close (X) fully exits. Minimize uses the standard minimize button.
        SaveWindowPlacement();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        ShowInTaskbar = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Vm.CurrentPage == "Search")
        {
            Vm.RunSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>Fully quit the application (e.g. from Settings → Exit).</summary>
    public void ExitApplication()
    {
        SaveWindowPlacement();
        Application.Current.Shutdown();
    }

    private void TryRestoreWindowPlacement()
    {
        try
        {
            var settings = AppSettings.Load();
            if (settings.WindowWidth >= MinWidth && settings.WindowHeight >= MinHeight)
            {
                Width = settings.WindowWidth;
                Height = settings.WindowHeight;
            }
            if (settings.WindowLeft is double left && settings.WindowTop is double top
                && left > -20000 && top > -20000)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
            }
        }
        catch
        {
            // keep defaults
        }
    }

    private void SaveWindowPlacement()
    {
        try
        {
            var settings = AppSettings.Load();
            if (WindowState == WindowState.Normal)
            {
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
            }
            settings.Save();
        }
        catch
        {
            // ignore
        }
    }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: FileEntry file })
            Vm.ShowFileDetailsCommand.Execute(file);
    }

    private void FolderList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: FolderNode folder })
            Vm.ShowFolderDetailsCommand.Execute(folder);
    }

    private void CleanupList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: CleanupSuggestion item })
            Vm.ShowCleanupDetailsCommand.Execute(item);
    }

    private void CloseDetail_Click(object sender, RoutedEventArgs e)
    {
        Vm.HasDetail = false;
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        ExitApplication();
    }
}
