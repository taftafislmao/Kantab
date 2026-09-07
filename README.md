# KanTab

A fast, local-first desktop task manager built with **Avalonia UI**, **.NET 10** and **Supabase**.

KanBan · Tasks · Notes — with offline support, sync-ready storage, and a compact, distraction-free desktop UI.

---

## Features

- **KanBan board** — drag & drop, column management, checklists, due dates & priorities
- **Tasks list** — search, filter (All / Active / Completed), sort, inline completion
- **Notes** — autosave, pinned-first ordering, search
- **Global Search** (`Ctrl+K`) and **Command Palette** (`Ctrl+Shift+P`)
- **Task Details** — double-click to view/edit checklist and description
- **Backup / Import** — export and restore a versioned local snapshot
- **Notifications** — upcoming and overdue reminders (configurable lead time)
- **Local-first** — all data lives in `%LOCALAPPDATA%\KanTab\kantab.json` with atomic writes
- **Cloud sync** (optional) — Supabase Auth + Postgres + Realtime when configured, otherwise fully offline

---

## Tech Stack

- **.NET 10**, **Avalonia UI 12** (MVVM, CommunityToolkit.Mvvm)
- **xUnit** for tests
- **Supabase** (GoTrue Auth, Postgres, RLS, Realtime) — optional

---

## Getting Started

### Prerequisites

- .NET SDK 10.0+
- Windows 10/11 (Linux/macOS work for the desktop shell, Windows notifications are Windows-only)

### Run

```bash
dotnet build KanTab.slnx
dotnet run --project KanTab/KanTab.csproj
```

### Test

```bash
dotnet test
```

Clean build (CI style):

```bash
dotnet build KanTab.slnx
dotnet build KanTab/KanTab.csproj --no-incremental
```

---

## Configuration

KanTab runs fully offline without any configuration.

To enable cloud sync, provide Supabase credentials **one** of these ways (first match wins):

**Environment variables (recommended)**

| Variable | Purpose |
|---|---|
| `KANTAB_SUPABASE_URL` / `SUPABASE_URL` | Supabase project URL |
| `KANTAB_SUPABASE_ANON_KEY` / `SUPABASE_ANON_KEY` | Supabase public anon key |
| `KANTAB_AUTH_WEB_URL` | Auth web base URL (desktop opens `…/sign-in`, `…/sign-up`) |

```bash
# Windows PowerShell
setx KANTAB_SUPABASE_URL "https://xxx.supabase.co"
setx KANTAB_SUPABASE_ANON_KEY "eyJ..."
setx KANTAB_AUTH_WEB_URL "http://localhost:5173"
```

**Local file** (dev, git-ignored)

- `%LOCALAPPDATA%\KanTab\supabase.json` — `{"url":"...","anonKey":"..."}`
- `<exe dir>/supabase.local.json` — same shape

Never commit the service-role key. Only the anon/public key belongs in the client. See [SUPABASE_SETUP.md](SUPABASE_SETUP.md) for the full setup guide.

**Auth web (optional)**

If you host `KanTabAuthWeb`:

```bash
cd KanTabAuthWeb
cp .env.example .env        # then fill in VITE_SUPABASE_URL / VITE_SUPABASE_ANON_KEY
npm install
npm run dev    # http://localhost:5173
npm run build  # production build
```

---

## Project Structure

```
KanTab/
  KanTab/                 # desktop app — KanTab.slnx
  KanTab.Tests/           # xUnit tests
  KanTabAuthWeb/          # auth web (Vite + React, optional)
  supabase/migrations/    # Postgres schema & RLS
```

Key areas inside `KanTab/KanTab`:

- `Models/` — Board, KanBanColumn, TaskItem, ChecklistItem, Note
- `ViewModels/` — one VM per workspace + WorkspaceState (single source of truth)
- `Views/` — Avalonia views, AuthenticationWindow, StartupView
- `Storage/` — LocalJson repository, WorkspaceState persistence
- `Storage/Supabase/` — auth, sync/outbox, realtime
- `Services/` — search, notifications, file dialogs
- `Themes/` — dark restrained theme

---

## Notes

- **Offline by default** — if Supabase is unconfigured, `Continue Offline` on first launch marks setup complete and the app stays local-only.
- **Atomic writes** — saves go via a temp file then move, so a crash cannot corrupt `kantab.json`.
- **Security** — secrets are git-ignored (`supabase.local.json`, `.env`). Only the public anon key is ever shipped to clients.

## License

Private — all rights reserved.
