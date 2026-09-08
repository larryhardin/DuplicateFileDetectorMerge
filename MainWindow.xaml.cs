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
using System.IO;
using System.Threading.Tasks;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;

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

    // Initialize the hashing algorithm once (PerceptualHash is robust for edits/resizes)


    public MainWindow()
    {
        InitializeComponent();

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

    private static readonly PerceptualHash _hasher = new PerceptualHash();

    // File extensions decodable by ImageSharp's built-in decoders, and therefore supported by PerceptualHash.
    private static readonly HashSet<string> SupportedPerceptualHashExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".pbm", ".cur", ".jpeg", ".jpg", ".tiff", ".tif", ".gif",
        ".exr", ".tga", ".ani", ".webp", ".ico", ".png", ".qoi"
    };

    /// <summary>Returns true if extension (with or without a leading dot) is decodable by PerceptualHash.</summary>
    private static bool IsPerceptualHashSupported(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        var normalized = extension.StartsWith('.') ? extension : "." + extension;
        return SupportedPerceptualHashExtensions.Contains(normalized);
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

            //If the filename and extension match, but the size does not match, perform a more detailed check using perceptual hashing.
            if (extensionMatches && !sizeMatches)
            {
                if (IsPerceptualHashSupported(compareToElement.Extension))
                {
                    double fileSizeRatio = (double)compareToElement.FileSize / searchForElement.FileSize;
                    //This range is arbitrary and allows for a very small difference in file sizes.
                    //Note that the < 1 and > 1 bounds relate to how the from file could be slightly smaller or larger than the compareTo file.
                    //While the metadata of the file can be different, the actual content might still be nearly identical.
                    //The check is done to prevent comparing files that are clearly different in size, which would be unnecessary.
                    if (0.999 < fileSizeRatio && fileSizeRatio < 1.001) //let's do some magics...
                    {
                        //Log a message that the comparison is occuring, listing both files and paths
                        Console.WriteLine($"Comparing files: {searchForElement.Path}/{searchForElement.FileName}{searchForElement.Extension} and {compareToElement.Path}/{compareToElement.FileName}{compareToElement.Extension}");

                        ShowCurrentComparison(searchForElement, compareToElement);

                        // Consider the files as matching in size within a small tolerance ( using declaration ensures streams are properly disposed )
                        using var stream1 = File.OpenRead(searchForElement.Path + "/" + searchForElement.FileName + searchForElement.Extension);
                        using var stream2 = File.OpenRead(compareToElement.Path + "/" + compareToElement.FileName + compareToElement.Extension);
                        // You can now perform further checks on the streams if needed
                        // Generate the 64-bit fingerprint fingerprints for both images
                        ulong hash1 = _hasher.Hash(stream1);
                        ulong hash2 = _hasher.Hash(stream2);

                        double similarityScore = CompareHash.Similarity(hash1, hash2);
                        if (similarityScore >= 95.0)
                        {
                            sizeMatches = true;
                        }

                        HideCurrentComparison();
                    }
                } else {
                    LogMismatch(searchForElement, compareToElement, "Extension Not supported by PerceptualHash");
                    continue;
                }
            }
            if (extensionMatches && sizeMatches)
            {
                // Continue checking the remaining properties; mismatches are logged but do not change isMatched.
                /*
                if (compareToElement.DateCreated != searchForElement.DateCreated)
                {
                    //LogMismatch(searchForElement, compareToElement, "Date Created");
                }

                if (compareToElement.DateModified != searchForElement.DateModified)
                {
                    //LogMismatch(searchForElement, compareToElement, "Date Modified");
                }
                */
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

    private static void ShowCurrentComparison(ListElement fromElement, ListElement toElement)
    {
        // Resolve MainWindow inside the dispatched call; Application/MainWindow are thread-affine.
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var window = System.Windows.Application.Current?.MainWindow as MainWindow;
            if (window is null)
            {
                return;
            }

            window.CompareFromCurrentText.Text = $"Compare From: {fromElement.FileName}{fromElement.Extension} | Path={fromElement.Path}";
            window.CompareToCurrentText.Text = $"Compare To: {toElement.FileName}{toElement.Extension} | Path={toElement.Path}";
            window.CurrentComparisonPanel.Visibility = Visibility.Visible;
        });
    }

    private static void HideCurrentComparison()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var window = System.Windows.Application.Current?.MainWindow as MainWindow;
            if (window is not null)
            {
                window.CurrentComparisonPanel.Visibility = Visibility.Collapsed;
            }
        });
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

    private static void Log(string message)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var window = System.Windows.Application.Current?.MainWindow as MainWindow;
            window?.AppendLog(message);
        });
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() => LogTextBox.AppendText(message + Environment.NewLine));
    }
}
