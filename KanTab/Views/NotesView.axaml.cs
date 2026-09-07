using Avalonia.Controls;
using Avalonia;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using KanTab.ViewModels;

namespace KanTab.Views;

public partial class NotesView : UserControl
{
    private NotesViewModel? _viewModel;
    private bool _isCompact;

    public NotesView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        AttachedToVisualTree += (_, _) => UpdateResponsiveLayout();
        AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;

        _viewModel = DataContext as NotesViewModel;
        if (_viewModel != null)
            _viewModel.PropertyChanged += ViewModelOnPropertyChanged;

        Dispatcher.UIThread.Post(UpdateResponsiveLayout);
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotesViewModel.SelectedNote))
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateResponsiveLayout();
                if (_viewModel?.SelectedNote != null)
                    NoteTitleBox.Focus();
            });
        }
        else if (e.PropertyName is nameof(NotesViewModel.IsCompactLayout) or nameof(NotesViewModel.IsEditorOpen))
        {
            Dispatcher.UIThread.Post(UpdateResponsiveLayout);
        }
    }

    private void UpdateResponsiveLayout()
    {
        if (_viewModel == null || WorkspaceGrid == null)
            return;

        var width = Bounds.Width;
        if (width <= 0)
            width = RootGrid.Bounds.Width;

        var compact = width > 0 && width < 720;
        if (compact == _isCompact && WorkspaceGrid.ColumnDefinitions.Count > 0)
            return;

        _isCompact = compact;
        _viewModel.IsCompactLayout = compact;
        WorkspaceGrid.ColumnDefinitions.Clear();

        if (compact)
        {
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(NotesListPane, 0);
            Grid.SetColumn(NotesEditorPane, 0);
        }
        else
        {
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(260, GridUnitType.Pixel)));
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(NotesListPane, 0);
            Grid.SetColumn(NotesEditorPane, 1);
        }
    }

    private void NoteItem_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not NoteListItemViewModel item || _viewModel == null)
            return;

        _viewModel.SelectNoteCommand.Execute(item.Note);
    }
}
