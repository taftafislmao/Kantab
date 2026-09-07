-- Supabase migration: Create profiles table
-- This migration creates the profiles table and sets up RLS policies.

-- Create profiles table
create table public.profiles (
    id uuid primary key references auth.users(id) on delete cascade,
    display_name text null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

-- Enable Row Level Security on profiles
alter table public.profiles enable row level security;

-- Policies for profiles: authenticated users can only access their own profile
create policy "Users can select own profile"
    on public.profiles for select
    using (auth.uid() = id);

create policy "Users can insert own profile"
    on public.profiles for insert
    with check (auth.uid() = id);

create policy "Users can update own profile"
    on public.profiles for update
    using (auth.uid() = id);

create policy "Users can delete own profile"
    on public.profiles for delete
    using (auth.uid() = id);

-- Create a function to automatically create a profile when a new user signs up
create or replace function public.handle_new_user()
returns trigger as $$
begin
    insert into public.profiles (id, display_name)
    values (
        new.id,
        coalesce(new.raw_user_meta_data->>'display_name', '')
    );
    return new;
end;
$$ language plpgsql security definer;

-- Create trigger to call handle_new_user on auth.user creation
create trigger on_auth_user_created
    after insert on auth.users
    for each row execute function public.handle_new_user();
