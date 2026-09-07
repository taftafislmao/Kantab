-- Supabase migration: Create tasks table
-- This migration creates the tasks table and sets up RLS policies.

-- Create tasks table
create table public.tasks (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    board_id uuid not null references public.boards(id) on delete cascade,
    column_id uuid not null references public.columns(id) on delete cascade,
    title text not null,
    description text not null default '',
    priority text not null default 'Medium',
    due_date date null,
    is_completed boolean not null default false,
    position integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    deleted_at timestamptz null
);

-- Enable Row Level Security on tasks
alter table public.tasks enable row level security;

-- Policies for tasks: authenticated users can only access their own tasks
-- Users can access tasks where they own the parent board or column
create policy "Users can select own tasks"
    on public.tasks for select
    using (
        auth.uid() = user_id or
        auth.uid() in (select user_id from public.boards where id = board_id) or
        auth.uid() in (select user_id from public.columns where id = column_id)
    );

create policy "Users can insert own tasks"
    on public.tasks for insert
    with check (auth.uid() = user_id);

create policy "Users can update own tasks"
    on public.tasks for update
    using (auth.uid() = user_id);

create policy "Users can delete own tasks"
    on public.tasks for delete
    using (auth.uid() = user_id);

-- Create trigger for tasks updated_at
create trigger tasks_updated_at
    before update on public.tasks
    for each row execute function public.handle_updated_at();
