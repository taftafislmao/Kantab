using Avalonia.Controls;
using Avalonia.Input;
using KanTab.ViewModels;

namespace KanTab.Views;

public partial class AuthenticationWindow : Window
{
    public AuthenticationWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    public AuthenticationWindow(AuthenticationWindowViewModel vm) : this()
    {
        DataContext = vm;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AuthenticationWindowViewModel.ErrorMessage))
            {
                // keep window centered when error text changes
            }
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not AuthenticationWindowViewModel vm) return;
        if (e.Key == Key.Escape && !vm.IsWelcome)
        {
            vm.BackToWelcomeCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (vm.IsLogin) vm.LoginCommand.Execute(null);
            else if (vm.IsSignUp) vm.SignUpCommand.Execute(null);
        }
    }
}
