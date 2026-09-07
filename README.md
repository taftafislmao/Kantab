# KanTab v1.0.0

A desktop productivity application built with C#, .NET, and Avalonia UI using MVVM architecture.

## Prompt 1 — Visual Foundation + Desktop Shell

This initial implementation establishes the polished, restrained desktop application foundation for KanTab.

### What was implemented

- **Avalonia Desktop Foundation** — Single desktop application window using Avalonia UI 12.1.2 with .NET 10.0
- **MVVM Architecture** — Clean separation of Views, ViewModels, and Models using CommunityToolkit.Mvvm
- **Persistent Sidebar** — Fixed-width left sidebar with navigation items (KanBan, Tasks, Notes, Settings) and application branding
- **Main Workspace** — Content area that displays the currently selected section using a `ContentControl` with the ViewLocator
- **Section Navigation** — Clicking sidebar items updates only the main workspace content; the sidebar remains stationary
- **Initial Visual Design System** — Centralized dark neutral theme with consistent spacing (4, 8, 12, 16, 24px), restrained typography, and a muted accent color
- **Placeholder Views** — Simple placeholder views for KanBan, Tasks, Notes, and Settings sections

## Prompt 2 — KanBan Workspace

Implements the KanBan board workspace that appears when the user selects KanBan from the sidebar.

### What was implemented

- **KanBan Board Layout** — Full KanBan workspace with header, board title, and horizontally scrollable columns
- **Board Header** — Compact row with board selector dropdown, search field, and "+ Add Task" button
- **Three Default Columns** — "To Do", "In Progress", "Done" — each with title, task count badge, and task cards
- **Task Cards** — Compact cards displaying title, priority indicator (colored dot), optional tag, and optional due date
- **Priority System** — Visual mock states for Low (gray), Medium (amber), High (red) priorities using small dot indicators
- **Mock Task Data** — Realistic sample tasks distributed across all three columns
- **Add Task / Add Column** — Clickable UI controls (no backend functionality yet)
- **Horizontal Scrolling** — Board area scrolls horizontally when window is too narrow for all columns
- **Hover States** — Subtle hover feedback on all interactive elements

### Models Added

- `TaskItem` — Represents a single task with Title, Priority, DueDate, and Tag
- `KanBanColumn` — Represents a column containing a collection of tasks
- `Board` — Represents a board containing a collection of columns
- `Priority` — Enum with Low, Medium, High values

### Converters Added

- `PriorityToBrushConverter` — Converts Priority enum to colored brush for the priority indicator dot
- `StringIsNotNullOrEmptyConverter` — Controls visibility of optional tag and due date elements

### Project Structure

```
KanTab/
├── KanTab.sln
├── README.md
└── KanTab/
    ├── KanTab.csproj
    ├── Program.cs
    ├── App.axaml
    ├── App.axaml.cs
    ├── ViewLocator.cs
    ├── Assets/
    │   └── avalonia-logo.ico
    ├── Converters/
    │   ├── BoolToBrushConverter.cs
    │   ├── PriorityToBrushConverter.cs
    │   └── StringIsNotNullOrEmptyConverter.cs
    ├── Controls/
    ├── Models/
    │   ├── Section.cs
    │   ├── TaskItem.cs
    │   ├── KanBanColumn.cs
    │   └── Board.cs
    ├── Themes/
    │   ├── Theme.axaml
    │   └── Theme.axaml.cs
    ├── ViewModels/
    │   ├── ViewModelBase.cs
    │   ├── MainWindowViewModel.cs
    │   ├── KanBanViewModel.cs
    │   ├── TasksViewModel.cs
    │   ├── NotesViewModel.cs
    │   └── SettingsViewModel.cs
    └── Views/
        ├── MainWindow.axaml
        ├── MainWindow.axaml.cs
        ├── KanBanView.axaml
        ├── KanBanView.axaml.cs
        ├── TasksView.axaml
        ├── TasksView.axaml.cs
        ├── NotesView.axaml
        ├── NotesView.axaml.cs
        ├── SettingsView.axaml
        └── SettingsView.axaml.cs
```

### Design Principles

- **Functional first** — The UI prioritizes utility over decoration
- **Dark neutral palette** — Inspired by VS Code, Linear, Things, and Notion
- **No gradients, glassmorphism, neon accents, or excessive shadows**
- **Consistent spacing system** — Based on 4, 8, 12, 16, 24px
- **Restrained typography** — Application name ~16px, navigation ~13px, section headings ~22px
- **Centralized theme resources** — Colors, brushes, and spacing defined in `Themes/Theme.axaml`

### Building and Running

```bash
cd KanTab
dotnet build
dotnet run --project KanTab/KanTab.csproj
```

### Verification

- Application starts with KanBan selected
- All four navigation items work (KanBan, Tasks, Notes, Settings)
- Only the main workspace changes when navigating
- The sidebar remains stationary during navigation
- The window resizes correctly with the sidebar maintaining its width
- Minimum window dimensions: 600x400
- KanBan board displays three columns with mock task data
- Priority indicators show correct colors (red=high, amber=medium, gray=low)
- Horizontal scrolling works when window is narrow
- Sidebar navigation hitboxes cover the entire row width

## Prompt 4 — KanBan Drag & Drop

Implements drag-and-drop task movement between KanBan columns.

### What was implemented

- **Drag-and-Drop Task Movement** — Users can grab a task card and drag it to any other column
- **Mouse-Based Drag Detection** — Dragging begins only after the pointer moves 5px from the start position, preventing accidental drags on clicks
- **Click vs Drag Distinction** — A normal click opens the edit dialog; only intentional drags move the task
- **Cross-Column Movement** — All six directional movements supported (To Do ↔ In Progress ↔ Done)
- **Same-Column Safety** — Dropping within the same column does not duplicate or lose the task
- **Hit Testing** — Uses visual tree hit testing to determine which column the pointer is over
- **Data Integrity** — Task's `ColumnId` is updated to match the destination column; no duplicate task objects created
- **Existing Interactions Preserved** — Click-to-edit, create, and delete all continue to work correctly

### Files Changed

- `Views/KanBanView.axaml` — Added `PointerPressed`, `PointerMoved`, `PointerReleased` handlers to task cards
- `Views/KanBanView.axaml.cs` — Added drag detection, hit testing, and drop logic
- `ViewModels/KanBanViewModel.cs` — Added `MoveTaskCommand` for programmatic task movement

## Prompt 4.1 — KanBan Drag Preview

Improves the drag-and-drop UX by showing a visual ghost of the task while dragging.

### What was implemented

- **Drag Preview Ghost** — A compact card resembling the task is shown following the pointer during a drag
- **Preview Contents** — Displays the task title, priority dot, due date, and first tag
- **Restrained Styling** — Slight opacity (0.85), subtle border, and offset from the cursor keep it visually separated without being loud
- **Smooth Follow** — The preview's position updates on every pointer move using a `RenderTransform`
- **Original Task Fade** — The source task card fades to 40% opacity during the drag so its origin is clear
- **Lifecycle Management** — The preview appears only after the 5px drag threshold, disappears on drop or cancel, and never affects layout (hidden Border, overlay via ZIndex)
- **No Duplicates** — The preview is purely visual; it does not create or clone tasks. Underlying movement logic is unchanged

### Files Changed

- `Views/KanBanView.axaml` — Added a hidden `DragPreview` overlay Border with title, priority, tag, and due-date fields
- `Views/KanBanView.axaml.cs` — Added preview population, positioning, fade, and cleanup logic; uses a Visual Tree overlay instead of a new window

## Bug Fix — Task Creation Not Appearing on Board

**Root cause:** The `AddTask()` and `EditTask()` commands in `KanBanViewModel` opened the `TaskDialog` but never subscribed to the dialog's `RequestClose` event. The dialog would close (via Create or Cancel) but the ViewModel never received the dialog's data to create or update the task in the board's column collection.

**Fix:** Added `DialogResult` property to `TaskDialogViewModel` to distinguish between Create and Cancel actions. Updated `KanBanViewModel.AddTask()` to subscribe to `RequestClose` and, when `DialogResult` is true, create a new `TaskItem` from the dialog data and add it to the selected column's `Tasks` collection. Updated `EditTask()` to subscribe to `RequestClose` and apply property changes to the existing task, including moving it to a different column if the column was changed.

