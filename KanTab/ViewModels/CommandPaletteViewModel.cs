using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KanTab.ViewModels;

/// <summary>
/// Single command entry shown in the palette.
/// </summary>
public sealed class PaletteCommand
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    /// <summary>Optional hint shown alongside the title.</summary>
    public string? Hint { get; init; }
    public required Action Execute { get; init; }
}

public partial class CommandPaletteViewModel : ObservableObject
{
    private readonly List<PaletteCommand> _all = new();

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private ObservableCollection<PaletteCommand> _filtered = new();
    [ObservableProperty] private int _selectedIndex = -1;
    [ObservableProperty] private bool _isOpen;

    public bool HasResults => Filtered.Count > 0;
    public bool IsEmpty => IsOpen && Filtered.Count == 0 && !string.IsNullOrWhiteSpace(Query);
    public string EmptyText => $"No commands matching \"{Query.Trim()}\"";
    public bool IsSelected(PaletteCommand cmd) => SelectedCommand == cmd;

    public event Action? RequestFocus;

    public CommandPaletteViewModel() { }

    public CommandPaletteViewModel(IEnumerable<PaletteCommand> commands)
    {
        _all.AddRange(commands);
        Refresh();
    }

    public void SetCommands(IEnumerable<PaletteCommand> commands)
    {
        _all.Clear();
        _all.AddRange(commands);
        Refresh();
    }

    public IReadOnlyList<PaletteCommand> AllCommands => _all.AsReadOnly();
    public PaletteCommand? SelectedCommand => SelectedIndex >= 0 && SelectedIndex < Filtered.Count ? Filtered[SelectedIndex] : null;

    partial void OnQueryChanged(string value) => Refresh();
    partial void OnIsOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEmpty));
        if (value) RequestFocus?.Invoke();
    }

    private void Refresh()
    {
        var q = Query?.Trim() ?? string.Empty;
        IEnumerable<PaletteCommand> matches = string.IsNullOrWhiteSpace(q)
            ? _all
            : _all.Where(c => c.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                           || (c.Hint != null && c.Hint.Contains(q, StringComparison.OrdinalIgnoreCase))
                           || c.Id.Contains(q, StringComparison.OrdinalIgnoreCase));

        var list = matches.ToList();
        Filtered = new ObservableCollection<PaletteCommand>(list);
        SelectedIndex = list.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    public void MoveSelection(int delta)
    {
        if (Filtered.Count == 0) return;
        if (SelectedIndex < 0) { SelectedIndex = delta > 0 ? 0 : Filtered.Count - 1; return; }
        var next = SelectedIndex + delta;
        if (next < 0) next = Filtered.Count - 1;
        if (next >= Filtered.Count) next = 0;
        SelectedIndex = next;
    }

    [RelayCommand] private void MoveUp() => MoveSelection(-1);
    [RelayCommand] private void MoveDown() => MoveSelection(1);

    [RelayCommand]
    private void ExecuteSelected()
    {
        var cmd = SelectedCommand;
        if (cmd == null) return;
        Close();
        cmd.Execute();
    }

    public void Execute(PaletteCommand cmd)
    {
        Close();
        cmd.Execute();
    }

    [RelayCommand]
    private void ExecuteCommand(PaletteCommand? cmd)
    {
        if (cmd == null) return;
        Execute(cmd);
    }

    public void Open()
    {
        Query = string.Empty; // triggers Refresh -> all commands shown, first selected
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        Query = string.Empty;
        SelectedIndex = -1;
    }

    /// <summary>Helper for MainWindow key handling: only handle palette keys when appropriate.</summary>
    public bool HandleKey(Avalonia.Input.Key key, Avalonia.Input.KeyModifiers mods)
    {
        if (!IsOpen) return false;
        if (key == Avalonia.Input.Key.Escape) { CloseCommand.Execute(null); return true; }
        if (mods == Avalonia.Input.KeyModifiers.None)
        {
            if (key == Avalonia.Input.Key.Down) { MoveDownCommand.Execute(null); return true; }
            if (key == Avalonia.Input.Key.Up) { MoveUpCommand.Execute(null); return true; }
            if (key == Avalonia.Input.Key.Enter) { ExecuteSelectedCommand.Execute(null); return true; }
        }
        return false;
    }
}
