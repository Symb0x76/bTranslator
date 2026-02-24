using bTranslator.App.ViewModels;
using System.Globalization;
using Windows.ApplicationModel.DataTransfer;

namespace bTranslator.App.Views;

public sealed partial class DictionaryEditorDialog : ContentDialog
{
    private readonly MainViewModel _mainViewModel;

    public DictionaryEditorDialog(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        ViewModel = new DictionaryEditorDialogViewModel(
            mainViewModel.Ui,
            mainViewModel.GetDictionaryEntriesSnapshot());

        InitializeComponent();
        DataContext = ViewModel;

        Title = ViewModel.Ui.DictionaryEditorDialogTitle;
        PrimaryButtonText = ViewModel.Ui.DictionaryEditorApplyButtonContent;
        CloseButtonText = ViewModel.Ui.DictionaryEditorCloseButtonContent;
    }

    public DictionaryEditorDialogViewModel ViewModel { get; }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            await _mainViewModel.ReplaceDictionaryEntriesAsync(ViewModel.ExportEntries()).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            args.Cancel = true;
            var template = _mainViewModel.GetLocalizedString(
                "Status.DictionaryEditorSaveFailed",
                "Save dictionary editor changes failed: {0}");
            _mainViewModel.StatusText = string.Format(CultureInfo.CurrentUICulture, template, ex.Message);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnAddEntryClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.AddEntry();
    }

    private void OnDuplicateEntryClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.DuplicateSelectedEntry();
    }

    private void OnDeleteEntryClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.DeleteSelectedEntry();
    }

    private async void OnPasteDelimitedClicked(object sender, RoutedEventArgs e)
    {
        var seedText = await TryReadClipboardTextAsync().ConfigureAwait(true);
        var pasted = await PromptPasteTextAsync(seedText).ConfigureAwait(true);
        if (pasted is null)
        {
            return;
        }

        var (parsedCount, addedCount) = ViewModel.AddEntriesFromDelimitedText(pasted);
        if (parsedCount == 0)
        {
            _mainViewModel.StatusText = _mainViewModel.GetLocalizedString(
                "Status.DictionaryPasteNoEntries",
                "No valid dictionary entries found in pasted text.");
            return;
        }

        var template = _mainViewModel.GetLocalizedString(
            "Status.DictionaryPasteImported",
            "Parsed {0} entries from pasted text, added {1} after dedupe.");
        _mainViewModel.StatusText = string.Format(
            CultureInfo.CurrentUICulture,
            template,
            parsedCount,
            addedCount);
    }

    private static async Task<string?> TryReadClipboardTextAsync()
    {
        try
        {
            var dataPackage = Clipboard.GetContent();
            if (dataPackage.Contains(StandardDataFormats.Text))
            {
                return await dataPackage.GetTextAsync();
            }
        }
        catch
        {
            // Ignore clipboard failures and fallback to empty textbox.
        }

        return null;
    }

    private async Task<string?> PromptPasteTextAsync(string? seedText)
    {
        var textbox = new TextBox
        {
            AcceptsReturn = true,
            MinWidth = 860,
            MinHeight = 320,
            PlaceholderText = ViewModel.Ui.DictionaryEditorPastePlaceholderText,
            Text = seedText ?? string.Empty,
            TextWrapping = TextWrapping.Wrap
        };
        ScrollViewer.SetVerticalScrollBarVisibility(textbox, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(textbox, ScrollBarVisibility.Auto);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = ViewModel.Ui.DictionaryEditorPasteDialogTitle,
            PrimaryButtonText = ViewModel.Ui.DictionaryEditorPasteApplyButtonContent,
            CloseButtonText = ViewModel.Ui.DictionaryEditorPasteCancelButtonContent,
            DefaultButton = ContentDialogButton.Primary,
            Content = textbox
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? textbox.Text : null;
    }
}