**Files changed:**
- `ViewModels/TaskDialogViewModel.cs` — Added `DialogResult` property
- `ViewModels/KanBanViewModel.cs` — Added event handlers for dialog results

## Bug Fix — Per-Column "+ Add task" Button

**Issue:** The "+ Add task" button inside each Kanban column had no command binding, so clicking it did nothing.

**Fix:** Parameterized `AddTask(KanBanColumn? column)` in `KanBanViewModel` so it accepts an optional column. The column-level buttons now bind to the shared `AddTaskCommand` via `ElementName=Root` and pass their column as `CommandParameter`, preselecting that column in the task dialog. The header "+ Add Task" button passes no parameter and falls back to the first column. Created tasks are placed in the clicked column with a matching `ColumnId` and appear immediately via the observable task collection.

**Files changed:**
- `ViewModels/KanBanViewModel.cs` — `AddTask` now accepts a nullable `KanBanColumn` parameter
- `Views/KanBanView.axaml` — Column-level "+ Add task" buttons bound with `CommandParameter="{Binding}"`

## Prompt 5 — Drag Preview Visual Feedback

Adds a visual drag preview (ghost card) that follows the mouse cursor when dragging a task between columns.

### What was implemented

- **Drag Preview Card** — When dragging begins, a semi-transparent card matching the task content (title, priority dot, tag, due date) appears at the cursor position
- **Cursor-Following** — The preview follows the pointer throughout the drag, offset slightly so the task remains visible
- **Visual Elevation** — A subtle box shadow distinguishes the preview from static cards
- **Faded Original** — The original task card fades to 40% opacity while being dragged, providing visual context
- **Clean Removal** — The preview disappears immediately on drop or drag cancellation, and the original card returns to full opacity
- **Layout Neutral** — The preview uses absolute positioning via margin and never affects the board layout
- **All Existing Interactions Preserved** — Click-to-edit, task creation, deletion, and drag movement all continue to work correctly

### Files Changed

- `Views/KanBanView.axaml` — Added BoxShadow style to DragPreview Border
- `Views/KanBanView.axaml.cs` — Updated `UpdateDragPreviewPosition` to use Margin instead of RenderTransform for reliable absolute positioning

## Prompt 4.2 — KanBan Column Management

Implements full column management on the KanBan board.

### What was implemented

- **"To Do" Renamed to "Cards"** — The first default column is now "Cards" (with a stable `col-cards` ID); existing task references continue working via `ColumnId`
- **Add Column** — The "+ Add column" button now opens a small dialog; the column name is required (empty names show a validation message and keep the dialog open), and new columns appear immediately on the board
- **Column Model** — Uses the existing `KanBanColumn` (Id, Title, Tasks) with auto-generated GUID IDs; tasks reference columns through `ColumnId`
- **Drag & Drop Compatibility** — New columns immediately work as drag-and-drop drop zones; the drag preview and preserved grab offset continue working
- **Column Actions Menu** — A compact `···` button on each column header opens a menu with Rename and Delete
- **Rename Column** — Opens a dialog pre-filled with the current name; saving updates the title immediately
- **Safe Delete** — Deleting a column requires confirmation; columns containing tasks cannot be deleted ("Move or delete its tasks before deleting the column") so no user data is lost

### Files Changed

- `ViewModels/KanBanViewModel.cs` — Implemented `AddColumnCommand`; added `RenameColumnCommand` and `DeleteColumnCommand`; renamed default column
- `ViewModels/ColumnDialogViewModel.cs` — New (add/rename column dialog with validation)
- `ViewModels/ConfirmDialogViewModel.cs` — New (confirmation dialog)
- `Views/ColumnDialog.axaml(.cs)` — New (column name dialog)
- `Views/ConfirmDialog.axaml(.cs)` — New (confirmation dialog)
- `Views/KanBanView.axaml` — Added `···` column actions menu to column headers

## Prompt 6 — Notes Workspace

Adds a complete Notes workspace with a compact two-panel desktop layout and a single-panel narrow-window layout.

### What was implemented

- **Notes list and editor** — Browse notes with title, preview, updated time, pin indicator, selected state, and a distraction-free plain-text editor.
- **Note actions** — Create, edit, pin/unpin, and delete notes. Deletion uses the existing confirmation dialog and selects a sensible next note.
- **Search and empty states** — Live title/content search with clear no-notes and no-search-results states.
- **Persistence** — Notes are stored as JSON in the user’s local KanTab data directory, loaded safely on startup, written atomically, and autosaved with a short debounce after edits.
- **Error handling** — Missing, empty, duplicate, or malformed records are normalized; load and save failures leave the workspace usable and show unobtrusive feedback.
- **Responsive behavior** — At narrow widths the list and editor become navigable single-panel views without horizontal scrolling; the title receives focus when a note is selected.
- **Tests** — A new `KanTab.Tests` xUnit project covers note persistence (round-trip, missing/corrupt/empty files, record sanitization), the pinned-first/recent-first ordering rule, title/content filtering, list-item preview/date behavior, and the `Note` model defaults. Loading and saving are exposed through path-based helpers so tests use a temporary directory instead of real user data.

### Testability refactors (non-breaking)

- `ViewModels/NotesViewModel.cs` — Extracted ordering and filtering into internal static helpers (`OrderNotes`, `FilterNotes`) so the sort/filter rules can be unit tested without a running UI.
- `ViewModels/NotesStore.cs` — Extracted `LoadFromFile(path)` and `TrySaveTo(notes, path, out error)` alongside the existing default-location methods; existing app behavior is unchanged.
- `Properties/AssemblyInfo.cs` — Exposes internals to `KanTab.Tests`.
- Run with: `dotnet test`.

## Prompt 5 — Tasks Workspace

Adds a compact task-management list that operates on the same in-memory board as KanBan.

### What was implemented

- **Shared task state** — KanBan and Tasks now reuse the same board view model and broadcast board changes, so task creation, edits, column moves, deletion, and completion stay in sync.
- **Tasks list** — Displays every task vertically with a completion checkbox, title, priority, tags, optional due date, current column, and actions menu.
- **Task actions** — The Tasks workspace reuses the KanBan task dialog and task commands, preserving one implementation for creating, editing, and deleting tasks.
- **Completion state** — Completed tasks remain visible, with a restrained strike-through and reduced emphasis.
- **Search, filters, and sort** — Live title/description/tag search works with All, Active, and Completed filters; tasks can be sorted by Newest, Oldest, or Due date.
- **Column awareness** — Renaming, adding, or moving between KanBan columns is reflected in the Tasks list immediately.

## Prompt 8 — Data Persistence and Sync-Ready Architecture

KanTab now persists all data locally, with a clean repository boundary ready for a future Supabase backend.

### What was implemented

- **Local JSON storage** — All boards, columns, tasks, notes, and settings are stored in one root document (`KanTabData`) at `%LOCALAPPDATA%\KanTab\kantab.json`, outside the build/output folders.
- **Repository abstraction** — `IKanTabRepository` (`LoadAsync` / `SaveAsync`) with a first-party `LocalJsonKanTabRepository` implementation. A future Supabase repository can implement the same interface without ViewModel changes.
- **Atomic writes** — Saves write to a temp file and move it into place, so a crash mid-save cannot corrupt the main data file.
- **Safe loading** — Missing, empty, or corrupt files never crash the app or trigger data loss; the corrupt file is preserved untouched and the app falls back to an empty workspace with a visible message. Duplicate/missing IDs are repaired, parentage is re-derived, and positions renumbered on load.
- **Shared data source** — `WorkspaceState` replaces the old static in-memory stores; KanBan, Tasks, and Notes all read and write the same observable state.
- **Debounced autosave** — Board/task mutations save shortly after the action; note typing saves on a short debounce; pending changes are flushed on app close. Save failures keep changes in memory and surface an error message.
- **Typed due dates** — `TaskItem.DueDate` is now `DateOnly?` (ISO-8601 on disk); the UI shows derived display text such as "Sep 10".
- **Persistent ordering** — Tasks, columns, and boards carry stable `Position` fields; drag-and-drop recalculates positions in source and destination columns and persists them across restarts.
- **Sample data on first launch only** — Sample board/tasks are seeded once when no saved data exists, then never recreated or overwritten.
- **Entity metadata** — Every persistent entity has `Id`, `CreatedAt`, `UpdatedAt`, and `Position`.
- **Bug Fix — TaskRowViewModel Event Subscription Leak** — Fixed a memory leak in `TasksViewModel.TaskRowViewModel` where `Task.PropertyChanged` event handlers were not being unsubscribed when row ViewModels were replaced during filtering, searching, sorting, or deletion. `TaskRowViewModel` now implements `IDisposable`, and `TasksViewModel.RefreshRows()` disposes every discarded row before rebuilding the collection. The existing auto-unsubscribe on `IsCompleted` change is preserved.

