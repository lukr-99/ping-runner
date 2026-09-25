-- 0001_initial.sql
-- Ping runs with every attempt, and speed tests. The migration runner owns the transaction.
-- Times are Unix milliseconds (UTC) plus the local offset in minutes that was in effect, so a run
-- reads back exactly as it was recorded.

CREATE TABLE ping_runs (
    id INTEGER PRIMARY KEY,
    target_host TEXT NOT NULL,
    started_at_ms INTEGER NOT NULL,
    started_offset_min INTEGER NOT NULL,
    ended_at_ms INTEGER,
    interval_ms INTEGER NOT NULL CHECK (interval_ms > 0),
    timeout_ms INTEGER NOT NULL CHECK (timeout_ms > 0),
    planned_duration_ms INTEGER,
    outcome TEXT NOT NULL CHECK (outcome IN ('running', 'completed', 'stopped', 'interrupted')),
    sent INTEGER NOT NULL DEFAULT 0,
    received INTEGER NOT NULL DEFAULT 0,
    mean_ms REAL,
    median_ms REAL,
    p95_ms REAL,
    min_ms REAL,
    max_ms REAL,
    jitter_ms REAL,
    outages INTEGER NOT NULL DEFAULT 0,
    longest_outage_ms INTEGER,
    mos REAL
);

CREATE INDEX ix_ping_runs_started_at ON ping_runs (started_at_ms);

CREATE TABLE ping_attempts (
    run_id INTEGER NOT NULL REFERENCES ping_runs (id) ON DELETE CASCADE,
    sequence INTEGER NOT NULL,
    timestamp_ms INTEGER NOT NULL,
    offset_min INTEGER NOT NULL,
    is_success INTEGER NOT NULL CHECK (is_success IN (0, 1)),
    roundtrip_ms INTEGER,
    details TEXT NOT NULL,
    PRIMARY KEY (run_id, sequence)
) WITHOUT ROWID;

-- Series are JSON arrays of [elapsed_ms, bits_per_second]; latencies are JSON arrays of milliseconds.
CREATE TABLE speed_tests (
    id INTEGER PRIMARY KEY,
    started_at_ms INTEGER NOT NULL,
    started_offset_min INTEGER NOT NULL,
    server TEXT NOT NULL,
    latency_host TEXT NOT NULL,
    download_average_bps REAL NOT NULL,
    download_peak_bps REAL NOT NULL,
    download_bytes INTEGER NOT NULL,
    download_duration_ms INTEGER NOT NULL,
    download_series TEXT NOT NULL,
    upload_average_bps REAL NOT NULL,
    upload_peak_bps REAL NOT NULL,
    upload_bytes INTEGER NOT NULL,
    upload_duration_ms INTEGER NOT NULL,
    upload_series TEXT NOT NULL,
    idle_latency TEXT NOT NULL,
    download_latency TEXT NOT NULL,
    upload_latency TEXT NOT NULL
);

CREATE INDEX ix_speed_tests_started_at ON speed_tests (started_at_ms);
