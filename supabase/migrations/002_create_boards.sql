-- Supabase migration: Create boards table
-- This migration creates the boards table and sets up RLS policies.

-- Create boards table
create table public.boards (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    name text not null,
    position integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    archived_at timestamptz null,
    deleted_at timestamptz null
);

-- Enable Row Level Security on boards
alter table public.boards enable row level security;

-- Policies for boards: authenticated users can only access their own boards
create policy "Users can select own boards"
    on public.boards for select
    using (auth.uid() = user_id);

create policy "Users can insert own boards"
    on public.boards for insert
    with check (auth.uid() = user_id);

create policy "Users can update own boards"
    on public.boards for update
    using (auth.uid() = user_id);

create policy "Users can delete own boards"
    on public.boards for delete
    using (auth.uid() = user_id);

-- Create updated_at trigger function
create or replace function public.handle_updated_at()
returns trigger as $$
begin
    new.updated_at = now();
    return new;
end;
$$ language plpgsql;

-- Create trigger for boards updated_at
create trigger boards_updated_at
    before update on public.boards
    for each row execute function public.handle_updated_at();
