using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KanTab.ViewModels;

public partial class ConfirmDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _dialogResult;

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Confirm()
    {
        DialogResult = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? RequestClose;
}
