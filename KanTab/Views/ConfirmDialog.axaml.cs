using Avalonia.Controls;
using KanTab.ViewModels;

namespace KanTab.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(ConfirmDialogViewModel vm, string title) : this()
    {
        Title = title;
        DataContext = vm;
        vm.RequestClose += (_, _) => Close();
    }
}
