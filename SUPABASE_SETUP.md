# KanTab Supabase Integration - Developer Guide

## Overview

KanTab uses Supabase as its cloud data foundation for future synchronization. This guide explains how to set up Supabase for development and production.

## Prerequisites

- A Supabase project (create at https://supabase.com/docs)
- Node.js and npm installed
- Git

## Creating a Supabase Project

1. Go to [Supabase Dashboard](https://app.supabase.com)
2. Click "New Project"
3. Set a project name, database password, and region
4. Wait for the project to initialize
5. Note your **Project URL** and **anon/public key** from Settings > API

## Required Environment Variables

KanTab reads Supabase configuration from environment variables:

```bash
SUPABASE_URL=https://your-project-ref.supabase.co
SUPABASE_ANON_KEY=your-anon-key-here
```

### Where to set them

- **Windows**: `setx SUPABASE_URL "https://..."` and `setx SUPABASE_ANON_KEY "..."`
- **macOS/Linux**: Add to `~/.bashrc` or `~/.zshrc`
- **Development**: Use `supabase.local.json` (git-ignored)

## Running Database Migrations

1. Navigate to the `supabase/migrations` directory
2. Run each SQL file in order in the Supabase SQL Editor:
   - `001_create_profiles.sql`
   - `002_create_boards.sql`
   - `003_create_columns.sql`
   - `004_create_tasks.sql`
   - `005_create_task_tags.sql`
   - `006_create_notes.sql`

Or use the Supabase CLI:
```bash
supabase db push
```

## Configuring Authentication

1. In Supabase Dashboard, go to **Authentication > Settings**
2. Enable **Email** authentication provider
3. Configure email templates if needed
4. Ensure "Confirm email" is disabled for development (or enable for production)

## Why the Service-Role Key Must Never Be in the Desktop Client

The service-role key bypasses Row Level Security (RLS). If exposed in the desktop client, any user could:
- Read all users' data
- Modify or delete any user's data
- Bypass all database security policies

**Only the public anonymous key** is used in the desktop client. The service-role key is only used server-side via Supabase Edge Functions or database triggers.

## Row Level Security (RLS)

RLS protects user data at the database level:

- Every table has RLS enabled
- Policies ensure users can only access rows where `user_id = auth.uid()`
- Child tables (columns, tasks, tags) also protect against parent access by other users
- A database trigger automatically creates profile rows for new users

## Running in Local-Only Mode

If no Supabase configuration exists, KanTab runs in local-only mode:

1. No environment variables set
2. No `supabase.local.json` file present
3. The app uses `LocalJsonKanTabRepository` exclusively

This allows full development and testing without Supabase.

## Configuration Files

### Safe example configuration file

Create `supabase.local.json` (git-ignored):

```json
{
  "url": "https://your-project-ref.supabase.co",
  "anonKey": "your-anon-key-here"
}
```

### .gitignore additions

Ensure these files are excluded from source control:

```
# Supabase credentials
supabase.local.json
.env
.env.local

# Never commit service-role keys
```

## Testing

1. Create a test Supabase account
2. Sign up, sign in, and sign out
3. Restart the app and verify session restoration
4. Create boards, columns, tasks, tags, and notes
5. Confirm they save locally
6. Confirm they upload to Supabase for the signed-in user
7. Verify another authenticated user cannot read the first user's data
8. Turn off network access and confirm local KanTab usage still works
9. Restore network access and confirm failed cloud saves can retry

## Troubleshooting

- **"Supabase is not configured"**: Check environment variables or `supabase.local.json`
- **Authentication fails**: Verify email/password and that email auth is enabled in Supabase
- **RLS errors**: Ensure migrations are applied and policies are created
- **Session not restored**: Check that `session.json` exists in `%LOCALAPPDATA%\KanTab\`
