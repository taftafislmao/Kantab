using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KanTab.Views;

namespace KanTab.Services;

public sealed class AvaloniaFileDialogService : IFileDialogService
{
    private readonly Window _owner;

    public AvaloniaFileDialogService(Window owner) => _owner = owner;

    public async Task<string?> ShowSaveDialogAsync(string suggestedFileName, string title = "Export Backup")
    {
        var sp = _owner.StorageProvider;
        if (sp == null) return null;
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "json",
            FileTypeChoices = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } }
        });
        return file?.Path.LocalPath;
    }

    public async Task<string?> ShowOpenDialogAsync(string title = "Import Backup")
    {
        var sp = _owner.StorageProvider;
        if (sp == null) return null;
        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } }
        });
        if (files.Count == 0) return null;
        return files[0].Path.LocalPath;
    }

    public Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(path);
    public Task WriteTextAsync(string path, string content) => File.WriteAllTextAsync(path, content);

    public async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var vm = new ViewModels.ConfirmDialogViewModel { Message = message };
        var dlg = new ConfirmDialog(vm, title);
        // ShowDialog requires a parent window
        await dlg.ShowDialog(_owner);
        return vm.DialogResult;
    }

    public async void ShowMessage(string title, string message)
    {
        var vm = new ViewModels.ConfirmDialogViewModel { Message = message };
        var dlg = new ConfirmDialog(vm, title);
        try { await dlg.ShowDialog(_owner); } catch { }
    }
}
