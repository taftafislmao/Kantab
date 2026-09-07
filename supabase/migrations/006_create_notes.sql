-- Supabase migration: Create notes table
-- This migration creates the notes table and sets up RLS policies.

-- Create notes table
create table public.notes (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    title text not null,
    content text not null default '',
    is_pinned boolean not null default false,
    position integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    deleted_at timestamptz null
);

-- Enable Row Level Security on notes
alter table public.notes enable row level security;

-- Policies for notes: authenticated users can only access their own notes
create policy "Users can select own notes"
    on public.notes for select
    using (auth.uid() = user_id);

create policy "Users can insert own notes"
    on public.notes for insert
    with check (auth.uid() = user_id);

create policy "Users can update own notes"
    on public.notes for update
    using (auth.uid() = user_id);

create policy "Users can delete own notes"
    on public.notes for delete
    using (auth.uid() = user_id);

-- Create trigger for notes updated_at
create trigger notes_updated_at
    before update on public.notes
    for each row execute function public.handle_updated_at();
