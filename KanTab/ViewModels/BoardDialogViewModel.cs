using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KanTab.ViewModels;

public partial class BoardDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _boardName = string.Empty;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _dialogTitle = "New Board";

    [ObservableProperty]
    private bool _dialogResult;

    public bool CanSave => !string.IsNullOrWhiteSpace(BoardName);

    public BoardDialogViewModel()
    {
    }

    public BoardDialogViewModel(string title, string name, bool isEditMode = false)
    {
        DialogTitle = title;
        BoardName = name;
        IsEditMode = isEditMode;
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Save()
    {
        if (!CanSave)
        {
            ValidationMessage = "Board name is required.";
            return;
        }

        ValidationMessage = null;
        DialogResult = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? RequestClose;
}