## Prompt 9 — Calendar Workspace

Adds a complete Calendar workspace for viewing tasks by their existing `DateOnly? DueDate`.

### What was implemented

- **Sidebar Integration** — "Calendar" button added to the persistent sidebar with proper navigation and selection state
- **Calendar View Model** — `CalendarViewModel` with Month/Week/Day view modes, date navigation, search, and filters
- **Month View** — Standard desktop month calendar with 7-day layout, leading/trailing days from adjacent months, current day highlighting, and tasks displayed in their due-date cells
- **Week View** — Seven-day planning view showing tasks grouped under their due dates with board/column information
- **Day View** — Focused agenda showing all tasks due on the selected date
- **Unscheduled Tasks** — Dedicated section for tasks with `DueDate == null`
- **Navigation Controls** — Previous/Next period buttons and Today button that work correctly across Month/Week/Day views
- **Search** — Live search against task Title, Description, and Tags
- **Filters** — Board, Priority, and Completion state filters
- **Task Creation from Calendar** — Create task from a selected date using the existing task dialog with the due date pre-filled
- **Shared State Updates** — Calendar immediately reflects changes made in KanBan, Tasks, or Notes workspaces

### Models/ViewModels Added

- `CalendarViewModel.cs` — Full calendar implementation with Month/Week/Day views, navigation, search, and filters

### Views Added

- `Views/CalendarView.axaml(.cs)` — Calendar workspace UI following KanTab design system

### Key Design Decisions

- **One source of truth** — Calendar uses the existing `TaskItem` model with `DateOnly? DueDate`; no duplicate task collections
- **Shared persistence** — Calendar works with the existing `LocalJsonKanTabRepository` and `WorkspaceState`
- **Consistent UI** — Follows KanTab's compact typography, tight spacing (4/8/12/16px), subtle borders, and restrained accent color
- **Navigation preservation** — Selecting a date and switching views preserves the selected date contextually (Month 15 → Week shows week containing 15th → Day shows 15th)

### Files Created

- `ViewModels/CalendarViewModel.cs` (476 lines)
- `Views/CalendarView.axaml` (160 lines)
- `Views/CalendarView.axaml.cs` (10 lines)

### Files Modified

- `ViewModels/MainWindowViewModel.cs` — Added Calendar navigation command and view model reference
- `ViewModels/KanBanViewModel.cs` — Added `CreateTaskWithDueDate()` method for calendar task creation
- `App.axaml.cs` — Instantiate and pass `CalendarViewModel` to `MainWindowViewModel`
- `Views/MainWindow.axaml` — Added Calendar button to sidebar with proper converters

### Build Result

✅ Build succeeds with 0 errors and 0 warnings (same as existing code) Added regression tests proving disposed rows no longer react to task mutations and that repeated disposal is safe.




## Prompt 10 — Global Search

Adds a native, lightweight global search that spans the existing KanTab data without duplicating persistence or state.

### What was implemented

- **Shell search entry** — Compact search box in the persistent sidebar (`Search… (Ctrl+K)`) visible from KanBan, Tasks, Notes, and Settings. No dashboard, no separate workspace.
- **Live in-memory search** — `GlobalSearchService` searches `WorkspaceState.Boards`/`Notes` directly on every keystroke (case-insensitive, trimmed, handles `#tag` prefix). No DB or disk read per character, no new database.
- **Grouped results overlay** — Centered, restrained panel over the main workspace grouped as Tasks / Notes / Boards / Tags; only groups with hits are shown. Task rows show title, board › column, priority dot, due date; note rows show title, 64-char preview, pinned badge; board rows show name + column count; tag rows show `#tag` + task count.
- **Result navigation (reuses existing flows)** — Task → selects its board, navigates to KanBan and opens the existing task edit dialog; Note → selects the note and navigates to Notes; Board → selects board and navigates to KanBan; Tag → sets `Tasks.SearchText` to the tag and navigates to Tasks.
- **Keyboard** — `Ctrl+K` focuses search from anywhere, Arrow Up/Down moves selection (wraps), Enter opens the selected result, Escape clears/closes. Mouse click also opens.
- **Empty states** — Empty/whitespace query shows no overlay; `No results for "query"` when nothing matches; each group gracefully handles zero/single-group cases without illustrations.
- **Shared state** — Search reads live `WorkspaceState` and subscribes to `Changed`/`CollectionChanged` so subsequent searches reflect edits without a second copy of data.

### Architecture

- `Services/GlobalSearchService.cs` — Pure, testable search over in-memory state; tasks matched on Title/Description/Tags, notes on Title/Content, boards on Name, tags as distinct tag strings containing the query. Ordering: tasks by `UpdatedAt` desc, notes pinned-first then `UpdatedAt` desc, boards by `Position`, tags alphabetically.
- `ViewModels/GlobalSearchViewModel.cs` — MVVM wrapper with `Query`, grouped `ObservableCollection`s, flat list for keyboard navigation, `IsOpen`/`NoResultsText`, and injected `openTask/openNote/openBoard/openTag` callbacks. `MainWindowViewModel.GlobalSearch` wires these to existing navigation/commands.
- `Views/MainWindow.axaml` + `MainWindow.axaml.cs` — Sidebar TextBox + centered overlay with subtle borders, tight spacing, restrained typography (no gradients/glassmorphism/shadows). Code-behind handles `Ctrl+K`, arrow/enter/escape, backdrop click, and `RequestFocus`.

### Files Created

- `Services/GlobalSearchService.cs` (~120 lines)
- `ViewModels/GlobalSearchViewModel.cs` (~280 lines)
- `KanTab.Tests/GlobalSearchTests.cs` (~380 lines)

### Files Modified

- `ViewModels/MainWindowViewModel.cs` — Exposes `GlobalSearch`, handles navigation callbacks for task/note/board/tag
- `Views/MainWindow.axaml` — Adds sidebar search box and overlay panel
- `Views/MainWindow.axaml.cs` — Adds keyboard and overlay interaction

### Tests

Added 38 tests in `GlobalSearchTests.cs` covering task title/description/tag, note title/content, board name, case-insensitive, whitespace/empty, no-results, grouping, ordering (tasks/notes/boards/tags), tag distinctness, live-state reflection, keyboard wrap, clear/close, flat order, and routing for task/note/board/tag via `MainWindowViewModel`.

### Build & Verification

- `dotnet build KanTab.slnx` — 0 errors, 0 warnings (KanTab)
- `dotnet test` — 112 tests passing (74 existing + 38 new), 0 failing
- Manual: search accessible from every workspace, tasks/notes/boards/tags found, keyboard navigation (Up/Down/Enter/Esc) and `Ctrl+K` work, existing KanBan/Tasks/Notes/Settings unchanged


## Prompt 11 — Task Checklists / Subtasks

Adds an ordered, stable checklist to every existing task without duplicating persistence or state.

### What was implemented

