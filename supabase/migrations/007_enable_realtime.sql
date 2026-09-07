-- Supabase migration: Enable Realtime replication for KanTab tables
-- This migration enables realtime change replication for all user-owned tables.

-- Add tables to the realtime publication
-- In Supabase, this is done by adding the tables to the supabase_realtime publication

-- First, check if the publication exists and add tables to it
-- PostgreSQL requires tables to be in a publication to receive realtime events

-- Add all user-owned tables to the realtime publication
-- Use the supported publication method; the old realtime.schema_mappings table
-- was removed in newer Supabase versions (hence 42P01).

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'supabase_realtime') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE public.boards;
        ALTER PUBLICATION supabase_realtime ADD TABLE public.columns;
        ALTER PUBLICATION supabase_realtime ADD TABLE public.tasks;
        ALTER PUBLICATION supabase_realtime ADD TABLE public.task_tags;
        ALTER PUBLICATION supabase_realtime ADD TABLE public.notes;
    END IF;
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Note: In Supabase, you can also enable realtime via the dashboard:
-- 1. Go to Database > Replication
-- 2. Enable Realtime for each table
--
-- Or use the SQL editor to run:
-- SELECT realtime.add_to_realtime('public', 'boards');
-- SELECT realtime.add_to_realtime('public', 'columns');
-- SELECT realtime.add_to_realtime('public', 'tasks');
-- SELECT realtime.add_to_realtime('public', 'task_tags');
-- SELECT realtime.add_to_realtime('public', 'notes');
