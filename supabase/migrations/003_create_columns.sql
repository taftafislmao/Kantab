-- Supabase migration: Create columns table
-- This migration creates the columns table and sets up RLS policies.

-- Create columns table
create table public.columns (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    board_id uuid not null references public.boards(id) on delete cascade,
    title text not null,
    position integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    deleted_at timestamptz null
);

-- Enable Row Level Security on columns
alter table public.columns enable row level security;

-- Policies for columns: authenticated users can only access their own columns
-- Users can access columns where they own the parent board
create policy "Users can select own columns"
    on public.columns for select
    using (
        auth.uid() = user_id or
        auth.uid() in (select user_id from public.boards where id = board_id)
    );

create policy "Users can insert own columns"
    on public.columns for insert
    with check (auth.uid() = user_id);

create policy "Users can update own columns"
    on public.columns for update
    using (auth.uid() = user_id);

create policy "Users can delete own columns"
    on public.columns for delete
    using (auth.uid() = user_id);

-- Create trigger for columns updated_at
create trigger columns_updated_at
    before update on public.columns
    for each row execute function public.handle_updated_at();
