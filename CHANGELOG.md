# Changelog

Notable changes to Ping Runner. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and versions follow [Semantic Versioning](https://semver.org/). `Version.props` already holds the next
release's number; the section moves from `[Unreleased]` to that number in the release commit.

## [Unreleased]

Everything below ships as 2.0.0.

### Added

- One window with a navigation pane: Monitor, Graph, Speed test, Connection and Settings.
- New figures on the Monitor: median with p95 and p99, jitter, packet loss, outages (two or more lost
  pings in a row) with the longest one, an estimated call-quality score, and the mean of the 50 worst
  spikes.
- A speed test against Cloudflare: average and peak download and upload, idle and loaded latency, and a
  bufferbloat grade, with a live chart and this session's history.
- A Connection page with the adapter, link speed, addresses, gateway and DNS servers, one-click pings
  to each, and the public IP (hidden until shown).
- Light, dark and Windows themes, and four accent colors.
- Copy a text summary of a session.
- Graph: ranges up to 24 hours, zoom with the mouse wheel at the pointer, drag to pan, hover readout,
  and figures for exactly what is on screen.
- Settings for how many pings a session keeps and for the speed test's length and streams.
- An update check that opens the newest release's page.
- A per-user installer with a SHA-256 checksum, and a release workflow that drafts GitHub Releases.
- `pingrunner speed` in the console app.

### Changed

- The graph is a page instead of a separate window.
- Pings go out on a fixed schedule, so a slow reply no longer stretches the interval.
- Pings carry a 32-byte payload, like Windows' own `ping`.
- Settings are remembered between runs in `%LOCALAPPDATA%\PingRunner\settings.json`.
- CSV import checks every row and names the first bad line.
- Builds that are not release builds carry a `-dev` version.

### Removed

- `install.ps1`. The installer replaces it and removes the copy it installed.

## [1.1.0] - 2026-03-14

### Added

- The current public IP in the header, hidden until shown.
- Versioned install folders and `-Reinstall` in `install.ps1`.

### Fixed

- The graph window keeps its layout when resized.

## [1.0.0] - 2026-03-12

### Added

- The desktop ping runner with a live graph window, CSV import and export, and PNG export.
- The console try app.
