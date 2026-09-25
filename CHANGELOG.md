# Changelog

Notable changes to Ping Runner. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and versions follow [Semantic Versioning](https://semver.org/). `Version.props` already holds the next
release's number; the section moves from `[Unreleased]` to that number in the release commit.

## [Unreleased]

### Added

- Reports: a new Reports page makes a PDF to hand over or an Excel workbook to work with, from the
  live session, what the Graph shows, a stored run, or everything stored for a target between two
  dates. A report has the key figures, findings in plain words, charts (latency over time with lost
  pings and outages marked, loss per slice of time, how replies were spread, speed tests), a table
  over time, every outage, the speed tests in the period, the connection details and how it was all
  measured. A title and notes (a ticket number, an address) go on the front. The public IP address
  stays out unless you tick it. The workbook has a sheet per table with real numbers, and every ping.
- Import ping files into the History: each target in the file becomes a stored run, marked with the
  file it came from, ready for the Graph and for reports.
- A Report button on each run in the History, and on the Graph for what it shows.

### Changed

- Import reads Excel workbooks, and CSV files that a spreadsheet saved again or another tool wrote:
  semicolons or tabs, renamed or reordered columns, local date formats, decimal commas and a UTC
  offset column. Rows that cannot be read are left out and listed, instead of failing the file.
- The Graph's CSV and PNG exports are under one Export menu, next to Import and Report.

## [2.1.0] - 2026-09-25

### Added

- History: every ping run, with every ping, and every speed test is kept on this computer until you
  delete it. A run is saved while it goes, so a crash loses at most a few seconds, and a run a crash
  left open is marked "Interrupted" at the next start. The History page lists runs and speed tests
  with their figures; a run opens in the Graph, exports as CSV or is deleted.
- Back up, restore and clear the history from Settings. A restore checks the file first and keeps
  the previous history as `history.before-restore.db`.
- "Open router" on the Connection page opens the gateway's admin page in the browser.

### Changed

- The Speed test page lists the last 20 stored tests and shows the latest one when the app starts,
  instead of only this session's tests.

## [2.0.0] - 2026-09-25

### Added

- One window with a navigation pane: Monitor, Graph, Speed test, Connection and Settings.
- New figures on the Monitor: median with p95 and p99, jitter, packet loss, outages (two or more lost
  pings in a row) with the longest one, an estimated call-quality score, and the mean of the 50 worst
  spikes.
- A speed test against Cloudflare: average and peak download and upload, idle and loaded latency, and a
  bufferbloat grade, with a live chart and this session's history.
- A Connection page with the adapter, link speed, addresses, gateway and DNS servers, one-click pings
  to each, and the public IP (hidden until shown).
- Light, dark and Windows themes, and four accent colors. The accent tints each page's header icon,
  the figure icons, section labels, secondary buttons and the selected page in the menu.
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
