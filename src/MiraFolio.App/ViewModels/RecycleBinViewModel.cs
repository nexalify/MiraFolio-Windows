using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiraFolio.App.Localization;
using MiraFolio.Core.Models;
using MiraFolio.Core.Services;

namespace MiraFolio.App.ViewModels;

public partial class RecycleBinViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearAllCommand))]
    private ObservableCollection<RemovedImageItemViewModel> _items = new();

    public RecycleBinViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        Refresh();
    }

    [RelayCommand]
    private void Restore(RemovedImageItemViewModel? item)
    {
        if (item == null)
            return;

        RemoveRecord(item.FilePath);
    }

    [RelayCommand]
    private void DeletePermanently(RemovedImageItemViewModel? item)
    {
        if (item == null)
            return;

        var fileMessage = item.Exists
            ? LocalizationService.Get("DeleteExistingPrompt")
            : LocalizationService.Get("DeleteMissingPrompt");
        var result = MessageBox.Show(
            $"{fileMessage}\n\n{item.FilePath}",
            LocalizationService.Get("ConfirmPermanentDeleteTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            if (File.Exists(item.FilePath))
                File.Delete(item.FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            MessageBox.Show(
                LocalizationService.Format("DeleteImageFailedFormat", ex.Message),
                LocalizationService.Get("DeleteFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        RemoveRecord(item.FilePath);
    }

    private bool CanClearAll() => Items.Count > 0;

    [RelayCommand(CanExecute = nameof(CanClearAll))]
    private void ClearAll()
    {
        var settings = _settingsService.Load();
        settings.RemovedImages ??= [];

        var paths = settings.RemovedImages
            .Select(record => record.FilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paths.Count == 0)
        {
            settings.RemovedImages.Clear();
            _settingsService.Save(settings);
            Refresh();
            return;
        }

        var existingFileCount = paths.Count(File.Exists);
        var result = MessageBox.Show(
            LocalizationService.Format("ClearConfirmLine1Format", paths.Count) + Environment.NewLine +
            LocalizationService.Format("ClearConfirmLine2Format", existingFileCount),
            LocalizationService.Get("ConfirmClearTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
            return;

        var failures = new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
            {
                failures[path] = ex;
            }
        }

        var targetedPaths = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var failedPaths = failures.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        settings = _settingsService.Load();
        settings.RemovedImages ??= [];
        settings.RemovedImages.RemoveAll(record =>
            string.IsNullOrWhiteSpace(record.FilePath) ||
            (targetedPaths.Contains(record.FilePath) && !failedPaths.Contains(record.FilePath)));
        _settingsService.Save(settings);
        Refresh();

        if (failures.Count == 0)
            return;

        var details = string.Join(
            "\n",
            failures.Take(3).Select(failure => $"• {failure.Key}\n  {failure.Value.Message}"));
        if (failures.Count > 3)
            details += Environment.NewLine + LocalizationService.Format("MoreDeleteFailuresFormat", failures.Count - 3);

        MessageBox.Show(
            LocalizationService.Format("ClearPartialFormat", paths.Count - failures.Count, failures.Count, details),
            LocalizationService.Get("PartialDeleteTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void RemoveRecord(string filePath)
    {
        var settings = _settingsService.Load();
        settings.RemovedImages ??= [];
        settings.RemovedImages.RemoveAll(record =>
            string.Equals(record.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        _settingsService.Save(settings);
        Refresh();
    }

    private void Refresh()
    {
        var records = _settingsService.Load().RemovedImages ?? [];
        Items = new ObservableCollection<RemovedImageItemViewModel>(
            records
                .Where(record => !string.IsNullOrWhiteSpace(record.FilePath))
                .GroupBy(record => record.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(record => record.RemovedAtUtc).First())
                .OrderByDescending(record => record.RemovedAtUtc)
                .Select(record => new RemovedImageItemViewModel(record)));
    }
}

public sealed class RemovedImageItemViewModel
{
    public string FilePath { get; }
    public string FileName { get; }
    public string RemovedAtText { get; }
    public bool Exists { get; }
    public string StatusText => Exists
        ? LocalizationService.Get("SourceFileExists")
        : LocalizationService.Get("SourceFileMissing");

    public RemovedImageItemViewModel(RemovedImageRecord record)
    {
        FilePath = record.FilePath;
        FileName = Path.GetFileName(record.FilePath);
        RemovedAtText = record.RemovedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        Exists = File.Exists(record.FilePath);
    }
}
