using Avalonia.Controls;
using KanTab.ViewModels;

namespace KanTab.Views;

public partial class ColumnDialog : Window
{
    public ColumnDialog()
    {
        InitializeComponent();
    }

    public ColumnDialog(ColumnDialogViewModel vm, string title) : this()
    {
        Title = title;
        DataContext = vm;
        vm.RequestClose += (_, _) => Close();
    }
}
