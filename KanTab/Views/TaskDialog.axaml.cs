using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KanTab.ViewModels;

namespace KanTab.Views;

public partial class TaskDialog : Window
{
    public TaskDialog()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    public TaskDialog(TaskDialogViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose += (_, _) => Close();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not TaskDialogViewModel vm) return;
        // Escape -> cancel/close
        if (e.Key == Key.Escape)
        {
            vm.CancelCommand.Execute(null);
            e.Handled = true;
            return;
        }
        // Ctrl+Enter -> Create/Save (respect validation)
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
        {
            vm.CreateCommand.Execute(null);
            e.Handled = true;
        }
    }
}
