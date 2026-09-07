-- Supabase migration: Add checklist support to tasks
-- Stores ordered checklist items as JSONB array inside tasks table.
-- Each item: { id uuid, text text, is_completed boolean, position integer, created_at timestamptz, updated_at timestamptz }

alter table public.tasks add column if not exists checklist jsonb not null default '[]'::jsonb;

create index if not exists idx_tasks_has_checklist on public.tasks ((jsonb_array_length(checklist) > 0)) where jsonb_array_length(checklist) > 0;
