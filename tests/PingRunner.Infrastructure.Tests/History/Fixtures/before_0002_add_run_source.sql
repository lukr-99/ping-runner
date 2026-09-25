-- Representative rows of a history at migration 0001, for testing 0002 from its predecessor
-- (tools/migrations.py test --migration 2 --fixture ..., and SqliteMigratorTests).

INSERT INTO ping_runs (id, target_host, started_at_ms, started_offset_min, ended_at_ms, interval_ms, timeout_ms, planned_duration_ms,
                       outcome, sent, received, mean_ms, median_ms, p95_ms, min_ms, max_ms, jitter_ms, outages, longest_outage_ms, mos)
VALUES (1, '8.8.8.8', 1790338800000, 120, 1790338803000, 1000, 1000, 300000,
        'completed', 4, 3, 16.0, 16.0, 17.0, 15.0, 17.0, 1.0, 0, NULL, 4.4),
       (2, 'router.local', 1790342400000, 120, NULL, 500, 1000, NULL,
        'interrupted', 1, 0, NULL, NULL, NULL, NULL, NULL, NULL, 0, NULL, NULL);

INSERT INTO ping_attempts (run_id, sequence, timestamp_ms, offset_min, is_success, roundtrip_ms, details)
VALUES (1, 0, 1790338800000, 120, 1, 15, 'Reply from 8.8.8.8'),
       (1, 1, 1790338801000, 120, 1, 16, 'Reply from 8.8.8.8'),
       (1, 2, 1790338802000, 120, 0, NULL, 'Request timed out'),
       (1, 3, 1790338803000, 120, 1, 17, 'Reply from 8.8.8.8'),
       (2, 0, 1790342400000, 120, 0, NULL, 'Destination host unreachable');

INSERT INTO speed_tests (id, started_at_ms, started_offset_min, server, latency_host,
                         download_average_bps, download_peak_bps, download_bytes, download_duration_ms, download_series,
                         upload_average_bps, upload_peak_bps, upload_bytes, upload_duration_ms, upload_series,
                         idle_latency, download_latency, upload_latency)
VALUES (1, 1790338900000, 120, 'Cloudflare', '1.1.1.1',
        480000000.0, 520000000.0, 600000000, 10000, '[[0,0],[1000,480000000]]',
        95000000.0, 110000000.0, 120000000, 10000, '[[0,0],[1000,95000000]]',
        '[12,13,14]', '[40,42,44]', '[30,32]');
