using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KanTab.ViewModels;

public partial class ColumnDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _columnName = string.Empty;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _dialogTitle = "New Column";

    [ObservableProperty]
    private bool _dialogResult;

    public bool CanSave => !string.IsNullOrWhiteSpace(ColumnName);

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
            ValidationMessage = "Column name is required.";
            return;
        }

        ValidationMessage = null;
        DialogResult = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? RequestClose;
}
