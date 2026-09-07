using Avalonia.Controls;
using KanTab.ViewModels;

namespace KanTab.Views;

public partial class TaskDetailsWindow : Window
{
    public TaskDetailsWindow()
    {
        InitializeComponent();
    }

    public TaskDetailsWindow(TaskDetailsViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose += (_, _) => Close();
        Closing += (_, _) => vm.Detach();
    }
}