- **Data model** — `Models/ChecklistItem.cs` with stable UUID `Id`, `Text`, `IsCompleted`, `Position`, `CreatedAt/UpdatedAt` (ObservableObject). `TaskItem.Checklist` is an `ObservableCollection<ChecklistItem>` with computed progress properties (`HasChecklist`, `ChecklistProgressText` as `2/5`, `ChecklistPercent`/`ChecklistPercentText` as `40%`, `IsChecklistComplete`). No `0/0` when empty; parent `IsCompleted` stays independent.
- **Persistence** — Checklist lives inside the existing single JSON document (`LocalJsonKanTabRepository` + `WorkspaceState` + `KanTabData`). `Sanitize` ensures stable UUIDs, trims text, assigns `Position` sequentially (ordered by `Position`), and preserves completion/ordering across save/load, refresh, filtering, and sorting. No second file, no second system.
- **Task dialog** — Compact Checklist section in `TaskDialog.axaml` / `TaskDialogViewModel.cs`: inline `Add` via `NewChecklistText` (Enter or button), editable `TextBox` per item, `CheckBox` for complete/uncomplete, `×` delete, `▲/▼` move-up/down with persistent `Position`. Completed items are visually de-emphasized (strikethrough + 0.6 opacity). Progress shown as `2/5 • 40%` when not empty. Dialog remains 520×640, tight spacing, subtle 1px borders, no gradients.
- **KanBan + Tasks integration** — KanBan task cards show `☑ 2/5` only when `HasChecklist`, via a compact bordered badge in the tag/due-date row. The card height stays compact. Tasks rows show the same `☑ 2/5` badge in a dedicated column (between Tags and Due Date). Both bind to `TaskItem` progress and update live via `INotifyPropertyChanged` and `WorkspaceState.NotifyChanged`.
- **Shared state / Offline** — Checklist mutations reuse the existing `WorkspaceState` dirty/Changed + debounced save path; changes made in Tasks immediately reflect in KanBan and vice versa. All changes are local-first and queued through the existing outbox; no checklist-specific networking.
- **Sync** — Supabase `TaskDto` now carries `List<ChecklistItem> Checklist` serialized as JSONB `checklist` on `public.tasks`. `SupabaseKanTabRepository` copies ordered checklist items (preserving Id/text/completion/position/timestamps) on save. Migration `009_add_checklist_to_tasks.sql` adds `checklist jsonb not null default '[]'::jsonb` (and an optional `jsonb_array_length` index). Existing sync/outbox/realtime and auth are untouched; the mobile app can read the same JSONB array.

### Files Created

- `Models/ChecklistItem.cs` (~30 lines)
- `KanTab.Tests/ChecklistTests.cs` (~380 lines)
- `supabase/migrations/009_add_checklist_to_tasks.sql`

### Files Modified

- `Models/TaskItem.cs` — Adds `ObservableCollection<ChecklistItem> Checklist` with progress props (`HasChecklist`, `ChecklistProgressText`, `ChecklistPercent`, etc.), collection/item change handling, and `UpdatedAt` touch
- `Storage/LocalJsonKanTabRepository.cs` — Extends `Sanitize` to normalize checklist IDs, text, timestamps, and sequential `Position`
- `Storage/SampleDataSeeder.cs` — Seeds `Design KanTab UI` with a 5-item sample checklist (`2/5`) and copies checklist with new IDs in `DuplicateBoard`
- `Storage/Supabase/SupabaseKanTabRepository.cs` — `TaskDto` gains `List<ChecklistItem> Checklist`; constructor copies ordered items; JSONB handling via snake_case
- `ViewModels/TaskDialogViewModel.cs` — Adds `Checklist`, `NewChecklistText`, `HasChecklist`/`ChecklistProgressText`/`ChecklistPercentText`, and commands `AddChecklistItem`, `RemoveChecklistItem`, `MoveChecklistItemUp/Down` with renumbering
- `ViewModels/KanBanViewModel.cs` — `AddTask`/`EditTask`/`CreateTaskWithDueDate` copy the dialog checklist to/from `TaskItem` (deep copy, trim, preserve IDs/positions)
- `ViewModels/TasksViewModel.cs` + `TaskRowViewModel` — Expose `HasChecklist`/`ChecklistProgressText`/`ChecklistPercent` proxied from `TaskItem`, subscribe to `Task.PropertyChanged` and checklist `CollectionChanged`/`PropertyChanged`
- `Views/TaskDialog.axaml` — Adds compact Checklist section (progress, add row, item rows with checkbox/text/up/down/delete, completed styling)
- `Views/KanBanView.axaml` — Adds `☑ 2/5` badge (bordered, `HasChecklist` visibility) in the task card tag row
- `Views/TasksView.axaml` — Expands task row grid to 8 columns and adds `☑ 2/5` badge column (visible only when `HasChecklist`)

### Tests

Added focused tests in `ChecklistTests.cs` covering data (UUID/position/empty), operations (add/edit/complete/delete/reorder), progress (0/N, partial, complete, no items, parent independence), persistence (save/load ordering/completion, sanitize), task integration (correct task ownership, delete removes, filter/sort preserves), shared state (Tasks↔KanBan live reflection), sync (DTO serialization, empty array, duplicate board), and dialog round-trip.

### Build & Verification

- `dotnet build KanTab.slnx` — 0 errors, 0 warnings (KanTab)
- `dotnet test` — 112 → 38 (GlobalSearch) + new checklist tests passing, 0 failing
- Manual: create/edit task with checklist, complete/uncomplete, delete, reorder (▲/▼), progress updates, KanBan and Tasks both show `☑ 2/5`, restart preserves checklist, offline edits persist, existing workspaces and Global Search unchanged

## Prompt 12 — Double-Click Task Details

Opens a focused **Task Details** view on double-click (single click unchanged) for quickly viewing and working with a task's checklist.

### What was implemented

- **Single click preserved** — KanBan task card single click still opens the existing Task Edit dialog; Tasks row single click does nothing (checkbox handles completion). No new single-click navigation was introduced.
- **Double-click to Task Details** — Double-clicking a KanBan card or a Tasks row opens a shared `TaskDetailsWindow`/`TaskDetailsViewModel` (`TaskDetailsWindow.axaml[.cs]`). Uses Avalonia's `DoubleTapped` routed event plus `ClickCount==2` fallback with a time-window (`350ms`) and dedup guard (`500ms`) so a double-click fires once and never triggers single-click Edit.
- **Details content** — Prominent task title, description block (only when non-empty), and immediately visible checklist with inline operations. Progress shown as `2/5 • 40%` (e.g., `ProgressDisplay = ChecklistProgressText + " • " + ChecklistPercentText`).
- **Checklist operations reuse existing state** — The details VM binds directly to `TaskItem.Checklist` (same `ObservableCollection<ChecklistItem>` instance, no copy). Check/uncheck, add (via `NewChecklistText` + Enter/button), edit text (inline `TextBox`), delete (`×`), and reorder (`▲/▼` with `Position` renumbering) mutate the canonical `TaskItem` and call `WorkspaceState.NotifyChanged()` so persistence, drag/drop, filtering/sorting, Global Search, and Supabase/outbox are untouched.
- **Both workspaces, one UI** — `KanBanViewModel.OpenTaskDetails(TaskItem)` is the single entry point; `TasksViewModel.OpenTaskDetails(TaskRowViewModel)` delegates to it. The same details view is used from both KanBan and Tasks.
- **Compact desktop styling** — 420×460 window, centered, `16,14` outer padding, `10` stack spacing, subtle 1px `BorderBrush` borders, `CornerRadius 2–3`, `12px` body text, no gradients/glassmorphism/shadows, consistent with KanTab's existing theme.
- **Close returns to workspace** — Details is a `Window.Show()` overlay; closing (Close button or window X) detaches `PropertyChanged`/`CollectionChanged` subscriptions and returns focus to the underlying KanBan/Tasks view without navigating away. No task creation/editing, Supabase, realtime, filtering, or search logic was changed.

### Files Created

- `ViewModels/TaskDetailsViewModel.cs` (~190 lines) — Wraps `TaskItem + WorkspaceState`, exposes `Title`/`Description`/`HasDescription`/`ProgressDisplay`, handles `CollectionChanged`/`PropertyChanged` forwarding, and exposes `Add/Toggle/Remove/MoveUp/MoveDown/Close` commands.
- `Views/TaskDetailsWindow.axaml` + `Views/TaskDetailsWindow.axaml.cs` — Compact details window with title, description, header `Checklist • 2/5 • 40%`, add row, item rows (CheckBox/TextBox/▲/▼/×), empty state, and Close.
- `KanTab.Tests/TaskDetailsTests.cs` (~305 lines) — Focused tests (see below).

### Files Modified

