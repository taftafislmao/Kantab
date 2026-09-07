-- Supabase migration: Create task_tags table
-- This migration creates the task_tags table and sets up RLS policies.

-- Create task_tags table
create table public.task_tags (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    task_id uuid not null references public.tasks(id) on delete cascade,
    name text not null,
    created_at timestamptz not null default now()
);

-- Enable Row Level Security on task_tags
alter table public.task_tags enable row level security;

-- Policies for task_tags: authenticated users can only access their own tags
create policy "Users can select own task_tags"
    on public.task_tags for select
    using (
        auth.uid() = user_id or
        auth.uid() in (select user_id from public.tasks where id = task_id)
    );

create policy "Users can insert own task_tags"
    on public.task_tags for insert
    with check (auth.uid() = user_id);

create policy "Users can update own task_tags"
    on public.task_tags for update
    using (auth.uid() = user_id);

create policy "Users can delete own task_tags"
    on public.task_tags for delete
    using (auth.uid() = user_id);
