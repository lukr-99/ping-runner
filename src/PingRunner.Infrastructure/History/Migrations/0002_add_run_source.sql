-- 0002_add_run_source.sql
-- Runs can be read from a file as well as recorded by pinging. Existing runs were all recorded;
-- source_name keeps the file an imported run came from.

ALTER TABLE ping_runs ADD COLUMN source TEXT NOT NULL DEFAULT 'recorded' CHECK (source IN ('recorded', 'imported'));
ALTER TABLE ping_runs ADD COLUMN source_name TEXT;