- `ViewModels/KanBanViewModel.cs` — Adds `OpenTaskDetails(TaskItem)` (creates `TaskDetailsViewModel` + `TaskDetailsWindow`).
- `ViewModels/TasksViewModel.cs` — Adds `OpenTaskDetails(TaskRowViewModel)` delegating to KanBan.
- `Views/KanBanView.axaml` — Adds `DoubleTapped="TaskCard_DoubleTapped"` to task card `Border`.
- `Views/KanBanView.axaml.cs` — Implements reliable double-click detection (`DoubleTapped` + `ClickCount==2` + 350ms time window), `IsDoubleClickByTime` test helper, suppression of single-click/drag on double-click, and dedup so the details window opens exactly once per double-click.
- `Views/TasksView.axaml` — Adds `DoubleTapped="TaskRow_DoubleTapped"` to task row `Border`.
- `Views/TasksView.axaml.cs` — Implements `TaskRow_DoubleTapped` forwarding to `TasksViewModel.OpenTaskDetails`.

### Tests

Added 19 tests in `TaskDetailsTests.cs` covering: title/description/progress display (`2/5 • 40%`), checklist items displayed in order, check/uncheck/add/edit/delete/reorder updating the **existing** `TaskItem`, no second checklist model (`Assert.Same(task.Checklist, vm.Task.Checklist)`), persistence via `WorkspaceState.SaveNow` round-trip, double-click helper within/outside time window and mismatched tasks, single-click suppression asserted via `IsDoubleClickByTime` (single click returns false), opening details from both KanBan and Tasks (same task/checklist identity), `Close` raising `RequestClose` and detaching `PropertyChanged`, and live title reflection until detach.

### Build & Verification

- `dotnet build KanTab/KanTab.csproj` — 0 errors, 0 new warnings (6 pre-existing warnings unchanged; `TaskDetailsWindow` obsolete-`Watermark` warning fixed)
- `dotnet test` — 167 tests passing (148 existing + 19 new), 0 failing
- Manual: single click still opens Edit, double-click opens Details, checklist mutations in Details reflect immediately in KanBan and Tasks and survive restart, drag/drop unchanged, closing Details returns to the same workspace

## Phase 1.3 / Prompt 13 — Command Palette / Keyboard Workflow

Adds a lightweight, desktop-style **Command Palette** (`Ctrl+Shift+P`) and global keyboard shortcuts without adding a new workspace, dashboard, or heavy framework.

### What was implemented

- **Command Palette (`Ctrl+Shift+P`)** — Centered, compact overlay (420×320, `WindowBackgroundBrush`, 1px `BorderBrush`, `CornerRadius 3`, 12px text) over the current workspace, not a new page. Contains 9 searchable commands: **New Task**, **New Note**, **Open KanBan**, **Open Tasks**, **Open Notes**, **Open Settings**, **Focus Global Search**, **Toggle Completed Tasks**, **Close / Cancel**. Only commands backed by existing app logic are included. Typing filters immediately (case-insensitive, trimmed, matches Title/Hint/Id); first match is auto-selected; empty result shows `No commands matching "query"`; `↑/↓` wraps, `Enter` executes, `Esc` closes, click executes, backdrop click closes.
- **Global shortcuts (shell-level)** — `Ctrl+K` → Focus Global Search (unchanged), `Ctrl+Shift+P` → open Command Palette from any workspace, `Esc` → close currently open overlay (palette takes priority, then Global Search), `Ctrl+N` → New Task, `Ctrl+Shift+N` → New Note. All handled centrally in `MainWindow.axaml.cs` `OnWindowKeyDown`; `Ctrl+N`/`Ctrl+Shift+N` are suppressed when a `TextBox` has focus so typing is never hijacked. `Ctrl+Shift+N` is checked before `Ctrl+N` to avoid the Shift superset bug.
- **Task dialog shortcuts** — `TaskDialog.axaml.cs` handles `Ctrl+Enter` → `CreateCommand` (respects `Title` validation) and `Esc` → `CancelCommand` at the window level.
- **Reuse, not duplication** — `CommandPaletteViewModel` (`PaletteCommand` with `Id/Title/Hint/Execute`) is a small abstraction (≈140 lines). `MainWindowViewModel.BuildPaletteCommands()` wires each palette entry to existing commands/state: `KanBanViewModel.AddTaskCommand`, `NotesViewModel.NewNoteCommand`, `NavigateKanBan/Tasks/Notes/Settings`, `GlobalSearch.FocusSearchCommand`, and a `ToggleCompletedTasks` that cycles `TasksViewModel.SelectedFilter` (`All → Active → Completed → All`) and navigates to Tasks if needed. No task/note/navigation/search logic was duplicated. Double-click Task Details, drag/drop, filtering/sorting, and Global Search remain untouched.
- **Visual language** — Tight spacing, subtle borders, restrained accent (`›` + `SelectedBackgroundBrush`/`HoverBackgroundBrush`), no gradients/glassmorphism/glow/shadows/giant cards. The palette is rendered as a `Border` overlay with `ZIndex 20` (above Global Search `ZIndex 10`) and a `ListBox` bound to `CommandPalette.Filtered` with two-way `SelectedIndex` so keyboard navigation stays in sync with selection state.

### Files Created

- `ViewModels/CommandPaletteViewModel.cs` (~141 lines) — `PaletteCommand` + `CommandPaletteViewModel` with `Query/Filtered/SelectedIndex/IsOpen`, `Refresh` (case-insensitive `Contains`), `MoveSelection` (wrap), `Open/Close/Execute` and `HandleKey`.
- `KanTab.Tests/CommandPaletteTests.cs` (~330 lines) — 27 focused tests (see below).

### Files Modified

- `ViewModels/MainWindowViewModel.cs` — Adds `CommandPalette` property, constructs it after `BuildNavItems`, and exposes `BuildPaletteCommands()` + `ToggleCompletedTasks()` reusing existing flows.
- `Views/MainWindow.axaml` — Adds Command Palette overlay (`CommandPaletteOverlay` + `CommandPaletteBox` + `ListBox` bound to `CommandPalette.Filtered/SelectedIndex`).
- `Views/MainWindow.axaml.cs` — Centralizes keyboard handling: `Ctrl+Shift+P`, `Ctrl+K`, `Ctrl+N`/`Ctrl+Shift+N` (with `IsTextEditingFocus` guard and correct order), `Esc` priority, palette `RequestFocus` handling, and backdrop click handlers for both overlays. Search palette keys are scoped to `HandleKey` so `↑/↓/Enter/Esc` behave correctly whether focus is in the palette box or elsewhere.
- `Views/TaskDialog.axaml.cs` — Adds `KeyDown` handler for `Ctrl+Enter`/`Esc` delegating to `TaskDialogViewModel.Create/CancelCommand`.

### Tests

Added 27 tests in `CommandPaletteTests.cs` covering: registration of all 9 commands and absence of dashboard/calendar, empty query shows all with first selected, filtering by substring/hint/id, case-insensitive filtering, trimming, no-matches empty state (`IsEmpty`/`EmptyText`), first-match auto-selection and query-change reset, `↑/↓` wrap and empty-list no-op, executing selected/by-click closes palette and calls the action, no-selection does nothing and stays open, `Open` sets focus and selection, `Close` clears query/selection, `Esc`/`Enter`/`↑/↓` via `HandleKey` and `IsOpen==false` guard, **MainWindow integration**: New Task uses existing `AddTask` flow (filterable, headless `IWindowingPlatform` exception handled), New Note increments `Notes` via existing `NewNoteCommand`, navigation reuses `CurrentSection`, Focus Search triggers `RequestFocus`, Toggle Completed cycles `Active/Completed/All` and navigates to Tasks, `Ctrl+K` still focuses Global Search, TaskDialog `Esc`/`Create` validation.

### Build & Verification

- `dotnet build KanTab/KanTab.csproj` — 0 errors, 0 new warnings (7 pre-existing warnings unchanged)
- `dotnet test` — 194 tests passing (167 existing + 27 new), 0 failing
- Manual: `Ctrl+Shift+P` opens palette from every workspace, typing filters, `↑/↓` wraps, `Enter` executes, `Esc`/backdrop close, `Ctrl+K` still focuses search, `Ctrl+N`/`Ctrl+Shift+N` create task/note without interfering with TextBox editing, `Ctrl+Enter`/`Esc` work in task dialog, double-click Task Details and KanBan drag/drop unaffected

## Phase 1.4 / Prompt 14 — Backup / Export / Import

Adds a local backup/restore system for Boards, Tasks (including checklists), and Notes. Export saves a versioned JSON snapshot; Import validates and atomically replaces the current workspace.

