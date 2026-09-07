-- Supabase migration: Enable Realtime replication for KanTab tables
-- This migration enables realtime change replication for all user-owned tables.

-- Add tables to the realtime publication
-- In Supabase, this is done by adding the tables to the supabase_realtime publication

-- First, check if the publication exists and add tables to it
-- PostgreSQL requires tables to be in a publication to receive realtime events

-- Add all user-owned tables to the realtime publication
-- Note: This requires the supabase_realtime extension to be enabled

INSERT INTO realtime.schema_mappings (server_name, schema_name, table_name, alias_name)
VALUES
    ('supabase', 'public', 'boards', 'boards'),
    ('supabase', 'public', 'columns', 'columns'),
    ('supabase', 'public', 'tasks', 'tasks'),
    ('supabase', 'public', 'task_tags', 'task_tags'),
    ('supabase', 'public', 'notes', 'notes')
ON CONFLICT DO NOTHING;

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
