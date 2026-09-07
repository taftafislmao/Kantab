using System;
using Avalonia.Controls;
using KanTab.ViewModels;

namespace KanTab.Views;

public partial class BoardDialog : Window
{
    public BoardDialog()
    {
        InitializeComponent();
    }

    public BoardDialog(BoardDialogViewModel viewModel, string title)
    {
        InitializeComponent();
        DataContext = viewModel;
        Title = title;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 400;
        Height = 220;
        CanResize = false;
    }

    partial void InitializeComponent();
}