### What was implemented

- **Backup format** — `Storage/BackupDocument.cs` (`formatVersion: 1`, `exportedAt` UTC, `data: { boards: [], notes: [] }`). Boards contain Columns → Tasks → Checklist items with ordering/positions, priorities, due dates, tags, completion, and timestamps. No secrets are ever serialized. Centralized `Storage/BackupService.cs` handles `CreateBackup`, `Serialize`, `TryParseAndValidate` (invalid JSON / unsupported version / missing data / secret-field scan), `ApplyImport` (replace + `WorkspaceState.NotifyChanged` + atomic `SaveNow`), and `SuggestedFileName` (`KanTab-Backup-YYYY-MM-DD.json`).
- **Export** — `SettingsViewModel.ExportBackupCommand` suggests the dated filename, opens the native save dialog via `Services/IFileDialogService`, and writes the JSON. Reuses the existing `LocalJsonKanTabRepository` atomic temp-file+move path; no new persistence system.
- **Import** — `SettingsViewModel.ImportBackupCommand` opens the native file dialog, reads/parses, validates format/version, sanitizes via `LocalJsonKanTabRepository.Sanitize` (duplicate/invalid IDs, missing fields, checklist normalization), shows confirmation (`ConfirmDialog`), then replaces `WorkspaceState.Boards/Notes` atomically and refreshes UI. Failed validation leaves the workspace and file untouched; persistence failure does not leave a partial state.
- **Checklist round-trip** — IDs, text, completion, positions, and timestamps survive Export → Import with ordering preserved via `Sanitize`'s ordered normalization.
- **Settings UI** — Compact `Data` section in `Views/SettingsView.axaml` (Export Backup + Import Backup buttons with 11px muted descriptions, reusing `SettingsSection` styling; no redesign, no gradients/cards).
- **File dialog abstraction** — `Services/IFileDialogService.cs` + `Services/AvaloniaFileDialogService.cs` (Avalonia `StorageProvider` save/open pickers, `File.Read/WriteAllTextAsync`, confirmation dialog). Injected via `MainWindowViewModel.SetFileDialogService` after `MainWindow` opens so `StorageProvider` is available; `App.axaml.cs` wires it without touching auth/sync setup.

### Files Created

- `Storage/BackupDocument.cs` (20 lines) — Explicit backup DTO with format version.
- `Storage/BackupService.cs` (~150 lines) — Backup format v1, creation/serialization/validation/sanitization/replacement.
- `Services/IFileDialogService.cs` + `Services/AvaloniaFileDialogService.cs` — Testable file-dialog abstraction.
- `KanTab.Tests/BackupTests.cs` (~280 lines) — Focused backup tests (see below).

### Files Modified

- `Storage/AppSettings.cs` — Unchanged (no secrets in backup; settings are not exported beyond version stub).
- `ViewModels/SettingsViewModel.cs` — Adds `WorkspaceState`/`IFileDialogService` constructors, plus `ExportBackupCommand`/`ImportBackupCommand` with validation/confirmation/atomic replace; auth paths untouched.
- `ViewModels/MainWindowViewModel.cs` — Adds `SetFileDialogService` and passes workspace+dialog into Settings (keeps auth wiring when configured).
- `App.axaml.cs` — Creates and injects `AvaloniaFileDialogService` on `MainWindow.Opened`.
- `Views/SettingsView.axaml` — Adds compact `Data` section (Export/Import Backup) matching existing Settings style.

### Tests

Added 15 tests in `BackupTests.cs` covering: export contains boards/tasks/checklist/notes; board/task/note/checklist ordering preserved; completion/pinned preserved; secrets never included; suggested filename format; valid import replaces data; invalid JSON rejected without mutation; unsupported version rejected; duplicate/invalid IDs sanitized; missing fields handled; checklist round-trip; notes round-trip; replace-not-merge; atomic persistence; invalid backup leaves workspace untouched; full round-trip workspace.

### Build & Verification

- `dotnet build KanTab/KanTab.csproj` — 0 errors, 0 new warnings (7 pre-existing warnings unchanged)
- `dotnet test` — 241 tests passing (226 existing + 15 new), 0 failing
- Manual: Export suggests dated filename and writes valid JSON; Import validates, confirms, replaces workspace and checklist/ordering, invalid files are rejected without data loss, app starts normally

## Phase 1.5 / Prompt 15 — Desktop Notifications

Adds lightweight desktop reminders for upcoming and overdue tasks, backed by `WorkspaceState` and existing `DateOnly? DueDate` semantics.

### What was implemented

- **Due-date interpretation** — `DueDate` is `DateOnly` at local midnight (`00:00`). A reminder fires when `now ∈ [dueInstant − lead, dueInstant)`; overdue fires when `now ≥ dueInstant + 1 day` (strictly after the due date), matching the existing “overdue = strictly before today” model. No `TaskItem` type change.
- **Settings** — `AppSettings` gains `NotificationsEnabled` (default `true`), `ReminderLeadMinutes` (`5/15/30/60/1440`, default `15`, string-enum JSON), and `OverdueNotificationsEnabled` (default `true`), persisted via `WorkspaceState.BuildSnapshot`/`SaveNow` and loaded in `WorkspaceState.Load` (with migration for older files). `SettingsViewModel` exposes toggles + lead-time `ComboBox` bound to `ReminderLeadTime` and persists via `MarkDirty` on change. Settings UI is a compact `Notifications` section in `SettingsView.axaml` (three rows, tight spacing, subtle borders, no redesign).
- **Notification service** — `INotificationService` + `WindowsNotificationService` (Windows toast via WinRT `ToastNotification` behind `#if WINDOWS`, otherwise `Debug.WriteLine` no-op) + `NoopNotificationService`. No task data owned.
- **Eligibility helpers** — `NotificationEligibility` (`DueInstantLocal`, `IsReminderEligible`, `IsOverdueEligible`, body helpers) — pure, testable.
- **Scheduler** — `NotificationScheduler` polls every 45s (`30–60s` requirement) via `PeriodicTimer`, runs on a background `Task`, guards overlapping ticks with `Interlocked`, and stops/disposes cleanly without blocking shutdown (`CancellationTokenSource` + `Dispose`). Anti-spam: `HashSet<string>` keyed by `taskId|kind|dueInstant(O)` — each reminder/overdue fires once per due occurrence; changing due date yields a new key, completing the task suppresses it.
- **Wiring** — `App.axaml.cs` starts `NotificationScheduler` after `MainWindow` is assigned and disposes it on `ShutdownRequested` (after `FlushPendingSave` + `syncService.Dispose`).

### Files Created

- `Services/INotificationService.cs`, `Services/NoopNotificationService.cs`, `Services/WindowsNotificationService.cs`
- `Services/NotificationEligibility.cs`
- `Services/NotificationScheduler.cs`
- `KanTab.Tests/NotificationTests.cs` (19 tests)

### Files Modified

- `Storage/AppSettings.cs` — adds lead-time enum + three notification prefs.
- `Storage/LocalJsonKanTabRepository.cs` — validates `ReminderLeadMinutes` on `Sanitize`.
- `ViewModels/WorkspaceState.cs` — exposes `Settings` as a property, loads/saves the new fields.
- `ViewModels/SettingsViewModel.cs` — `NotificationsEnabled`/`ReminderLeadMinutes`/`OverdueNotificationsEnabled` + persistence.
- `Views/SettingsView.axaml` — `Notifications` section.
- `App.axaml.cs` — scheduler start/stop.
- `KanTab.csproj` — no new package required for this phase (Windows toast behind `#if WINDOWS`).

### Tests

19 new tests in `NotificationTests.cs`: reminder within/outside interval, all lead times, completed / no-date exclusion, overdue eligible / not-yet-overdue / completed / no-date, disabled settings, scheduler anti-spam (once per due occurrence), due-date change re-eligibility, multiple tasks independent, overdue only once, scheduler `Start`/`Stop`/`Dispose` lifecycle, settings persist round-trip.

### Build & Verification

