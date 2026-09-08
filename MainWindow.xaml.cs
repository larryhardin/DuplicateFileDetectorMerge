using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DuplicateFileDetectorMerge.Models;
using DuplicateFileDetectorMerge.Services;
using MessageBox = System.Windows.MessageBox;
using Window = System.Windows.Window;
using System.Threading.Tasks;

namespace DuplicateFileDetectorMerge;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Global (class) scope list that SearchCompareTo searches against.
    private static List<ListElement> compareToList = new();

    private List<ListElement> compareFromList = new();
    private List<ListElement> matchedEntries = new();
    private List<ListElement> filesToMigrate = new();

    private string? compareToFolder;
    private string? compareFromFolder;

    public MainWindow()
    {
        InitializeComponent();

        ComparisonActivity.Started += ShowCurrentComparison;
        ComparisonActivity.Ended += HideCurrentComparison;
        AppLog.MessageLogged += AppendLog;

        compareToFolder = @"H:\MediaCenter\Pictures";
        compareFromFolder = @"H:\Pictures";
        CompareToFolderTextBox.Text = compareToFolder;
        CompareFromFolderTextBox.Text = compareFromFolder;
        UpdateBuildListsButtonState();
    }

    private void SelectCompareToFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var candidate = dialog.SelectedPath;

        if (!string.IsNullOrEmpty(compareFromFolder) && FolderValidation.IsSubfolderOf(compareFromFolder, candidate))
        {
            MessageBox.Show(
                "The Compare From folder can not be a subfolder of the Compare To folder.",
                "Invalid Folder Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        compareToFolder = candidate;
        CompareToFolderTextBox.Text = compareToFolder;
        UpdateBuildListsButtonState();
    }

    private void SelectCompareFromFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var candidate = dialog.SelectedPath;

        if (!string.IsNullOrEmpty(compareToFolder) && FolderValidation.IsSubfolderOf(compareToFolder, candidate))
        {
            MessageBox.Show(
                "The Compare To folder can not be a subfolder of the Compare From folder.",
                "Invalid Folder Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        compareFromFolder = candidate;
        CompareFromFolderTextBox.Text = compareFromFolder;
        UpdateBuildListsButtonState();
    }

    private void UpdateBuildListsButtonState()
    {
        BuildListsButton.IsEnabled = !string.IsNullOrEmpty(compareToFolder) && !string.IsNullOrEmpty(compareFromFolder);
    }

    private void BuildListsButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(compareToFolder) || string.IsNullOrEmpty(compareFromFolder))
        {
            return;
        }

        compareToList = FileListBuilder.BuildList(compareToFolder);
        compareFromList = FileListBuilder.BuildList(compareFromFolder);

        CompareToCountText.Text = $"Compare To List: {compareToList.Count}";
        CompareFromCountText.Text = $"Compare From List: {compareFromList.Count}";

        Log($"Built Compare To list ({compareToList.Count} files) and Compare From list ({compareFromList.Count} files).");

        ExecuteComparisonButton.IsEnabled = true;
    }

    private Window? _progressWindow;
    private TextBlock? _progressText;

    private void ShowProgressWindow(int total)
    {
        _progressText = new TextBlock
        {
            Text = $"Processing file 0 out of {total} files",
            Margin = new Thickness(20),
            FontSize = 14,
            TextAlignment = System.Windows.TextAlignment.Center
        };

        _progressWindow = new Window
        {
            Title = "Comparing Files...",
            Width = 350,
            Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Content = _progressText
        };

        _progressWindow.Show();
    }

    private void UpdateProgress(int current, int total)
    {
        if (_progressText != null)
        {
            _progressText.Text = $"Processing file {current} out of {total} files";
        }
    }

    private void CloseProgressWindow()
    {
        _progressWindow?.Close();
        _progressWindow = null;
        _progressText = null;
    }

    private async void ExecuteComparisonButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var total = compareFromList.Count;
            var localMatched = new List<ListElement>();
            var localMigrate = new List<ListElement>();

            ShowProgressWindow(total);

            await Task.Run(() =>
            {
                for (var i = 0; i < compareFromList.Count; i++)
                {
                    var element = compareFromList[i];
                    var isMatched = SearchCompareTo(element);

                    if (isMatched)
                    {
                        localMatched.Add(element);
                    }
                    else
                    {
                        localMigrate.Add(element);
                    }

                    var current = i + 1;
                    Dispatcher.Invoke(() => UpdateProgress(current, total));
                }
            });

            matchedEntries = localMatched;
            filesToMigrate = localMigrate;

            MatchedCountText.Text = $"Matched: {matchedEntries.Count}";
            MigrateCountText.Text = $"To Migrate: {filesToMigrate.Count}";

            PrintResults();
        }
        catch (Exception ex)
        {
            // Prevent an unhandled exception mid-comparison from crashing the app on large file sets.
            Log($"Comparison failed: {ex}");
            MessageBox.Show(
                $"An error occurred while comparing files:\n{ex.Message}",
                "Comparison Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            CloseProgressWindow();
        }
    }

    /// <summary>
    /// Searches the class-scoped compareToList for an entry matching searchForElement.
    /// </summary>
    private static bool SearchCompareTo(ListElement searchForElement)
    {
        foreach (var compareToElement in compareToList)
        {
            var nameMatches = string.Equals(compareToElement.FileName, searchForElement.FileName, StringComparison.OrdinalIgnoreCase);
            if (!nameMatches)
            {
                continue;
            }

            var extensionMatches = string.Equals(compareToElement.Extension, searchForElement.Extension, StringComparison.OrdinalIgnoreCase);
            if (!extensionMatches)
            {
                continue;
            }

            var sizeMatches = compareToElement.FileSize == searchForElement.FileSize;

            if (!sizeMatches)
            {
                var handler = ExtensionHandlerRegistry.GetHandlerForExtension(searchForElement.Extension);
                sizeMatches = handler.AreSimilar(searchForElement, compareToElement);
            }

            if (sizeMatches)
            {
                return true;
            }
        }

        return false;
    }

    private static void LogMismatch(ListElement fromElement, ListElement toElement, string fieldName)
    {
        var message = $"Mismatch on '{fieldName}' for file '{fromElement.FileName}{fromElement.Extension}': " +
                      $"From folder='{fromElement.Path}', To folder='{toElement.Path}'";
        Log(message);
    }

    private void ShowCurrentComparison(ListElement fromElement, ListElement toElement)
    {
        Dispatcher.Invoke(() =>
        {
            CompareFromCurrentText.Text = $"Compare From: {fromElement.FileName}{fromElement.Extension} | Path={fromElement.Path}";
            CompareToCurrentText.Text = $"Compare To: {toElement.FileName}{toElement.Extension} | Path={toElement.Path}";
            CurrentComparisonPanel.Visibility = Visibility.Visible;
        });
    }

    private void HideCurrentComparison()
    {
        Dispatcher.Invoke(() => CurrentComparisonPanel.Visibility = Visibility.Collapsed);
    }

    private void PrintResults()
    {
        Log("--- Matched Entries ---");
        foreach (var element in matchedEntries)
        {
            Log(element.ToString());
        }

        Log("--- Files To Migrate ---");
        foreach (var element in filesToMigrate)
        {
            Log(element.ToString());
        }
    }

    private static void Log(string message) => AppLog.Log(message);

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() => LogTextBox.AppendText(message + Environment.NewLine));
    }
}
