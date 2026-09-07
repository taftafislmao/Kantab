-- Supabase migration: Add indexes for board archival and task due dates
-- Improves query performance for archived board filtering and overdue task lookups.

-- Index for filtering archived/non-archived boards
create index if not exists idx_boards_archived
    on public.boards (archived_at)
    where archived_at is not null;

create index if not exists idx_boards_deleted
    on public.boards (deleted_at)
    where deleted_at is not null;

-- Index for querying active (non-archived, non-deleted) boards
create index if not exists idx_boards_active
    on public.boards (user_id, position)
    where archived_at is null and deleted_at is null;

-- Index for task due date filtering (overdue tasks)
-- tasks table uses is_completed, not completed_at
create index if not exists idx_tasks_due_date
    on public.tasks (due_date)
    where is_completed = false;