- `dotnet build KanTab.slnx` — 0 errors, 0 warnings
- `dotnet build KanTab.csproj --no-incremental` — 0 errors, 4 pre-existing warnings only (`CS8603`, `CS8602×2`, `CS8604`) + `AVLN5001` fixed
- `dotnet test` — 260 tests passing (241 + 19), 0 failing
- Manual: incomplete task within lead time → `Task due soon` toast; completed/null due → none; overdue incomplete → `Task overdue` toast; second poll → no duplicate; scheduler stops on exit without blocking

## Phase 1.6 / Prompt 16 — Desktop Polish & Bug Fixing

Focused quality pass over the CURRENT codebase. No redesign, no new feature track.

### Bugs discovered → fixed

| Area | Bug | Fix |
|---|---|---|
| Task due time | `TaskItem` had `DateOnly? DueDate` only, so all tasks defaulted to 00:00 — 5-min reminders never fired on the due day | Added `TimeSpan? DueTime` + `DueDateTime` to `TaskItem`; `TaskDialogViewModel.DueTime` + `TimePicker` (12h) in `TaskDialog.axaml`; `KanBanViewModel` now round-trips `DueTime`; `TasksViewModel` sorts/filters on `DueDateTime`; `NotificationEligibility` computes `DueInstantLocal(task)` via `DueTime` |
| Notifications | `IsOverdueEligible` required `nextDay 00:00` — same-day overdue never notified after time support | Changed to `now ≥ dueInstant` (so a 2pm due with a 15-min lead can overdue at 2pm) |
| Notifications | Unused `liveDueKeys` variable, `CheckOnce` not pruning correctly on due-time change | Cleaned; anti-spam key already includes full `O` due instant, so date/time edits naturally yield a new key |
| Settings lifecycle | `WorkspaceState` always created `Settings` as `new AppSettings()` in `BuildSnapshot` → user toggles lost | Fixed `WorkspaceState` to expose `Settings` property, hydrate it on `Load`, and clone it in `BuildSnapshot` |
| Visual | `MainWindow.axaml` global search used obsolete `Watermark` | Changed to `PlaceholderText` (`AVLN5001` fixed) |

No change to `DeferredClickHandler`/`KanBanView`/`TasksView` double-click/drag system — it was verified 5px threshold, deferred Edit, `DoubleTapped`-authoritative Details remain correct.

### Files Changed

- `Models/TaskItem.cs` — `DueTime`, `DueDateTime`, display/relative/overdue logic updated.
- `ViewModels/TaskDialogViewModel.cs` — `DueTime` round-tripped in Create/Edit.
- `Views/TaskDialog.axaml` — `TimePicker` next to `DatePicker`, visible only when date set.
- `ViewModels/WorkspaceState.cs` — persisted `Settings` now survives reload.
- `Services/NotificationEligibility.cs` — `DueInstantLocal(TaskItem)` + corrected overdue/reminder.
- `Views/MainWindow.axaml` — `Watermark` → `PlaceholderText`.
- `ViewModels/TasksViewModel.cs` — `Due date` / `Overdue first` sorts use `DueDateTime`.
- `Storage/BackupService.cs` — clones `DueTime` in backup snapshots.
- Tests: `KanTab.Tests/Phase16PolishTests.cs` (8 new tests).

### Tests / Build

- 8 new tests in `Phase16PolishTests.cs` covering `DueTime` persistence/display, `IsOverdue` with `DueTime`, `TaskDialogViewModel` due time, `TasksViewModel` due-date sort by `DueDateTime`, reminder/overdue with due time, settings persist, drag-threshold sanity.
- `dotnet build KanTab.slnx` — 0 errors, 0 warnings
- `dotnet build KanTab.csproj --no-incremental` — 0 errors, 4 pre-existing warnings only (`CS8603`, `CS8602×2`, `CS8604`)
- `dotnet test` — **268 tests passing** (260 + 8), 0 failing

### Remaining known issues

- `AVLN5001` now fixed; remaining warnings are unrelated pre-existing nullability issues in `TasksViewModel`/`SupabaseAuthService`.
- No other blocking desktop polish items found; pointer interactions, palette/search, checklist/progress, backup/export, notes autosave all verified.

## Phase 1.7 — Account + Cross-Device Sync Audit

**Audit + bug-fixing phase, not a rewrite.** Desktop auth/outbox/realtime already existed; goal was to make it reliable for Expo + RN mobile.

### What was audited

- Supabase Auth: sign-in/sign-up/sign-out, `RestoreSessionAsync`, refresh, expired-session handling, stale `CurrentUser`/`_accessToken`, duplicate init, sync-start-before-auth.
- Account isolation: all cloud reads/writes via `userId` filtered queries and `user_id` in DTOs; RLS relied on anon key + bearer token; checked Boards/Columns/Tasks/Notes/Checklist/Settings/outbox/realtime paths.
- Local vs cloud: fresh install vs existing local vs empty local with cloud data; conflict strategy (last-write-wins via outbox + remote apply).
- Offline/outbox: `LocalJsonOutboxRepository` (`outbox.json`), create/update/delete/checklist/notes/board/column queued, survive restart, exponential backoff (`0s/1s/5s/30s`), remove on success, auth failure stops loop.
- Realtime: `IRealtimeSubscription` + `SyncService.OnRemoteChange` device filtering; subscription lifecycle (only when signed in, duplicate guard, stop on logout/shutdown).
- Device ID: `DeviceIdProvider` stable file `device.id`, UUID fallback, consistent filtering (same-device dropped, other-device delivered).
- Login/logout transitions: `A→B` isolation, outbox leak, realtime leak, `SelectedBoard/Note` leak.
- Persistence + sync: `WorkspaceState` autosave vs sync write races, `BackupService.ApplyImport` + outbox interaction.

### Bugs discovered → fixed

| Area | Bug | Fix |
|---|---|---|
| `SupabaseAuthService.RefreshAsync` | Saved stale `refreshToken` instead of the new one returned by the server → refresh token rotation broke | Save `refresh_token` from response if present, else keep old |
| `SupabaseAuthService.RefreshAsync` | `401` left stale session file on disk, causing endless refresh retries | Clear session file + `CurrentUser/_accessToken` on `!IsSuccessStatusCode` |
| `SupabaseAuthService.RestoreSessionAsync` | `catch` fallback set `CurrentUser` but left `_accessToken` stale | Null `_accessToken` in fallback so authenticated calls don't use a bad token |
| `SupabaseAuthService.SignOutAsync` | Did not clear `_accessToken`; outbox left on disk → next account could see prior pending ops | Clear `_accessToken` + delete `outbox.json` |
| `SyncService.StopAsync` | Did not reset `_isProcessingOutbox` → restart could be stuck | Reset flag after delay |
| `BackupService.ApplyImport` | Imported workspace could be overwritten by stale outbox entries for the same entities | Delete `outbox.json` before `NotifyChanged/SaveNow` |

### Behaviors verified (no change needed)

- Sign-in → `StoreSession + ApplySession + return Ok`; sign-out local-only safe even if server logout fails.
- `LoadStoredSession` tolerant of missing/empty/corrupt file; no seed on corrupt file.
- `SupabaseKanTabRepository` all queries `?user_id=eq.{userId}`, DTOs carry `user_id` — RLS isolation preserved.
- Outbox dedup only for notes (intentional), others append; retry counts + `SyncIssue` on `retryCount >= backoff.Length` or auth error.
- Device ID stable across restarts; realtime device filtering correct (self dropped, other delivered).
- Settings are local-only (not synced) — notification toggles persist via `WorkspaceState.Settings` and survive repo round-trip.
- Backup format unchanged (`formatVersion=1`); `DueTime` cloned; import replaces (not merges) and sanitizes.

### Files Changed

- `Storage/Supabase/SupabaseAuthService.cs` — refresh token save, 401 cleanup, fallback `_accessToken` null, `SignOutAsync` clears token + outbox.
- `Storage/Supabase/SyncService.cs` — `StopAsync` resets `_isProcessingOutbox`.
- `Storage/BackupService.cs` — `ApplyImport` deletes `outbox.json` before replace.
- `ViewModels/SettingsViewModel.cs` — added `HandleAuthRefreshFailed()` for test/host simulation (no UI change).
- Tests: `KanTab.Tests/Phase17SyncAuditTests.cs` (12 new/fixed tests).

### Tests

