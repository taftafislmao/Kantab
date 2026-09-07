using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using KanTab.ViewModels;

namespace KanTab.Views;

public partial class MainWindow : Window
{
    private TextBox? _searchBox;
    private Border? _searchOverlay;
    private TextBox? _paletteBox;
    private Border? _paletteOverlay;

    public MainWindow()
    {
        InitializeComponent();
        Closing += (_, _) => (DataContext as MainWindowViewModel)?.FlushPendingSave();
        Opened += OnOpened;
        DataContextChanged += OnDataContextChanged;
        KeyDown += OnWindowKeyDown;
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        _searchBox = this.FindControl<TextBox>("GlobalSearchBox");
        _searchOverlay = this.FindControl<Border>("SearchOverlay");
        _paletteBox = this.FindControl<TextBox>("CommandPaletteBox");
        _paletteOverlay = this.FindControl<Border>("CommandPaletteOverlay");
        if (_searchBox != null) _searchBox.KeyDown += OnSearchBoxKeyDown;
        if (_searchOverlay != null) _searchOverlay.PointerPressed += OnSearchOverlayPointerPressed;
        if (_paletteBox != null) _paletteBox.KeyDown += OnPaletteBoxKeyDown;
        if (_paletteOverlay != null) _paletteOverlay.PointerPressed += OnPaletteOverlayPointerPressed;
        AttachViewModelEvents();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e) => AttachViewModelEvents();

    private void AttachViewModelEvents()
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.GlobalSearch.RequestFocus -= OnGlobalSearchRequestFocus;
            vm.GlobalSearch.RequestFocus += OnGlobalSearchRequestFocus;
            vm.CommandPalette.RequestFocus -= OnPaletteRequestFocus;
            vm.CommandPalette.RequestFocus += OnPaletteRequestFocus;
        }
    }

    private void OnGlobalSearchRequestFocus() => _searchBox?.Focus();
    private void OnPaletteRequestFocus()
    {
        _paletteBox?.Focus();
        _paletteBox?.SelectAll();
    }

    private static bool IsTextEditingFocus(IInputElement? focused)
    {
        // When a TextBox has focus, text editing keys must not be hijacked.
        // We only suppress global shortcuts that would insert characters (Ctrl+N etc),
        // not navigation/close keys. Palette/Search already handle their own keys.
        return focused is TextBox;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Palette has priority when open
        if (vm.CommandPalette.IsOpen)
        {
            // Let the palette handle Esc / Up / Down / Enter
            if (e.Key is Key.Escape or Key.Up or Key.Down or Key.Enter)
            {
                // If focus is inside palette box, let its KeyDown handle it first;
                // otherwise handle here.
                if (vm.CommandPalette.HandleKey(e.Key, e.KeyModifiers))
                {
                    e.Handled = true;
                    return;
                }
            }
            // Any other key while palette open: don't trigger global shortcuts
            // except Esc already handled.
            return;
        }

        // Global Search overlay open: Esc closes it (already handled below), others go to search box
        bool isTextEditing = IsTextEditingFocus(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement());

        // Ctrl+Shift+P -> Command Palette
        if (e.Key == Key.P && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            vm.CommandPalette.Open();
            e.Handled = true;
            return;
        }

        // Ctrl+K -> focus/open Global Search
        if (e.Key == Key.K && e.KeyModifiers == KeyModifiers.Control)
        {
            vm.GlobalSearch.FocusSearchCommand.Execute(null);
            _searchBox?.Focus();
            _searchBox?.SelectAll();
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+N -> New Note (check before Ctrl+N because Shift is superset)
        if (e.Key == Key.N && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && !isTextEditing)
        {
            foreach (var c in vm.CommandPalette.AllCommands) if (c.Id == "new-note") { c.Execute(); break; }
            e.Handled = true;
            return;
        }
        // Ctrl+N -> New Task (only when not typing in a TextBox)
        if (e.Key == Key.N && e.KeyModifiers == KeyModifiers.Control && !isTextEditing)
        {
            foreach (var c in vm.CommandPalette.AllCommands) if (c.Id == "new-task") { c.Execute(); break; }
            e.Handled = true;
            return;
        }

        // Esc -> close global search if open
        if (e.Key == Key.Escape)
        {
            if (vm.GlobalSearch.IsOpen)
            {
                vm.GlobalSearch.ClearCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (e.Key == Key.Down) { vm.GlobalSearch.MoveDownCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Up) { vm.GlobalSearch.MoveUpCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Enter) { vm.GlobalSearch.ExecuteSelectedCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Escape)
        {
            if (vm.GlobalSearch.HasQuery || vm.GlobalSearch.IsOpen)
            {
                vm.GlobalSearch.ClearCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnPaletteBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.CommandPalette.HandleKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            return;
        }
        // Typing itself is handled via binding; Enter/Esc/Up/Down handled above.
    }

    private void OnSearchOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.GlobalSearch.IsOpen && e.Source == _searchOverlay)
        {
            vm.GlobalSearch.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnPaletteOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.CommandPalette.IsOpen && e.Source == _paletteOverlay)
        {
            vm.CommandPalette.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }
}