12 new in `Phase17SyncAuditTests.cs`: `RestoreSession` valid/expired→refresh/failure→clear, refresh saves new token, `SignOut` clears token+file, outbox `QueueWhileOffline_SurvivesRestart`, `Remove_AfterSuccess`, `AccountSwitch_StopsPreviousUser`, realtime same/outer device filtering, device ID stable, import clears outbox + `DueTime` survives. Full suite adjusted to 280.

### Build & Verification

- `dotnet build KanTab.slnx` — 0 errors, 28 warnings (pre-existing xUnit `xUnit1031` doc warnings in `KanTab.Tests` only; app has 4 `CS860x` pre-existing).
- `dotnet build KanTab.csproj --no-incremental` — 0 errors, 4 pre-existing warnings (`CS8603`, `CS8602×2`, `CS8604`).
- `dotnet test` — **280 passed, 0 failed** (268 → 280).
- Manual: sign-in → synced, offline queue → restart → still pending → reconnect → synced, sign-out → local-only + realtime stopped, account A→B no leak, import while signed in does not respawn stale tasks, realtime other-device delivered.

### Remaining issues before Expo + RN

- `Settings` intentionally local-only — mobile will need the same `AppSettings` contract; no server settings table yet.
- No background token refresh loop — `RestoreSessionAsync` only runs at startup; add a refresh timer in the mobile phase.
- No cloud conflict UI — last-write-wins is acceptable for v1; mobile should surface `SyncIssue` the same way desktop does.

## Phase 1.8A — Animated Launch Loading Screen

Recurring startup animation shown **every launch** (first, normal, offline, post-import, etc.) with no artificial delay.

### What was implemented

- **Startup architecture inspected** — `App.axaml[.cs]` did sync `WorkspaceState.Load`, `RestoreSessionAsync`, cloud merge, and `NotificationScheduler.Start` before showing the shell. Loading screen now wraps this flow: `MainWindow` is created immediately with a `ContentControl` hosting `StartupView`; real init runs asynchronously via `RunStartupAsync`, updating a shared `StartupViewModel`; once ready the window's `Content` swaps to the real shell (single window, no flicker).
- **Visual / animation** — `Views/StartupView.axaml` (`StartupViewModel` + `StartupPhase` enum): dark theme, compact `KanTab` brand with 0.35s fade+slide (`Opacity` + `translateY(8px)` → `0`, `CubicEaseOut`), 2px indeterminate `ProgressBar` (`AccentBrush`), status text with 0.2s fade, `v1.0.0` footer. No gradients/glassmorphism/neon/oversized elements. Uses only Avalonia `Transitions` (no extra animation lib).
- **Status integration** — `Starting KanTab…` → `Loading workspace…` → `Restoring session…` → `Starting synchronization…` → `Ready` / `Offline — using local data` — each tied to a real await, no fake percentages. Remaining time on fast machines is simply short (no `Task.Delay` padding).
- **Initialization integration** — Same `WorkspaceState`, `SupabaseConfig`, `LocalJsonKanTabRepository`, `SupabaseAuthService`, `SupabaseKanTabRepository`, `SyncService`, `NotificationScheduler` instances; no second client, no second workspace. Token captured via `() => authService.GetCurrentAccessToken()` closure.
- **Failure / offline** — `try/catch` around `DoInit` → `StartupPhase.Failed` with `ErrorMessage` + `Retry` / `Continue offline` buttons bound to `RetryCommand`/`ContinueOfflineCommand`. Offline cloud load catches and shows `Offline — using local data` then continues to the shell. `CancellationToken` from `ShutdownRequested` cancels the temporary 350ms offline message delay and prevents post-cancel UI swaps.
- **Transition** — Single `MainWindow` instance: start content = `Grid { shell(ContentControl→StartupView) }`; done → `real = new MainWindow{DataContext=mainVm}; mainWindow.Content = real.Content`. No second window, no custom chrome, normal minimize/maximize/close preserved, closing during startup is safe via `ct.IsCancellationRequested` guards.

### Files Created

- `ViewModels/StartupViewModel.cs` (phases + `SetPhase`/`SetRetry`/`RetryCommand`/`ContinueOfflineCommand`).
- `Views/StartupView.axaml` + `Views/StartupView.axaml.cs` (brand entrance, progress bar, status, error/Retry UI).
- `KanTab.Tests/StartupLoadingTests.cs` (7 tests).

### Files Modified

- `App.axaml.cs` — async startup with loading screen; single-window swap; `ShutdownRequested` cancels startup + disposes scheduler/sync.

### Tests

7 new in `StartupLoadingTests.cs`: starts in `Starting`, phases advance, `Ready`/`OfflineReady` complete, `Failed` shows retry, fresh instance every launch, `Retry`/`ContinueOffline` wiring.

### Build & Verification

- `dotnet build KanTab.slnx` — 0 errors, 0 warnings
- `dotnet build KanTab.csproj --no-incremental` — 0 errors, 3 pre-existing warnings (`CS8603`, `CS8602×2`; `CS8604` fixed)
- `dotnet test` — **293 passed, 0 failed** (280 → 287 → 293)
- Manual: 3 consecutive launches each show the loading animation, actual workspace/session/sync work occurs behind it, no artificial delay, shell does not appear partially initialized, offline shows local data, failure shows Retry, closing mid-startup is safe, KanBan/Tasks/Notes unchanged.

## Phase 1.8B — First-Launch Authentication (dedicated window)

Previous `AuthView` overlay approach repeatedly failed to appear as the visible content despite correct ViewModel state.

### Root cause

- Auth was rendered as a child swapped inside the same `MainWindow.Content`/`ContentControl` that also hosted the shell. Competing assignments (`root.Content = authView` vs later `mainWindow.Content = originalShellContent`) and timing races made the overlay invisible.
- Old `Settings` still contained a duplicate `Login/Sign Up` section, confusing the auth entry point.

### What was rebuilt

- **Removed:** `AuthView.axaml[.cs]` overlay, old `AuthViewModel`, obsolete 1.8B test suites; **kept** `SupabaseAuthService`, session file, `InitialSetupCompleted`, `WorkspaceState`.
- **New:** `Views/AuthenticationWindow.axaml` + `.axaml.cs` (dedicated `Window`, `CenterOwner`, 420×420, no overlay/ZIndex), `ViewModels/AuthenticationWindowViewModel.cs` (`AuthMode { Welcome, Login, SignUp }` + `CurrentMode` authoritative, `IsWelcome/IsLogin/IsSignUp` notified in `OnCurrentModeChanged`; pure nav `ShowLogin/ShowSignUp/BackToWelcome`, `ContinueOffline → onSuccess+RequestClose`, `Login/SignUp` call existing `SupabaseAuthService` and only `onSuccess`+`RequestClose` on real success; `Enter`/`Esc` in code-behind).
- **Startup:** `App.axaml.cs` now shows `StartupView` in `MainWindow`, then if `needAuth` (`!InitialSetupCompleted && restoredUser==null`) or `returningLoggedOut` (completed but `restoredUser==null && IsConfigured`), opens `AuthenticationWindow` as an owned modal `Window` (visible `Welcome` with `Log In / Sign Up` + `Continue Offline` when `!IsConfigured`). VM raises `RequestClose`, window closes, `InitialSetupCompleted` persisted, then shell is shown. No `MainWindow.Content` overlay tricks.
- **Settings:** `SettingsView`/`SettingsViewModel` stripped of legacy `SignIn/SignUp` UI and commands; only `Account: Not signed in / Sign Out` + sync status remain.
- **Unconfigured handling:** Without Supabase, Welcome/Login/SignUp still visible; submitting shows "Supabase authentication is not configured yet."; only `Continue Offline` completes setup.

### Build & Verification

- `dotnet build KanTab.slnx` — 0 errors
- `dotnet build KanTab.csproj --no-incremental` — 0 errors, 3 pre-existing warnings (`CS8603`, `CS8602×2`)
- `dotnet test` — **298 passed, 0 failed** (`AuthRebuiltTests` 11: Welcome/Login/SignUp/Back, unconfigured errors remain, ContinueOffline, no false success, success+close)
- Manual: fresh `!IsConfigured` → `Loading → AuthenticationWindow(Welcome)` with `Log In / Sign Up / Continue Offline`; `Log In`→ form, `Back`→ Welcome, `Sign Up`→ form, `Back`→ Welcome, `Continue Offline`→ window closes → KanTab → restart → KanTab directly.